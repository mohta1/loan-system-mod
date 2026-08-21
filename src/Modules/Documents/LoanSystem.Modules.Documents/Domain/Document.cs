namespace LoanSystem.Modules.Documents.Domain;

public sealed class Document
{
    private Document() { }
    private Document(Guid id, string fileName, string contentType, long size, string storageKey, Guid uploaderId, DateTimeOffset uploadedAt)
    { Id = id; FileName = fileName; ContentType = contentType; Size = size; StorageKey = storageKey; UploaderId = uploaderId; UploadedAt = uploadedAt; }
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public string StorageKey { get; private set; } = "";
    public Guid UploaderId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public static Document Create(string fileName, string contentType, long size, string storageKey, Guid uploaderId)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || fileName.IndexOfAny(['\r', '\n']) >= 0) throw new ArgumentException("Invalid file name.");
        if (string.IsNullOrWhiteSpace(contentType) || size <= 0 || uploaderId == Guid.Empty) throw new ArgumentException("Invalid document metadata.");
        if (!Guid.TryParseExact(storageKey, "N", out _)) throw new ArgumentException("Invalid storage key.");
        return new(Guid.NewGuid(), fileName, contentType, size, storageKey, uploaderId, DateTimeOffset.UtcNow);
    }
}
