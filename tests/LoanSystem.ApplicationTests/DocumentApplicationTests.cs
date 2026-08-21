using LoanSystem.Modules.Documents.Application;
using LoanSystem.Modules.Documents.Domain;
using Microsoft.Extensions.Options;
namespace LoanSystem.ApplicationTests;

public sealed class DocumentApplicationTests
{
    [Theory][InlineData("../evil.pdf")][InlineData("..\\evil.pdf")][InlineData("/tmp/evil.pdf")][InlineData("a/../evil.pdf")] public async Task Rejects_path_traversal(string name) { var s = Service(); await Assert.ThrowsAsync<DocumentValidationException>(() => s.UploadAsync(name, "application/pdf", 1, new MemoryStream([1]), Guid.NewGuid(), default)); }
    [Fact] public async Task Rejects_size_and_type() { var s = Service(); await Assert.ThrowsAsync<DocumentValidationException>(() => s.UploadAsync("a.pdf", "application/pdf", 11, new MemoryStream(new byte[11]), Guid.NewGuid(), default)); await Assert.ThrowsAsync<DocumentValidationException>(() => s.UploadAsync("a.exe", "application/octet-stream", 1, new MemoryStream([1]), Guid.NewGuid(), default)); }
    [Fact] public async Task Compensates_storage_when_database_fails() { var storage = new FakeStorage(); var s = new DocumentService(new FakeRepo { Fail = true }, storage, Options.Create(new FileStorageOptions { MaximumFileSizeBytes = 10, AllowedContentTypes = ["text/plain"] }), new UploaderDocumentAccessAuthorizer()); await Assert.ThrowsAsync<InvalidOperationException>(() => s.UploadAsync("a.txt", "text/plain", 1, new MemoryStream([1]), Guid.NewGuid(), default)); Assert.True(storage.Deleted); }
    static DocumentService Service() => new(new FakeRepo(), new FakeStorage(), Options.Create(new FileStorageOptions { MaximumFileSizeBytes = 10, AllowedContentTypes = ["application/pdf"] }), new UploaderDocumentAccessAuthorizer());
    sealed class FakeStorage : IFileStorage { public bool Deleted; public Task StoreAsync(string k, Stream s, CancellationToken c) => Task.CompletedTask; public Task<Stream?> OpenReadAsync(string k, CancellationToken c) => Task.FromResult<Stream?>(null); public Task DeleteAsync(string k, CancellationToken c) { Deleted = true; return Task.CompletedTask; } }
    sealed class FakeRepo : IDocumentRepository { public bool Fail; public Task AddAsync(Document d, CancellationToken c) => Task.CompletedTask; public Task<Document?> FindAsync(Guid i, CancellationToken c) => Task.FromResult<Document?>(null); public void Remove(Document d) { } public Task SaveAsync(CancellationToken c) => Fail ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask; }
}
