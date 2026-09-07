using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace LoanSystem.Modules.LoanOrigination.Presentation;

internal static class LoanApplicationEndpoints
{
    public static void Map(IEndpointRouteBuilder e) { var g = e.MapGroup("/api/v1/loan-applications").WithTags("Loan Applications"); g.MapGet("/", Search).RequireAuthorization(LoanApplicationPermissions.Read); g.MapGet("/{id:guid}", Get).RequireAuthorization(LoanApplicationPermissions.Read); g.MapPost("/", Create).RequireAuthorization(LoanApplicationPermissions.Create); g.MapPut("/{id:guid}", Edit).RequireAuthorization(LoanApplicationPermissions.Update); }
    static async Task<IResult> Search(Guid? loanApplicationId, Guid? borrowerId, Guid? loanProductId, LoanApplicationStatus? status, int pageNumber, int pageSize, LoanApplicationService s, CancellationToken ct) => Results.Ok(await s.SearchAsync(new(loanApplicationId, borrowerId, loanProductId, status, pageNumber, pageSize), ct));
    static async Task<IResult> Get(Guid id, LoanApplicationService s, CancellationToken ct) { var x = await s.GetAsync(id, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); }
    static async Task<IResult> Create(CreateLoanApplication input, LoanApplicationService s, CancellationToken ct) { try { var x = await s.CreateAsync(input, ct); return Header(Results.Created($"/api/v1/loan-applications/{x.LoanApplicationId}", x), x.ETag); } catch (BorrowerUnavailableException x) { return Problem(422, "Borrower is unavailable", x.Code); } catch (ProductUnavailableException x) { return Problem(422, "Product version is unavailable", x.Code); } catch (LoanApplicationValidationException x) { return Problem(400, "Loan application is invalid", x.Field == "financingType" ? "loanApplications.invalidFinancingType" : "loanApplications.invalidRequestedAmount"); } }
    static async Task<IResult> Edit(Guid id, EditLoanApplication input, HttpRequest request, LoanApplicationService s, CancellationToken ct) { if (!TryVersion(request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired"); try { var x = await s.EditAsync(id, input, version, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); } catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); } catch (LoanApplicationStateException) { return Problem(409, "Only Draft applications are editable", "loanApplications.notDraft"); } catch (LoanApplicationValidationException x) { return Problem(400, "Loan application is invalid", x.Field == "financingType" ? "loanApplications.invalidFinancingType" : "loanApplications.invalidRequestedAmount"); } }
    static bool TryVersion(HttpRequest r, out byte[] v) { v = []; var raw = r.Headers.IfMatch.ToString().Trim('"'); try { v = Convert.FromBase64String(raw); return v.Length > 0; } catch (FormatException) { return false; } }
    static EtagResult Header(IResult result, string etag) => new EtagResult(result, etag);
    static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { { "errorCode", code } });
    sealed class EtagResult(IResult inner, string etag) : IResult { public async Task ExecuteAsync(HttpContext c) { c.Response.Headers.ETag = $"\"{etag}\""; await inner.ExecuteAsync(c); } }
}
