using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LoanSystem.Modules.Borrowers.Presentation;

internal static class BorrowerEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/borrowers").WithTags("Borrowers");
        group.MapGet("/", Search).RequireAuthorization(BorrowerPermissions.Read);
        group.MapGet("/{id:guid}", Get).RequireAuthorization(BorrowerPermissions.Read);
        group.MapPost("/", Create).RequireAuthorization(BorrowerPermissions.Create);
        group.MapPut("/{id:guid}", Update).RequireAuthorization(BorrowerPermissions.Update);
        group.MapPost("/{id:guid}/activate", (Guid id, HttpRequest request, BorrowerService service, CancellationToken cancellationToken) => Status(id, true, request, service, cancellationToken)).RequireAuthorization(BorrowerPermissions.ManageStatus);
        group.MapPost("/{id:guid}/deactivate", (Guid id, HttpRequest request, BorrowerService service, CancellationToken cancellationToken) => Status(id, false, request, service, cancellationToken)).RequireAuthorization(BorrowerPermissions.ManageStatus);
        var imports = endpoints.MapGroup("/api/v1/borrower-imports").WithTags("Borrower Imports");
        imports.MapPost("/validate", ValidateImport).DisableAntiforgery().RequireAuthorization(BorrowerImportPermissions.Import);
        imports.MapPost("/{batchId:guid}/execute", ExecuteImport).RequireAuthorization(BorrowerImportPermissions.Import);
        imports.MapGet("/{batchId:guid}", GetImport).RequireAuthorization(BorrowerImportPermissions.Import);
    }

    private static async Task<IResult> ValidateImport(HttpRequest request, ClaimsPrincipal principal, BorrowerImportService service, IOptions<BorrowerImportOptions> options, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Problem(400, "Multipart form data is required", "borrowerImports.invalidFile");
        try { var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("file"); if (file is null) return Problem(400, "A workbook is required", "borrowerImports.invalidFile"); await using var stream = file.OpenReadStream(); var result = await service.ValidateAsync(file.FileName, file.ContentType, file.Length, stream, Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), options.Value, ct); return Results.Created($"/api/v1/borrower-imports/{result.BatchId}", result); }
        catch (BorrowerImportException exception) { return Problem(400, "The borrower workbook is invalid", exception.Code); }
        catch (InvalidDataException) { return Problem(400, "The borrower workbook is too large", "borrowerImports.invalidFile"); }
    }
    private static async Task<IResult> GetImport(Guid batchId, BorrowerImportService service, CancellationToken ct) { var result = await service.GetAsync(batchId, ct); return result is null ? Problem(404, "Import batch not found", "borrowerImports.batchNotFound") : Results.Ok(result); }
    private static async Task<IResult> ExecuteImport(Guid batchId, BorrowerImportService service, CancellationToken ct)
    {
        try { var result = await service.ExecuteAsync(batchId, ct); return result is null ? Problem(404, "Import batch not found", "borrowerImports.batchNotFound") : Results.Ok(result); }
        catch (BorrowerImportExecutionBusyException) { return Problem(409, "Import batch execution is already in progress", "borrowerImports.executionBusy"); }
        catch (BorrowerImportException exception) { return Problem(409, "Import batch cannot be executed", exception.Code); }
    }

    private static async Task<IResult> Search(string? civilNumber, string? employeeNumber, string? name, string? organization, BorrowerStatus? status, int? pageNumber, int? pageSize, BorrowerService service, CancellationToken ct)
    {
        var requestedPageNumber = pageNumber ?? 1;
        var requestedPageSize = pageSize ?? 25;
        if (requestedPageNumber < 1 || requestedPageSize < 1 || requestedPageSize > 100)
            return Problem(400, "Pagination values are invalid", "borrowers.invalidPagination");
        return Results.Ok(await service.SearchAsync(new(civilNumber, employeeNumber, name, organization, status, requestedPageNumber, requestedPageSize), ct));
    }

    private static async Task<IResult> Get(Guid id, BorrowerService service, CancellationToken ct)
    {
        var borrower = await service.GetAsync(id, ct);
        return borrower is null ? Problem(404, "Borrower not found", "borrowers.notFound") : Ok(borrower);
    }

    private static async Task<IResult> Create(BorrowerInput input, BorrowerService service, CancellationToken ct)
    {
        try
        {
            var borrower = await service.RegisterAsync(input, ct);
            return Header(Results.Created($"/api/v1/borrowers/{borrower.BorrowerId}", borrower), borrower.ETag);
        }
        catch (BorrowerValidationException)
        {
            return Problem(422, "Borrower data is invalid", "borrowers.validation");
        }
        catch (BorrowerConflictException exception)
        {
            return Problem(409, "Borrower identifier already exists", exception.Code);
        }
    }

    private static async Task<IResult> Update(Guid id, BorrowerInput input, HttpRequest request, BorrowerService service, CancellationToken ct)
    {
        if (!Version(request, out var version)) return Stale();
        try
        {
            var borrower = await service.UpdateAsync(id, input, version, ct);
            return borrower is null ? Problem(404, "Borrower not found", "borrowers.notFound") : Ok(borrower);
        }
        catch (BorrowerValidationException)
        {
            return Problem(422, "Borrower data is invalid", "borrowers.validation");
        }
        catch (BorrowerConflictException exception)
        {
            return Problem(409, "Borrower identifier already exists", exception.Code);
        }
        catch (BorrowerConcurrencyException)
        {
            return Stale();
        }
    }

    private static async Task<IResult> Status(Guid id, bool active, HttpRequest request, BorrowerService service, CancellationToken ct)
    {
        if (!Version(request, out var version)) return Stale();
        try
        {
            var borrower = await service.StatusAsync(id, active, version, ct);
            return borrower is null ? Problem(404, "Borrower not found", "borrowers.notFound") : Ok(borrower);
        }
        catch (BorrowerConcurrencyException)
        {
            return Stale();
        }
    }

    private static bool Version(HttpRequest request, out byte[] version)
    {
        version = [];
        try
        {
            if (!request.Headers.TryGetValue("If-Match", out var header)) return false;
            version = Convert.FromBase64String(header.ToString().Trim('"'));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HeaderResult Ok(BorrowerDto borrower) => Header(Results.Ok(borrower), borrower.ETag);
    private static HeaderResult Header(IResult result, string etag) => new(result, etag);
    private static IResult Stale() => Problem(412, "Borrower was changed by another request", "borrowers.concurrencyConflict");
    private static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { { "errorCode", code } });

    private sealed class HeaderResult(IResult inner, string etag) : IResult
    {
        public Task ExecuteAsync(HttpContext context)
        {
            context.Response.Headers.ETag = $"\"{etag}\"";
            return inner.ExecuteAsync(context);
        }
    }
}
