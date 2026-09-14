using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Modules.IdentityAccess.Domain;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class Task11AuthorizationIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Final_approval_and_loan_queries_enforce_independent_server_side_permissions()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/loans")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await FinalDecision(anonymous, Guid.NewGuid())).StatusCode);

        using var administrator = await Login(IdentityAccessIntegrationTests.Admin, IdentityAccessIntegrationTests.Password);
        using var applicationReader = await PermissionClient(administrator, "loanApplications.read");
        Assert.Equal(HttpStatusCode.Forbidden, (await FinalDecision(applicationReader, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicationReader.GetAsync("/api/v1/loans")).StatusCode);

        using var loanReader = await PermissionClient(administrator, "loans.read");
        Assert.Equal(HttpStatusCode.OK, (await loanReader.GetAsync("/api/v1/loans")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FinalDecision(loanReader, Guid.NewGuid())).StatusCode);

        using var finalApprover = await PermissionClient(administrator, "loanApplications.finalApprove");
        var authorizedFinalDecision = await FinalDecision(finalApprover, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, authorizedFinalDecision.StatusCode);
        Assert.Equal("loanApplications.notFound", (await Read(authorizedFinalDecision)).GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await finalApprover.GetAsync("/api/v1/loans")).StatusCode);
    }

    private async Task<HttpClient> PermissionClient(HttpClient administrator, string permissionKey)
    {
        var suffix = Guid.NewGuid().ToString("N");
        Guid roleId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var permission = await database.Permissions.SingleAsync(value => value.Key == permissionKey);
            var role = new Role { Id = Guid.NewGuid(), Name = $"TASK-11 {permissionKey} {suffix}" };
            database.Roles.Add(role);
            database.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await database.SaveChangesAsync();
            roleId = role.Id;
        }

        const string password = "Disposable_task11_permission_password_123!";
        var username = $"task11-{suffix}";
        var createdResponse = await administrator.PostAsJsonAsync("/api/v1/users", new { username, displayName = username, password });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await Read(createdResponse);
        var assign = await Send(
            administrator,
            HttpMethod.Put,
            $"/api/v1/users/{created.GetProperty("userId").GetGuid()}/roles",
            created.GetProperty("eTag").GetString()!,
            new { roleIds = new[] { roleId } });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        return await Login(username, password);
    }

    private async Task<HttpClient> Login(string username, string password)
    {
        var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> FinalDecision(HttpClient client, Guid applicationId) =>
        Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{applicationId}/final-decision", Convert.ToBase64String([1]), new { decision = "approve" });

    private static async Task<JsonElement> Read(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\"");
        return await client.SendAsync(request);
    }
}
