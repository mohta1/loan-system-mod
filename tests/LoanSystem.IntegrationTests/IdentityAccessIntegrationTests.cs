using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Modules.IdentityAccess;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using LoanSystem.Modules.IdentityAccess.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using LoanSystem.Api.Infrastructure;
using Testcontainers.MsSql;

namespace LoanSystem.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdentitySqlTestGroup : ICollectionFixture<IdentitySqlFixture>
{
    public const string Name = "Identity SQL Server";
}

public sealed class IdentitySqlFixture : IAsyncLifetime
{
    private MsSqlContainer? sql;
    public IdentityApiFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        ConnectionString = sql.GetConnectionString();
        Factory = new IdentityApiFactory(ConnectionString, IdentityAccessIntegrationTests.Admin, IdentityAccessIntegrationTests.Password);
        using var client = Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (sql is not null) await sql.DisposeAsync();
    }
}

public sealed class IdentityApiFactory(string connectionString, string username, string password) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LoanSystem", connectionString);
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("DevelopmentAdmin:Username", username);
        builder.UseSetting("DevelopmentAdmin:Password", password);
        builder.UseSetting("DevelopmentAdmin:DisplayName", "Integration Administrator");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PlatformDbContext>();
            services.RemoveAll<DbContextOptions<PlatformDbContext>>();
            services.RemoveAll<IdentityAccessDbContext>();
            services.RemoveAll<DbContextOptions<IdentityAccessDbContext>>();
            services.AddDbContext<PlatformDbContext>(options => options.UseSqlServer(connectionString));
            services.AddDbContext<IdentityAccessDbContext>(options => options.UseSqlServer(connectionString));
        });
    }
}

