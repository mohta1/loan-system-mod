using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LoanSystem.Modules.Documents.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class DocumentsIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Real_sql_upload_download_delete_and_authorization()
    {
        using var anonymous = fixture.Factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/documents/{Guid.NewGuid()}")).StatusCode); using var owner = fixture.Factory.CreateClient(new() { HandleCookies = true }); await Login(owner); using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("safe content")) { Headers = { ContentType = new("text/plain") } }, "file", "safe report.txt"); var upload = await owner.PostAsync("/api/v1/documents", form); Assert.Equal(HttpStatusCode.Created, upload.StatusCode); var json = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement; var id = json.GetProperty("documentId").GetGuid(); Assert.DoesNotContain("storageKey", json.ToString(), StringComparison.OrdinalIgnoreCase); Assert.Equal("safe content", await (await owner.GetAsync($"/api/v1/documents/{id}/content")).Content.ReadAsStringAsync());
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>(); var row = await db.Documents.SingleAsync(x => x.Id == id); Assert.Equal(12, row.Size); Assert.Empty(typeof(LoanSystem.Modules.Documents.Domain.Document).GetProperties().Where(x => x.PropertyType == typeof(byte[])));
        var created = await owner.PostAsJsonAsync("/api/v1/users", new { username = "document-other", displayName = "Other", password = "Disposable_user_password_123!" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode); using var other = fixture.Factory.CreateClient(new() { HandleCookies = true }); await Login(other, "document-other", "Disposable_user_password_123!"); Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/documents/{id}")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/documents/{id}")).StatusCode); Assert.Equal(LoanSystem.Modules.Documents.Domain.DocumentStatus.Deleted, row.Status);
    }

    [Fact] public async Task Delete_pending_metadata_is_unavailable_and_delete_retry_recovers() { using var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); await Login(client); using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent([1]) { Headers = { ContentType = new("text/plain") } }, "file", "retry.txt"); var uploaded = JsonDocument.Parse(await (await client.PostAsync("/api/v1/documents", form)).Content.ReadAsStringAsync()).RootElement; var id = uploaded.GetProperty("documentId").GetGuid(); using (var scope = fixture.Factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>(); var document = await db.Documents.SingleAsync(x => x.Id == id); document.BeginDelete(); await db.SaveChangesAsync(); } Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/{id}")).StatusCode); Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/documents/{id}")).StatusCode); using var verification = fixture.Factory.Services.CreateScope(); Assert.Equal(LoanSystem.Modules.Documents.Domain.DocumentStatus.Deleted, (await verification.ServiceProvider.GetRequiredService<DocumentsDbContext>().Documents.SingleAsync(x => x.Id == id)).Status); }
    [Fact] public async Task Invalid_uploads_are_rejected() { using var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); await Login(client); foreach (var item in new[] { ("../evil.pdf", "application/pdf"), ("evil.exe", "application/octet-stream") }) { using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent([1]) { Headers = { ContentType = new(item.Item2) } }, "file", item.Item1); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/documents", form)).StatusCode); } }
    static async Task Login(HttpClient c, string u = IdentityAccessIntegrationTests.Admin, string p = IdentityAccessIntegrationTests.Password) => Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/auth/login", new { username = u, password = p })).StatusCode);
}
