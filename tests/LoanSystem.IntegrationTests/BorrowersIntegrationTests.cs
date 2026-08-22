using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Modules.Borrowers.Domain;
using LoanSystem.Modules.Borrowers.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class BorrowersIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Real_sql_crud_search_concurrency_and_lifecycle()
    {
        using var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(client);
        var suffix = Guid.NewGuid().ToString("N");
        var civil = $"civil-{suffix}";
        var employee = $"employee-{suffix}";
        var createdResponse = await client.PostAsJsonAsync("/api/v1/borrowers", Input(civil, employee, "Integration Borrower", "MOD Integration"));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync()).RootElement;
        var id = created.GetProperty("borrowerId").GetGuid();
        var originalEtag = created.GetProperty("eTag").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(originalEtag));

        var detailResponse = await client.GetAsync($"/api/v1/borrowers/{id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(civil, JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("civilNumber").GetString());
        Assert.False(string.IsNullOrWhiteSpace(detailResponse.Headers.ETag?.Tag));

        var search = await client.GetFromJsonAsync<JsonElement>($"/api/v1/borrowers?civilNumber={civil}&employeeNumber={employee}&name=Integration&organization=MOD&status=Active&pageNumber=1&pageSize=1");
        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(id, search.GetProperty("items")[0].GetProperty("borrowerId").GetGuid());

        var updatedResponse = await Send(client, HttpMethod.Put, $"/api/v1/borrowers/{id}", originalEtag, Input(civil, employee, "Updated Borrower", "Updated Organization"));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = JsonDocument.Parse(await updatedResponse.Content.ReadAsStringAsync()).RootElement;
        var currentEtag = updated.GetProperty("eTag").GetString()!;
        Assert.NotEqual(originalEtag, currentEtag);

        var stale = await Send(client, HttpMethod.Put, $"/api/v1/borrowers/{id}", originalEtag, Input(civil, employee, "Stale", "MOD"));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("borrowers.concurrencyConflict", JsonDocument.Parse(await stale.Content.ReadAsStringAsync()).RootElement.GetProperty("errorCode").GetString());

        var deactivated = await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{id}/deactivate", currentEtag, new { });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var inactive = JsonDocument.Parse(await deactivated.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Inactive", inactive.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/borrowers/{id}")).StatusCode);
        var inactiveSearch = await client.GetFromJsonAsync<JsonElement>($"/api/v1/borrowers?civilNumber={civil}&status=Inactive&pageNumber=1&pageSize=25");
        Assert.Equal(1, inactiveSearch.GetProperty("totalCount").GetInt32());

        var activated = await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{id}/activate", inactive.GetProperty("eTag").GetString()!, new { });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal("Active", JsonDocument.Parse(await activated.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>();
        var persisted = await database.Borrowers.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Updated Borrower", persisted.FullName);
        Assert.Equal("Updated Organization", persisted.Organization);
        Assert.Equal(BorrowerStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task Real_sql_unique_indexes_reject_duplicate_identifiers()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BorrowersDbContext>();
        database.Borrowers.Add(Borrower.Register($"civil-a-{suffix}", $"employee-a-{suffix}", "First", null, "Omani", "MOD", null, null));
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        database.Borrowers.Add(Borrower.Register($"civil-a-{suffix}", $"employee-b-{suffix}", "Civil Duplicate", null, "Omani", "MOD", null, null));
        var civilException = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        AssertSqlUniqueViolation(civilException);
        database.ChangeTracker.Clear();

        database.Borrowers.Add(Borrower.Register($"civil-b-{suffix}", $"employee-a-{suffix}", "Employee Duplicate", null, "Omani", "MOD", null, null));
        var employeeException = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        AssertSqlUniqueViolation(employeeException);

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='borrowers' AND t.name='borrowers'", connection);
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static object Input(string civil, string employee, string name, string organization) => new { civilNumber = civil, employeeNumber = employee, fullName = name, phoneNumber = "90000000", nationality = "Omani", organization, rankGrade = "G7", employmentInformation = "Integration test" };
    private static async Task Login(HttpClient client) => Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode);
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body) { using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); return await client.SendAsync(request); }
    private static void AssertSqlUniqueViolation(DbUpdateException exception) { var sql = Assert.IsType<SqlException>(exception.InnerException); Assert.True(sql.Number is 2601 or 2627); }
}
