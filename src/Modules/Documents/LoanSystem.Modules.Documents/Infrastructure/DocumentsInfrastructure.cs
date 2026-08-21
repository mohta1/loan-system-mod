using LoanSystem.Modules.Documents.Application;
using LoanSystem.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace LoanSystem.Modules.Documents.Infrastructure;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options), IDocumentRepository
{
    public DbSet<Document> Documents => Set<Document>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) { var e = modelBuilder.Entity<Document>(); e.ToTable("documents", "documents"); e.HasKey(x => x.Id); e.Property(x => x.FileName).HasMaxLength(255).IsRequired(); e.Property(x => x.ContentType).HasMaxLength(127).IsRequired(); e.Property(x => x.StorageKey).HasMaxLength(64).IsRequired(); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired(); e.HasIndex(x => x.StorageKey).IsUnique(); }
    public Task AddAsync(Document document, CancellationToken ct) => Documents.AddAsync(document, ct).AsTask();
    public Task<Document?> FindAsync(Guid id, CancellationToken ct) => Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Remove(Document document) => Documents.Remove(document);
    public Task SaveAsync(CancellationToken ct) => base.SaveChangesAsync(ct);
}
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string root;
    public LocalFileStorage(IOptions<FileStorageOptions> options) { root = Path.GetFullPath(options.Value.RootPath); Directory.CreateDirectory(root); }
    private string Resolve(string key) { if (!Guid.TryParseExact(key, "N", out _)) throw new InvalidOperationException("Invalid storage key."); var path = Path.GetFullPath(Path.Combine(root, key)); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Storage-root escape."); return path; }
    public async Task StoreAsync(string key, Stream content, CancellationToken ct) { var path = Resolve(key); await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true); await content.CopyToAsync(output, ct); }
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) { var path = Resolve(key); Stream? result = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true) : null; return Task.FromResult(result); }
    public Task DeleteAsync(string key, CancellationToken ct) { var path = Resolve(key); if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }
}
