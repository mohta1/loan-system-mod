using LoanSystem.Modules.LoanAccounts.Application;
using LoanSystem.Modules.LoanAccounts.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.LoanAccounts.Presentation;

internal static class LoanAccountEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/loans").WithTags("Loan Accounts").RequireAuthorization(LoanAccountPermissions.Read);
        group.MapGet("/", Search); group.MapGet("/{id:guid}", Get);
    }
    private static async Task<IResult> Search(Guid? loanId, Guid? sourceApplicationId, Guid? borrowerId, LoanAccountStatus? status, int? pageNumber, int? pageSize, LoanAccountService service, CancellationToken ct)
    {
        var page = pageNumber ?? 1; var size = pageSize ?? 25;
        if (page < 1 || size is < 1 or > 100) return Problem(400, "Pagination is invalid", "loans.invalidPagination");
        return Results.Ok(await service.SearchAsync(new(loanId, sourceApplicationId, borrowerId, status, page, size), ct));
    }
    private static async Task<IResult> Get(Guid id, LoanAccountService service, CancellationToken ct) { var loan = await service.GetAsync(id, ct); return loan is null ? Problem(404, "Loan account not found", "loans.notFound") : Results.Ok(loan); }
    private static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}
