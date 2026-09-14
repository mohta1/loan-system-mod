using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace LoanSystem.Modules.LoanAccounts.Presentation;

internal static class LoanAccountEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/loans").WithTags("Loan Accounts").RequireAuthorization(LoanAccountPermissions.Read);
        group.MapGet("/", Search); group.MapGet("/{id:guid}", Get);
        endpoints.MapPost("/api/v1/loans/{id:guid}/financing-type", ChangeFinancingType).WithTags("Loan Accounts").RequireAuthorization(LoanAccountPermissions.ChangeFinancingType);
    }
    private static async Task<IResult> Search(Guid? loanId, Guid? sourceApplicationId, Guid? borrowerId, LoanAccountStatus? status, int? pageNumber, int? pageSize, LoanAccountService service, CancellationToken ct)
    {
        var page = pageNumber ?? 1; var size = pageSize ?? 25;
        if (page < 1 || size is < 1 or > 100) return Problem(400, "Pagination is invalid", "loans.invalidPagination");
        return Results.Ok(await service.SearchAsync(new(loanId, sourceApplicationId, borrowerId, status, page, size), ct));
    }
    private static async Task<IResult> Get(Guid id, LoanAccountService service, HttpResponse response, CancellationToken ct) { var loan = await service.GetAsync(id, ct); if (loan is null) return Problem(404, "Loan account not found", "loans.notFound"); response.Headers.ETag = $"\"{loan.ETag}\""; return Results.Ok(loan); }
    private static async Task<IResult> ChangeFinancingType(Guid id, FinancingTypeRequest request, HttpContext context, LoanAccountService service, CancellationToken ct)
    {
        if (!TryVersion(context.Request.Headers.IfMatch.FirstOrDefault(), out var expected)) return Problem(400, "A valid If-Match header is required", "loans.ifMatchRequired");
        try { var actor = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!); var loan = await service.ChangeFinancingTypeAsync(id, request.FinancingType ?? "", actor, context.TraceIdentifier, expected, ct); if (loan is null) return Problem(404, "Loan account not found", "loans.notFound"); context.Response.Headers.ETag = $"\"{loan.ETag}\""; return Results.Ok(loan); }
        catch (UnsupportedFinancingTypeException) { return Problem(422, "The financing type is not allowed by the approved product version", "loans.financingTypeUnsupported"); }
        catch (FinancingTypeChangeNotAllowedException) { return Problem(409, "Financing type cannot be changed after disbursement begins", "loans.financingTypeLocked"); }
        catch (LoanAccountConcurrencyException) { return Problem(412, "The loan account changed", "loans.concurrency"); }
    }
    private static bool TryVersion(string? header, out byte[] value) { value = []; try { if (string.IsNullOrWhiteSpace(header)) return false; value = Convert.FromBase64String(header.Trim().Trim('"')); return value.Length > 0; } catch (FormatException) { return false; } }
    private static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}
public sealed record FinancingTypeRequest(string? FinancingType);
