using System.Security.Claims;
using LoanSystem.Modules.Documents.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
namespace LoanSystem.Modules.Documents.Presentation;

internal static class DocumentEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents").WithTags("Documents").RequireAuthorization();
        group.MapPost("/", Upload).DisableAntiforgery(); group.MapGet("/{id:guid}", Metadata); group.MapGet("/{id:guid}/content", Content); group.MapDelete("/{id:guid}", Delete);
    }
    static Guid User(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    static async Task<IResult> Upload(HttpRequest request, ClaimsPrincipal principal, DocumentService service, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Problem(400, "Multipart form data is required", "documents.invalidRequest");
        try { var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("file"); if (file is null) return Problem(400, "A file is required", "documents.fileRequired"); await using var stream = file.OpenReadStream(); var dto = await service.UploadAsync(file.FileName, file.ContentType, file.Length, stream, User(principal), ct); return Results.Created($"/api/v1/documents/{dto.DocumentId}", dto); }
        catch (DocumentValidationException ex) { return Problem(400, "The document is invalid", ex.Code); }
        catch (InvalidDataException) { return Problem(400, "The document is too large", "documents.invalidSize"); }
    }
    static async Task<IResult> Metadata(Guid id, ClaimsPrincipal p, IDocumentRepository repo, IDocumentAccessAuthorizer auth, CancellationToken ct) { var d = await repo.FindAsync(id, ct); if (d is null) return Results.NotFound(); if (!auth.CanAccess(d.UploaderId, User(p))) return Problem(403, "Document access is denied", "documents.forbidden"); return Results.Ok(DocumentService.Map(d)); }
    static async Task<IResult> Content(Guid id, ClaimsPrincipal p, IDocumentRepository repo, IDocumentAccessAuthorizer auth, DocumentService service, CancellationToken ct) { var d = await repo.FindAsync(id, ct); if (d is null) return Results.NotFound(); if (!auth.CanAccess(d.UploaderId, User(p))) return Problem(403, "Document access is denied", "documents.forbidden"); var stream = await service.ContentAsync(d, ct); return stream is null ? Problem(404, "Document content is unavailable", "documents.contentMissing") : Results.File(stream, d.ContentType, SanitizeDownloadName(d.FileName), enableRangeProcessing: true); }
    static async Task<IResult> Delete(Guid id, ClaimsPrincipal p, IDocumentRepository repo, IDocumentAccessAuthorizer auth, DocumentService service, CancellationToken ct) { var d = await repo.FindAsync(id, ct); if (d is null) return Results.NotFound(); if (!auth.CanAccess(d.UploaderId, User(p))) return Problem(403, "Document access is denied", "documents.forbidden"); await service.DeleteAsync(d, ct); return Results.NoContent(); }
    internal static string SanitizeDownloadName(string name) => string.Concat(Path.GetFileName(name).Where(c => c != '\r' && c != '\n' && !char.IsControl(c)));
    static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { { "errorCode", code } });
}
