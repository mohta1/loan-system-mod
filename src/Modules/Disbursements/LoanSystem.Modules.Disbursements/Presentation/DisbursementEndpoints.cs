using System.Security.Claims;
using LoanSystem.Modules.Disbursements.Application;
using LoanSystem.Modules.Disbursements.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Disbursements.Presentation;

internal static class DisbursementEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/disbursements").WithTags("Disbursements");
        group.MapGet("/", Search).RequireAuthorization(DisbursementPermissions.Read); group.MapGet("/{id:guid}", Get).RequireAuthorization(DisbursementPermissions.Read); group.MapPost("/", Create).RequireAuthorization(DisbursementPermissions.Create);
    }
    private static async Task<IResult> Search(Guid? loanId, DisbursementStatus? status, int? pageNumber, int? pageSize, DisbursementService service, CancellationToken ct) { var page = pageNumber ?? 1; var size = pageSize ?? 25; if (page < 1 || size is < 1 or > 100) return Problem(400, "Pagination is invalid", "disbursements.invalidPagination"); return Results.Ok(await service.SearchAsync(new(loanId, status, page, size), ct)); }
    private static async Task<IResult> Get(Guid id, DisbursementService service, HttpResponse response, CancellationToken ct) { var value = await service.GetAsync(id, ct); if (value is null) return Problem(404, "Disbursement not found", "disbursements.notFound"); response.Headers.ETag = $"\"{value.ETag}\""; return Results.Ok(value); }
    private static async Task<IResult> Create(CreateDisbursementCommand command, HttpContext context, DisbursementService service, CancellationToken ct)
    {
        try
        {
            var actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!); var result = await service.CreateAsync(command, context.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? "", actor, context.TraceIdentifier, ct); var dto = DisbursementService.Map(result.Disbursement); context.Response.Headers.ETag = $"\"{dto.ETag}\"";
            if (result.Outcome == IdempotentCreateOutcome.Conflict) return Problem(409, "Idempotency key was reused with a different request", "idempotency.keyConflict");
            return result.Outcome == IdempotentCreateOutcome.Created ? Results.Created($"/api/v1/disbursements/{dto.DisbursementId}", dto) : Results.Ok(dto);
        }
        catch (MissingIdempotencyKeyException) { return Problem(400, "Idempotency-Key is required", "idempotency.keyRequired"); }
        catch (LoanNotFoundException) { return Problem(404, "Loan account not found", "disbursements.loanNotFound"); }
        catch (SupportingDocumentNotAccessibleException) { return Problem(422, "A supporting document is unavailable", "disbursements.documentUnavailable"); }
        catch (DisbursementValidationException) { return Problem(422, "The disbursement request is invalid", "disbursements.validation"); }
    }
    private static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}
