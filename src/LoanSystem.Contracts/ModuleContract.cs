namespace LoanSystem.Contracts;
/// <summary>Marker for behavior-free, cross-module public contracts.</summary>
public interface IModuleContract;

/// <summary>A narrow composition contract for retaining an import source without exposing storage details.</summary>
public interface IImportSourceDocumentStore
{
    Task<Guid> StoreAsync(string fileName, string contentType, long length, Stream content, Guid uploadedBy, CancellationToken cancellationToken);
    Task DiscardAsync(Guid documentId, Guid uploadedBy, CancellationToken cancellationToken);
}
