using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LoanSystem.Modules.Borrowers.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class BorrowerImportsIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Validate_persists_document_and_preview_without_borrower_then_execute_is_idempotent()
    {
        var suffix = Guid.NewGuid().ToString("N"); var civil = $"import-civil-{suffix}"; var employee = $"import-employee-{suffix}";
        using var client = await AdminClient();
        var validated = await Validate(client, Workbook([civil, "Imported Person", "Omani", "External", employee]));
        Assert.Equal("Validated", validated.GetProperty("status").GetString()); Assert.Equal(1, validated.GetProperty("validRows").GetInt32()); Assert.NotEqual(Guid.Empty, validated.GetProperty("sourceDocumentId").GetGuid());
        var batchId = validated.GetProperty("batchId").GetGuid();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>();
            Assert.False(await db.Borrowers.AnyAsync(x => x.CivilNumber == civil));
            var batch = await db.ImportBatches.Include(x => x.Rows).SingleAsync(x => x.BatchId == batchId); Assert.Single(batch.Rows); Assert.Equal(validated.GetProperty("sourceDocumentId").GetGuid(), batch.SourceDocumentId);
        }
        var first = await Execute(client, batchId); Assert.Equal("Completed", first.GetProperty("status").GetString()); Assert.Equal(1, first.GetProperty("importedRows").GetInt32()); Assert.Equal(0, first.GetProperty("failedRows").GetInt32()); Assert.NotEqual(Guid.Empty, first.GetProperty("rows")[0].GetProperty("borrowerId").GetGuid());
        var second = await Execute(client, batchId); Assert.Equal(first.GetProperty("rows")[0].GetProperty("borrowerId").GetGuid(), second.GetProperty("rows")[0].GetProperty("borrowerId").GetGuid());
        using (var scope = fixture.Factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>(); Assert.Equal(1, await db.Borrowers.CountAsync(x => x.CivilNumber == civil)); Assert.NotNull((await db.ImportBatches.SingleAsync(x => x.BatchId == batchId)).CompletedAtUtc); }
        using var freshClient = await AdminClient(); var restored = await freshClient.GetFromJsonAsync<JsonElement>($"/api/v1/borrower-imports/{batchId}"); Assert.Equal("Completed", restored.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Partial_invalid_and_existing_conflicts_have_stable_results_and_only_valid_rows_import()
    {
        var suffix = Guid.NewGuid().ToString("N"); var valid = $"valid-{suffix}"; var duplicate = $"duplicate-{suffix}"; var existingCivil = $"existing-{suffix}"; var existingEmployee = $"existing-employee-{suffix}";
        using var client = await AdminClient(); Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/borrowers", Input(existingCivil, existingEmployee))).StatusCode);
        var preview = await Validate(client, Workbook(
            [valid, "Valid Person", "Omani", "External", $"valid-employee-{suffix}"],
            ["", "Missing Civil", "Omani", "External", $"missing-{suffix}"],
            [duplicate, "Duplicate One", "Omani", "External", $"duplicate-a-{suffix}"],
            [duplicate, "Duplicate Two", "Omani", "External", $"duplicate-b-{suffix}"],
            [existingCivil, "Civil Conflict", "Omani", "External", $"other-{suffix}"],
            [$"other-civil-{suffix}", "Employee Conflict", "Omani", "External", existingEmployee]));
        Assert.Equal(1, preview.GetProperty("validRows").GetInt32()); Assert.Equal(5, preview.GetProperty("invalidRows").GetInt32());
        var rows = preview.GetProperty("rows").EnumerateArray().ToArray(); Assert.Equal(2, rows.Count(x => x.GetProperty("errorCodes").EnumerateArray().Any(e => e.GetString() == "borrowerImports.duplicateCivilNumberInFile"))); Assert.Contains(rows, x => x.GetProperty("errorCodes").EnumerateArray().Any(e => e.GetString() == "borrowers.civilNumberConflict")); Assert.Contains(rows, x => x.GetProperty("errorCodes").EnumerateArray().Any(e => e.GetString() == "borrowers.employeeNumberConflict"));
        var completed = await Execute(client, preview.GetProperty("batchId").GetGuid()); Assert.Equal(1, completed.GetProperty("importedRows").GetInt32()); Assert.Equal(5, completed.GetProperty("failedRows").GetInt32());
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>(); Assert.True(await db.Borrowers.AnyAsync(x => x.CivilNumber == valid)); Assert.False(await db.Borrowers.AnyAsync(x => x.CivilNumber == duplicate)); Assert.Equal(1, await db.Borrowers.CountAsync(x => x.CivilNumber == existingCivil));
    }

    [Fact]
    public async Task Execute_revalidates_changed_state_and_concurrent_same_batch_executes_at_most_once()
    {
        var suffix = Guid.NewGuid().ToString("N"); var changedCivil = $"changed-{suffix}"; using var client = await AdminClient();
        var changed = await Validate(client, Workbook([changedCivil, "Changed", "Omani", "External", $"changed-employee-{suffix}"])); Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/borrowers", Input(changedCivil, $"other-{suffix}"))).StatusCode);
        var changedResult = await Execute(client, changed.GetProperty("batchId").GetGuid()); Assert.Equal(0, changedResult.GetProperty("importedRows").GetInt32()); Assert.Equal("borrowers.civilNumberConflict", changedResult.GetProperty("rows")[0].GetProperty("errorCodes")[0].GetString());
        var concurrentCivil = $"concurrent-{suffix}"; var concurrent = await Validate(client, Workbook([concurrentCivil, "Concurrent", "Omani", "External", $"concurrent-employee-{suffix}"])); var id = concurrent.GetProperty("batchId").GetGuid();
        using var firstClient = await AdminClient(); using var secondClient = await AdminClient(); var results = await Task.WhenAll(firstClient.PostAsync($"/api/v1/borrower-imports/{id}/execute", null), secondClient.PostAsync($"/api/v1/borrower-imports/{id}/execute", null)); Assert.All(results, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>(); Assert.Equal(1, await db.Borrowers.CountAsync(x => x.CivilNumber == concurrentCivil)); Assert.Equal("Completed", (await db.ImportBatches.SingleAsync(x => x.BatchId == id)).Status);
    }

    [Fact]
    public async Task Import_endpoints_enforce_policy_and_return_stable_problem_codes()
    {
        using var anonymous = fixture.Factory.CreateClient(); var unknown = Guid.NewGuid(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/borrower-imports/{unknown}")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/v1/borrower-imports/{unknown}/execute", null)).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/borrower-imports/validate", null)).StatusCode);
        using var admin = await AdminClient(); const string password = "Disposable_import_permission_123!"; var username = $"no-import-{Guid.NewGuid():N}"; Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/users", new { username, displayName = username, password })).StatusCode); using var forbidden = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await forbidden.PostAsJsonAsync("/api/v1/auth/login", new { username, password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.GetAsync($"/api/v1/borrower-imports/{unknown}")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.PostAsync($"/api/v1/borrower-imports/{unknown}/execute", null)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.PostAsync("/api/v1/borrower-imports/validate", null)).StatusCode);
        AssertProblem(await admin.GetAsync($"/api/v1/borrower-imports/{unknown}"), HttpStatusCode.NotFound, "borrowerImports.batchNotFound"); AssertProblem(await admin.PostAsync($"/api/v1/borrower-imports/{unknown}/execute", null), HttpStatusCode.NotFound, "borrowerImports.batchNotFound"); AssertProblem(await admin.PostAsync("/api/v1/borrower-imports/validate", null), HttpStatusCode.BadRequest, "borrowerImports.invalidFile");
        using var unsupported = new MultipartFormDataContent(); var text = new ByteArrayContent([1]); text.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); unsupported.Add(text, "file", "borrowers.txt"); AssertProblem(await admin.PostAsync("/api/v1/borrower-imports/validate", unsupported), HttpStatusCode.BadRequest, "borrowerImports.unsupportedFile");
        using var malformed = new MultipartFormDataContent(); var bytes = new ByteArrayContent([1, 2, 3]); bytes.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"); malformed.Add(bytes, "file", "borrowers.xlsx"); AssertProblem(await admin.PostAsync("/api/v1/borrower-imports/validate", malformed), HttpStatusCode.BadRequest, "borrowerImports.invalidFile");
    }

    private async Task<HttpClient> AdminClient() { var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode); return client; }
    private static async Task<JsonElement> Validate(HttpClient client, MemoryStream workbook) { using var form = new MultipartFormDataContent(); using var content = new StreamContent(workbook); content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"); form.Add(content, "file", "borrowers.xlsx"); var response = await client.PostAsync("/api/v1/borrower-imports/validate", form); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(); }
    private static async Task<JsonElement> Execute(HttpClient client, Guid id) { var response = await client.PostAsync($"/api/v1/borrower-imports/{id}/execute", null); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(); }
    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code) { Assert.Equal(status, response.StatusCode); Assert.Equal(code, JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.GetProperty("errorCode").GetString()); }
    private static object Input(string civil, string employee) => new { civilNumber = civil, employeeNumber = employee, fullName = "Existing", nationality = "Omani", organization = "MOD" };
    private static MemoryStream Workbook(params string[][] rows)
    {
        var stream = new MemoryStream(); using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true)) { var part = document.AddWorkbookPart(); part.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook(); var worksheet = part.AddNewPart<WorksheetPart>(); var data = new SheetData(); worksheet.Worksheet = new Worksheet(data); part.Workbook.AppendChild(new Sheets()).Append(new Sheet { Id = part.GetIdOfPart(worksheet), SheetId = 1, Name = "Borrowers" }); data.Append(Row(["Civil Number", "Full Name", "Nationality", "Organization", "Employee Number"], 1)); for (var i = 0; i < rows.Length; i++) data.Append(Row(rows[i], (uint)i + 2)); part.Workbook.Save(); }
        stream.Position = 0; return stream;
    }
    private static Row Row(string[] values, uint index) { var row = new Row { RowIndex = index }; for (var i = 0; i < values.Length; i++) row.Append(new Cell { CellReference = $"{(char)('A' + i)}{index}", DataType = CellValues.InlineString, InlineString = new InlineString(new Text(values[i])) }); return row; }
}
