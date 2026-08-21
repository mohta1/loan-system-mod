using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    }

    private static async Task<IResult> Search(string? civilNumber, string? employeeNumber, string? name, string? organization, BorrowerStatus? status, int pageNumber, int pageSize, BorrowerService service, CancellationToken ct) =>
        Results.Ok(await service.SearchAsync(new(civilNumber, employeeNumber, name, organization, status, pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 25 : pageSize), ct));

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
