using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Modules.IdentityAccess.Domain;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using LoanSystem.Modules.LoanOrigination.Domain;
using LoanSystem.Modules.LoanOrigination.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class LoanOriginationIntegrationTests(IdentitySqlFixture fixture)
{
    static readonly string[] FinancingTypes = ["Build", "Renovate"];
    [Fact]
    public async Task Create_get_search_edit_and_stale_concurrency_use_real_http_and_sql()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var originalTag = createdResponse.Headers.ETag!.Tag.Trim('"');
        var created = await Read(createdResponse);
        var applicationId = created.GetProperty("loanApplicationId").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal(setup.BorrowerName, created.GetProperty("borrowerSnapshot").GetProperty("fullName").GetString());
        Assert.Equal(setup.ProductName, created.GetProperty("productSnapshot").GetProperty("productName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/loan-applications/{applicationId}")).StatusCode);
        var defaults = await Read(await client.GetAsync("/api/v1/loan-applications"));
        Assert.Equal(1, defaults.GetProperty("pageNumber").GetInt32());
        Assert.Equal(25, defaults.GetProperty("pageSize").GetInt32());
        foreach (var query in new[] { $"loanApplicationId={applicationId}", $"borrowerId={setup.BorrowerId}", $"loanProductId={setup.ProductId}", "status=Draft" })
            Assert.Contains((await Read(await client.GetAsync($"/api/v1/loan-applications?{query}"))).GetProperty("items").EnumerateArray(), item => item.GetProperty("loanApplicationId").GetGuid() == applicationId);
        foreach (var query in new[] { "pageNumber=0&pageSize=25", "pageNumber=1&pageSize=101" })
        {
            var invalid = await client.GetAsync($"/api/v1/loan-applications?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("loanApplications.invalidPagination", (await Read(invalid)).GetProperty("errorCode").GetString());
        }

        var updatedResponse = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{applicationId}", originalTag, new { requestedAmount = 400m, financingType = "Renovate" });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.NotEqual(originalTag, updatedResponse.Headers.ETag!.Tag.Trim('"'));
        var updated = await Read(updatedResponse);
        Assert.Equal(400m, updated.GetProperty("requestedAmount").GetDecimal());
        Assert.Equal(created.GetProperty("borrowerSnapshot").GetRawText(), updated.GetProperty("borrowerSnapshot").GetRawText());
        var stale = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{applicationId}", originalTag, new { requestedAmount = 300m, financingType = "Build" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("loanApplications.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>();
        Assert.Equal(400m, (await database.LoanApplications.AsNoTracking().SingleAsync(value => value.Id == applicationId)).RequestedAmount);
    }

    [Fact]
    public async Task Creation_rejects_unavailable_references_types_and_amounts_with_stable_codes()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        await Rejected(client, new { borrowerId = Guid.NewGuid(), loanProductVersionId = setup.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.borrowerNotFound");
        var borrowerDetail = await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}");
        await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{setup.BorrowerId}/deactivate", borrowerDetail.Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, ApplicationInput(setup), "loanApplications.borrowerInactive");
        await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{setup.BorrowerId}/activate", (await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}")).Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = Guid.NewGuid(), requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionNotFound");
        var draft = await CreateDraft(client, setup.ProductId, DateOnly.FromDateTime(DateTime.UtcNow));
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = draft.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionUnavailable");
        var futureProduct = await Read(await client.PostAsJsonAsync("/api/v1/loan-products", new { name = $"Future {Guid.NewGuid():N}" }));
        var futureProductId = futureProduct.GetProperty("loanProductId").GetGuid();
        var future = await CreateDraft(client, futureProductId, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2));
        await Publish(client, futureProductId, future);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = future.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionUnavailable");
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 1m, financingType = "Other" }, "loanApplications.invalidFinancingType", HttpStatusCode.BadRequest);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 0m, financingType = "Build" }, "loanApplications.invalidRequestedAmount", HttpStatusCode.BadRequest);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 1001m, financingType = "Build" }, "loanApplications.invalidRequestedAmount", HttpStatusCode.BadRequest);
        var product = await client.GetAsync($"/api/v1/loan-products/{setup.ProductId}");
        await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{setup.ProductId}/deactivate", product.Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, ApplicationInput(setup), "loanApplications.productVersionUnavailable");
    }

    [Fact]
    public async Task Historical_snapshots_do_not_follow_borrower_master_changes()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var created = await Read(await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup)));
        var applicationId = created.GetProperty("loanApplicationId").GetGuid();
        var borrower = await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}");
        var changed = new { civilNumber = setup.CivilNumber, employeeNumber = setup.EmployeeNumber, fullName = "Changed Name", phoneNumber = "90000000", nationality = "Changed", organization = "MOD", rankGrade = "B", employmentInformation = "Changed" };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/api/v1/borrowers/{setup.BorrowerId}", borrower.Headers.ETag!.Tag.Trim('"'), changed)).StatusCode);
        var loaded = await Read(await client.GetAsync($"/api/v1/loan-applications/{applicationId}"));
        var snapshot = loaded.GetProperty("borrowerSnapshot");
        Assert.Equal(setup.BorrowerName, snapshot.GetProperty("fullName").GetString());
        Assert.Equal("OM", snapshot.GetProperty("nationality").GetString());
        Assert.Equal("A", snapshot.GetProperty("rankGrade").GetString());
        Assert.Equal(setup.ProductName, loaded.GetProperty("productSnapshot").GetProperty("productName").GetString());
    }

    [Fact]
    public async Task Loan_application_permissions_are_independent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/loan-applications")).StatusCode);
        using var admin = await Administrator();
        var setup = await SetupAvailableVersion(admin);
        var createdResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var application = await Read(createdResponse);
        var id = application.GetProperty("loanApplicationId").GetGuid();
        var etag = createdResponse.Headers.ETag!.Tag.Trim('"');
        using var read = await PermissionClient(admin, $"read-{suffix}", "loanApplications.read");
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync("/api/v1/loan-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync($"/api/v1/loan-applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await read.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(read, HttpMethod.Put, $"/api/v1/loan-applications/{id}", etag, new { requestedAmount = 2m, financingType = "Build" })).StatusCode);
        using var create = await PermissionClient(admin, $"create-{suffix}", "loanApplications.create");
        Assert.Equal(HttpStatusCode.Created, (await create.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await create.GetAsync("/api/v1/loan-applications")).StatusCode);
        using var update = await PermissionClient(admin, $"update-{suffix}", "loanApplications.update");
        Assert.Equal(HttpStatusCode.OK, (await Send(update, HttpMethod.Put, $"/api/v1/loan-applications/{id}", etag, new { requestedAmount = 2m, financingType = "Build" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await update.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
    }


    [Fact]
    public async Task Eligibility_and_submission_lifecycle_persist_and_enforce_etags()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var created = await Read(createdResponse);
        var id = created.GetProperty("loanApplicationId").GetGuid();
        var createdTag = createdResponse.Headers.ETag!.Tag.Trim('"');

        var missing = await client.PostAsJsonAsync($"/api/v1/loan-applications/{id}/evaluate-eligibility", new { });
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        Assert.Equal("loanApplications.preconditionRequired", (await Read(missing)).GetProperty("errorCode").GetString());

        var evaluatedResponse = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { });
        Assert.Equal(HttpStatusCode.OK, evaluatedResponse.StatusCode);
        var evaluatedTag = evaluatedResponse.Headers.ETag!.Tag.Trim('"');
        var evaluated = await Read(evaluatedResponse);
        Assert.True(evaluated.GetProperty("eligibilityDecision").GetProperty("isEligible").GetBoolean());
        Assert.Equal(1000m, evaluated.GetProperty("eligibilityDecision").GetProperty("permittedAmount").GetDecimal());

        var stale = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("loanApplications.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());

        var edit = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{id}", evaluatedTag, new { requestedAmount = 400m, financingType = "Renovate" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Read(edit)).GetProperty("eligibilityDecision").ValueKind);

        var reevaluated = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", edit.Headers.ETag!.Tag.Trim('"'), new { });
        var submitted = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", reevaluated.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var submittedBody = await Read(submitted);
        Assert.Equal("Submitted", submittedBody.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, submittedBody.GetProperty("submittedAtUtc").ValueKind);

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>();
        var stored = await database.LoanApplications.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(LoanApplicationStatus.Submitted, stored.Status);
        Assert.NotNull(stored.EligibilityDecision);
        Assert.NotNull(stored.SubmittedAtUtc);

        var currentTag = submitted.Headers.ETag!.Tag.Trim('"');
        foreach (var response in new[] {
            await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{id}", currentTag, new { requestedAmount = 300m, financingType = "Build" }),
            await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", currentTag, new { }),
            await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", currentTag, new { })
        })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("loanApplications.notDraft", (await Read(response)).GetProperty("errorCode").GetString());
        }
    }

    async Task<HttpClient> Administrator() { var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode); return client; }
    static async Task<Setup> SetupAvailableVersion(HttpClient client) { var suffix = Guid.NewGuid().ToString("N"); var civil = $"C-{suffix}"; var employee = $"E-{suffix}"; var name = $"Original {suffix}"; var borrower = await Read(await client.PostAsJsonAsync("/api/v1/borrowers", new { civilNumber = civil, employeeNumber = employee, fullName = name, phoneNumber = "90000000", nationality = "OM", organization = "MOD", rankGrade = "A", employmentInformation = "Active" })); var productName = $"Product {suffix}"; var product = await Read(await client.PostAsJsonAsync("/api/v1/loan-products", new { name = productName })); var productId = product.GetProperty("loanProductId").GetGuid(); var draft = await CreateDraft(client, productId, DateOnly.FromDateTime(DateTime.UtcNow)); await Publish(client, productId, draft); return new(borrower.GetProperty("borrowerId").GetGuid(), productId, draft.VersionId, name, productName, civil, employee); }
    static object ApplicationInput(Setup setup) => new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 500m, financingType = "Build" };
    static async Task<Draft> CreateDraft(HttpClient client, Guid productId, DateOnly from) { var response = await client.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", new { maximumAmount = 1000m, currency = "OMR", deductionPercentage = 10m, financingTypes = FinancingTypes, eligibilityConfiguration = new { requiredNationality = "OM", maximumApplicationCount = 1, rankGradeAmountRules = new[] { new { rankGrade = "A", maximumAmount = 1000m } }, term = new { maximumTermMonths = 240, dueDateRule = "Monthly" } }, effectiveFrom = from, effectiveTo = (DateOnly?)null }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var body = await Read(response); return new(body.GetProperty("versionId").GetGuid(), response.Headers.ETag!.Tag.Trim('"')); }
    static async Task Publish(HttpClient client, Guid productId, Draft draft) => Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{draft.VersionId}/publish", draft.ETag, new { })).StatusCode);
    static async Task Rejected(HttpClient client, object input, string code, HttpStatusCode status = HttpStatusCode.UnprocessableEntity) { var response = await client.PostAsJsonAsync("/api/v1/loan-applications", input); Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await Read(response)).GetProperty("errorCode").GetString()); }
    async Task<HttpClient> PermissionClient(HttpClient administrator, string username, string permissionKey) { Guid roleId; using (var scope = fixture.Factory.Services.CreateScope()) { var database = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>(); var permission = await database.Permissions.SingleAsync(value => value.Key == permissionKey); var role = new Role { Id = Guid.NewGuid(), Name = $"Test {permissionKey} {username}" }; database.Roles.Add(role); database.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id }); await database.SaveChangesAsync(); roleId = role.Id; } const string password = "Disposable_permission_password_123!"; var created = await Read(await administrator.PostAsJsonAsync("/api/v1/users", new { username, displayName = username, password })); Assert.Equal(HttpStatusCode.OK, (await Send(administrator, HttpMethod.Put, $"/api/v1/users/{created.GetProperty("userId").GetGuid()}/roles", created.GetProperty("eTag").GetString()!, new { roleIds = new[] { roleId } })).StatusCode); var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password })).StatusCode); return client; }
    static async Task<JsonElement> Read(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body) { using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); return await client.SendAsync(request); }
    sealed record Setup(Guid BorrowerId, Guid ProductId, Guid VersionId, string BorrowerName, string ProductName, string CivilNumber, string EmployeeNumber);
    sealed record Draft(Guid VersionId, string ETag);
}
