using LoanSystem.Modules.Documents.Domain;
using Microsoft.Extensions.Options;
namespace LoanSystem.Modules.Documents.Application;

public sealed class FileStorageOptions { public string RootPath { get; set; } = "data/files"; public long MaximumFileSizeBytes { get; set; } = 5 * 1024 * 1024; public string[] AllowedContentTypes { get; set; } = ["application/pdf", "image/png", "image/jpeg", "text/plain"]; }
public interface IFileStorage { Task StoreAsync(string key, Stream content, CancellationToken ct); Task<Stream?> OpenReadAsync(string key, CancellationToken ct); Task DeleteAsync(string key, CancellationToken ct); }
public interface IDocumentRepository { Task AddAsync(Document document, CancellationToken ct); Task<Document?> FindAsync(Guid id, CancellationToken ct); void Remove(Document document); Task SaveAsync(CancellationToken ct); }
public interface IDocumentAccessAuthorizer { bool CanAccess(Guid uploaderId, Guid currentUserId); }
public sealed class UploaderDocumentAccessAuthorizer : IDocumentAccessAuthorizer { public bool CanAccess(Guid uploaderId, Guid currentUserId) => uploaderId == currentUserId; }
public sealed record DocumentDto(Guid DocumentId, string FileName, string ContentType, long Size, DateTimeOffset UploadedAt);
public sealed class DocumentValidationException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class DocumentService(IDocumentRepository repository, IFileStorage storage, IOptions<FileStorageOptions> options, IDocumentAccessAuthorizer authorizer)
{
    public async Task<DocumentDto> UploadAsync(string fileName, string contentType, long length, Stream content, Guid userId, CancellationToken ct)
    {
        var config = options.Value;
        if (length <= 0 || length > config.MaximumFileSizeBytes) throw new DocumentValidationException("documents.invalidSize");
        if (!config.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)) throw new DocumentValidationException("documents.unsupportedContentType");
        var safeName = Path.GetFileName(fileName);
        if (Path.IsPathRooted(fileName) || string.IsNullOrWhiteSpace(safeName) || safeName != fileName.Replace('\\', '/').Split('/').Last() || fileName.Contains("..", StringComparison.Ordinal) || fileName.IndexOfAny(['\r', '\n']) >= 0) throw new DocumentValidationException("documents.unsafeFileName");
        var key = Guid.NewGuid().ToString("N"); var document = Document.Create(safeName, contentType, length, key, userId);
        await storage.StoreAsync(key, content, ct);
        try { await repository.AddAsync(document, ct); await repository.SaveAsync(ct); }
        catch { await storage.DeleteAsync(key, CancellationToken.None); throw; }
        return Map(document);
    }
    public async Task<Document?> AuthorizedAsync(Guid id, Guid userId, CancellationToken ct) { var d = await repository.FindAsync(id, ct); return d is not null && authorizer.CanAccess(d.UploaderId, userId) ? d : null; }
    public async Task<Stream?> ContentAsync(Document document, CancellationToken ct) => await storage.OpenReadAsync(document.StorageKey, ct);
    public async Task DeleteAsync(Document document, CancellationToken ct) { await storage.DeleteAsync(document.StorageKey, ct); repository.Remove(document); await repository.SaveAsync(ct); }
    public static DocumentDto Map(Document d) => new(d.Id, d.FileName, d.ContentType, d.Size, d.UploadedAt);
}