[Collection(IdentitySqlTestGroup.Name)]
public sealed class IdentityAccessIntegrationTests(IdentitySqlFixture fixture)
{
    internal const string Admin = "integration-admin";
    internal const string Password = "Disposable_integration_password_123!";
    [Fact]
    public async Task Clean_migrations_and_seed_match_authorization_matrix()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString); await connection.OpenAsync();
        foreach (var table in new[] { "users", "roles", "permissions", "user_roles", "role_permissions" }) { using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='identity' AND t.name=@name", connection); command.Parameters.AddWithValue("@name", table); Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)); }
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>(); var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(fixture.ConnectionString, db.Database.GetConnectionString()); Assert.Equal(fixture.ConnectionString, platform.Database.GetConnectionString());
        Assert.Equal(11, await db.Roles.CountAsync()); Assert.Equal(40, await db.Permissions.CountAsync());
        var unit = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == "Unit Officer"); Assert.Contains(unit.RolePermissions, x => x.Permission.Key == "loanApplications.unitApprove"); Assert.DoesNotContain(unit.RolePermissions, x => x.Permission.Key == "identity.users.manage");
        var treasury = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == "Treasury Approver"); Assert.Contains(treasury.RolePermissions, x => x.Permission.Key == "treasury.execute"); Assert.DoesNotContain(treasury.RolePermissions, x => x.Permission.Key == "treasury.input");
        await fixture.Factory.Services.InitializeIdentityAccessAsync(new ConfigurationBuilder().Build(), false); Assert.Equal(11, await db.Roles.CountAsync());
        var stored = await db.Users.SingleAsync(x => x.NormalizedUsername == "INTEGRATION-ADMIN"); Assert.NotEqual(Password, stored.PasswordHash);
        db.Users.Add(new User(Guid.NewGuid(), " Integration-Admin ", "Duplicate", "not-plaintext", DateTimeOffset.UtcNow)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Login_me_logout_and_generic_failures_are_secure()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        foreach (var body in new object[] { new { username = Admin, password = "wrong-password" }, new { username = "unknown", password = "wrong-password" }, new { username = (string?)null, password = (string?)null } }) { var failure = await client.PostAsJsonAsync("/api/v1/auth/login", body); Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode); Assert.Equal("identity.invalidCredentials", JsonDocument.Parse(await failure.Content.ReadAsStringAsync()).RootElement.GetProperty("errorCode").GetString()); }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = Admin, password = Password })).StatusCode); var me = await client.GetAsync("/api/v1/auth/me"); Assert.Equal(HttpStatusCode.OK, me.StatusCode); var json = await me.Content.ReadAsStringAsync(); Assert.Contains("System Administrator", json); Assert.Contains("identity.users.manage", json); Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
    [Fact]
    public async Task User_permissions_deactivation_and_database_uniqueness_are_authoritative()
    {
        using var admin = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); await Login(admin); Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/users")).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { username = "limited-user", displayName = "Limited", password = "Disposable_user_password_123!" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode); var user = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement; var id = user.GetProperty("userId").GetGuid(); var etag = user.GetProperty("eTag").GetString()!;
        using var limited = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); await Login(limited, "limited-user", "Disposable_user_password_123!"); Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("/api/v1/users")).StatusCode);
        var duplicate = await admin.PostAsJsonAsync("/api/v1/users", new { username = " LIMITED-USER ", displayName = "Duplicate", password = "Disposable_user_password_123!" }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var roleCatalog = JsonDocument.Parse(await (await admin.GetAsync("/api/v1/roles")).Content.ReadAsStringAsync()).RootElement; var adminRole = roleCatalog.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "System Administrator").GetProperty("roleId").GetGuid(); var promoted = await Send(admin, HttpMethod.Put, $"/api/v1/users/{id}/roles", etag, new { roleIds = new[] { adminRole } }); Assert.Equal(HttpStatusCode.OK, promoted.StatusCode); etag = JsonDocument.Parse(await promoted.Content.ReadAsStringAsync()).RootElement.GetProperty("eTag").GetString()!; Assert.Equal(HttpStatusCode.OK, (await limited.GetAsync("/api/v1/users")).StatusCode); var demoted = await Send(admin, HttpMethod.Put, $"/api/v1/users/{id}/roles", etag, new { roleIds = Array.Empty<Guid>() }); Assert.Equal(HttpStatusCode.OK, demoted.StatusCode); etag = JsonDocument.Parse(await demoted.Content.ReadAsStringAsync()).RootElement.GetProperty("eTag").GetString()!; Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("/api/v1/users")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/users/{id}/deactivate"); request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); var disabled = await admin.SendAsync(request); Assert.Equal(HttpStatusCode.OK, disabled.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await limited.GetAsync("/api/v1/auth/me")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { username = "limited-user", password = "Disposable_user_password_123!" })).StatusCode);
    }
    [Fact]
    public async Task Multiple_roles_and_all_stale_mutations_return_precondition_failed()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); await Login(client); var created = await client.PostAsJsonAsync("/api/v1/users", new { username = "concurrency-user", displayName = "Concurrency", password = "Disposable_user_password_123!" }); var user = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement; var id = user.GetProperty("userId").GetGuid(); var stale = user.GetProperty("eTag").GetString()!;
        var roles = JsonDocument.Parse(await (await client.GetAsync("/api/v1/roles")).Content.ReadAsStringAsync()).RootElement.EnumerateArray().Take(2).Select(x => x.GetProperty("roleId").GetGuid()).ToArray(); var assigned = await Send(client, HttpMethod.Put, $"/api/v1/users/{id}/roles", stale, new { roleIds = roles }); Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); var current = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync()).RootElement.GetProperty("eTag").GetString()!; Assert.Equal(2, JsonDocument.Parse(await assigned.Content.ReadAsStringAsync()).RootElement.GetProperty("roleIds").GetArrayLength());
        foreach (var operation in new[] { (HttpMethod.Put, $"/api/v1/users/{id}", (object)new { displayName = "Stale" }), (HttpMethod.Put, $"/api/v1/users/{id}/roles", (object)new { roleIds = roles }), (HttpMethod.Post, $"/api/v1/users/{id}/activate", (object)new { }), (HttpMethod.Post, $"/api/v1/users/{id}/deactivate", (object)new { }) }) { var response = await Send(client, operation.Item1, operation.Item2, stale, operation.Item3); Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode); Assert.Contains("identity.concurrencyConflict", await response.Content.ReadAsStringAsync()); }
        Assert.NotEqual(stale, current);
    }
    private static async Task Login(HttpClient client, string username = Admin, string password = Password) => Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password })).StatusCode);
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body) { using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); return await client.SendAsync(request); }
}
