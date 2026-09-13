using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
namespace LoanSystem.Modules.LoanOrigination.Presentation;

internal static class LoanApplicationEndpoints
{
    public static void Map(IEndpointRouteBuilder e) { var g = e.MapGroup("/api/v1/loan-applications").WithTags("Loan Applications"); g.MapGet("/", Search).RequireAuthorization(LoanApplicationPermissions.Read); g.MapGet("/{id:guid}", Get).RequireAuthorization(LoanApplicationPermissions.Read); g.MapPost("/", Create).RequireAuthorization(LoanApplicationPermissions.Create); g.MapPut("/{id:guid}", Edit).RequireAuthorization(LoanApplicationPermissions.Update); g.MapPost("/{id:guid}/evaluate-eligibility", Evaluate).RequireAuthorization(LoanApplicationPermissions.EvaluateEligibility); g.MapPost("/{id:guid}/submit", Submit).RequireAuthorization(LoanApplicationPermissions.Submit); g.MapPost("/{id:guid}/unit-decision", UnitDecision).RequireAuthorization(LoanApplicationPermissions.UnitApprove); g.MapPost("/{id:guid}/committee-decision", CommitteeDecision).RequireAuthorization(LoanApplicationPermissions.CommitteeApprove); g.MapPost("/{id:guid}/final-decision", FinalDecision).RequireAuthorization(LoanApplicationPermissions.FinalApprove); }
    static async Task<IResult> Search(Guid? loanApplicationId, Guid? borrowerId, Guid? loanProductId, LoanApplicationStatus? status, int? pageNumber, int? pageSize, LoanApplicationService s, CancellationToken ct)
    {
        var requestedPageNumber = pageNumber ?? 1;
        var requestedPageSize = pageSize ?? 25;
        if (requestedPageNumber < 1 || requestedPageSize is < 1 or > 100)
            return Problem(400, "Pagination is invalid", "loanApplications.invalidPagination");
        return Results.Ok(await s.SearchAsync(new(loanApplicationId, borrowerId, loanProductId, status, requestedPageNumber, requestedPageSize), ct));
    }
    static async Task<IResult> Get(Guid id, LoanApplicationService s, CancellationToken ct) { var x = await s.GetAsync(id, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); }
    static async Task<IResult> Create(CreateLoanApplication input, LoanApplicationService s, CancellationToken ct) { try { var x = await s.CreateAsync(input, ct); return Header(Results.Created($"/api/v1/loan-applications/{x.LoanApplicationId}", x), x.ETag); } catch (BorrowerUnavailableException x) { return Problem(422, "Borrower is unavailable", x.Code); } catch (ProductUnavailableException x) { return Problem(422, "Product version is unavailable", x.Code); } catch (LoanApplicationValidationException x) { return Problem(400, "Loan application is invalid", x.Field == "financingType" ? "loanApplications.invalidFinancingType" : "loanApplications.invalidRequestedAmount"); } }
    static async Task<IResult> Edit(Guid id, EditLoanApplication input, HttpRequest request, LoanApplicationService s, CancellationToken ct) { if (!TryVersion(request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired"); try { var x = await s.EditAsync(id, input, version, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); } catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); } catch (LoanApplicationStateException) { return Problem(409, "Only Draft applications are editable", "loanApplications.notDraft"); } catch (LoanApplicationValidationException x) { return Problem(400, "Loan application is invalid", x.Field == "financingType" ? "loanApplications.invalidFinancingType" : "loanApplications.invalidRequestedAmount"); } }
    static async Task<IResult> Evaluate(Guid id, HttpRequest request, LoanApplicationService s, CancellationToken ct) { if (!TryVersion(request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired"); try { var x = await s.EvaluateEligibilityAsync(id, version, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); } catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); } catch (LoanApplicationStateException) { return Problem(409, "Only Draft applications can be evaluated", "loanApplications.notDraft"); } }
    static async Task<IResult> Submit(Guid id, HttpRequest request, LoanApplicationService s, CancellationToken ct) { if (!TryVersion(request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired"); try { var x = await s.SubmitAsync(id, version, ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); } catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); } catch (LoanApplicationStateException) { return Problem(409, "Only Draft applications can be submitted", "loanApplications.notDraft"); } catch (EligibilityRequiredException) { return Problem(409, "Eligibility evaluation is required", "loanApplications.eligibilityRequired"); } catch (LoanApplicationIneligibleException) { return Problem(422, "The application is ineligible", "loanApplications.ineligible"); } catch (ProductUnavailableException) { return Problem(422, "Product version is unavailable", "loanApplications.productVersionUnavailable"); } }
    sealed record UnitDecisionRequest(string? Decision, string? Comment);
    static async Task<IResult> UnitDecision(Guid id, UnitDecisionRequest input, HttpContext context, LoanApplicationService s, CancellationToken ct)
    {
        if (!TryVersion(context.Request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired");
        var decision = input.Decision?.Trim().ToLowerInvariant() switch { "approve" => LoanSystem.Modules.LoanOrigination.Domain.UnitDecision.Approved, "reject" => LoanSystem.Modules.LoanOrigination.Domain.UnitDecision.Rejected, _ => (LoanSystem.Modules.LoanOrigination.Domain.UnitDecision?)null };
        if (decision is null) return Problem(400, "Unit decision is invalid", "loanApplications.invalidUnitDecision");
        if (decision == LoanSystem.Modules.LoanOrigination.Domain.UnitDecision.Rejected && string.IsNullOrWhiteSpace(input.Comment)) return Problem(400, "Rejection reason is required", "loanApplications.rejectionReasonRequired");
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId) || actorUserId == Guid.Empty) return Results.Unauthorized();
        try { var x = await s.DecideByUnitAsync(new(id, actorUserId, decision.Value, input.Comment, version), ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); }
        catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); }
        catch (LoanApplicationStateException) { return Problem(409, "Only Submitted applications can receive a Unit decision", "loanApplications.notSubmitted"); }
        catch (RejectionReasonRequiredException) { return Problem(400, "Rejection reason is required", "loanApplications.rejectionReasonRequired"); }
    }
    sealed record CommitteeDecisionRequest(string? Decision, string? Comment);
    static async Task<IResult> CommitteeDecision(Guid id, CommitteeDecisionRequest input, HttpContext context, LoanApplicationService s, CancellationToken ct)
    {
        if (!TryVersion(context.Request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired");
        var decision = input.Decision?.Trim().ToLowerInvariant() switch { "approve" => LoanSystem.Modules.LoanOrigination.Domain.CommitteeDecision.Approved, "reject" => LoanSystem.Modules.LoanOrigination.Domain.CommitteeDecision.Rejected, _ => (LoanSystem.Modules.LoanOrigination.Domain.CommitteeDecision?)null };
        if (decision is null) return Problem(400, "Committee decision is invalid", "loanApplications.invalidCommitteeDecision");
        if (decision == LoanSystem.Modules.LoanOrigination.Domain.CommitteeDecision.Rejected && string.IsNullOrWhiteSpace(input.Comment)) return Problem(400, "Rejection reason is required", "loanApplications.rejectionReasonRequired");
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId) || actorUserId == Guid.Empty) return Results.Unauthorized();
        try { var x = await s.DecideByCommitteeAsync(new(id, actorUserId, decision.Value, input.Comment, version), ct); return x is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(x), x.ETag); }
        catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); }
        catch (LoanApplicationStateException) { return Problem(409, "Only Unit Approved applications can receive a Committee decision", "loanApplications.notUnitApproved"); }
        catch (RejectionReasonRequiredException) { return Problem(400, "Rejection reason is required", "loanApplications.rejectionReasonRequired"); }
    }
    sealed record FinalDecisionRequest(string? Decision);
    static async Task<IResult> FinalDecision(Guid id, FinalDecisionRequest input, HttpContext context, LoanApplicationService service, CancellationToken ct)
    {
        if (!TryVersion(context.Request, out var version)) return Problem(428, "If-Match is required", "loanApplications.preconditionRequired");
        if (!string.Equals(input.Decision?.Trim(), "approve", StringComparison.OrdinalIgnoreCase)) return Problem(400, "Final decision is invalid", "loanApplications.invalidFinalDecision");
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor == Guid.Empty) return Results.Unauthorized();
        try { var result = await service.FinalApproveAsync(id, actor, context.TraceIdentifier, version, ct); return result is null ? Problem(404, "Loan application not found", "loanApplications.notFound") : Header(Results.Ok(result), result.ETag); }
        catch (LoanApplicationConcurrencyException) { return Problem(412, "The loan application changed", "loanApplications.concurrencyConflict"); }
        catch (FinalApprovalPrerequisitesException) { return Problem(409, "Application is not ready for final approval", "loanApplications.notReadyForFinalApproval"); }
        catch (InvalidApprovedAmountException) { return Problem(422, "Approved amount is invalid", "loanApplications.invalidApprovedAmount"); }
    }
    static bool TryVersion(HttpRequest r, out byte[] v) { v = []; var raw = r.Headers.IfMatch.ToString().Trim('"'); try { v = Convert.FromBase64String(raw); return v.Length > 0; } catch (FormatException) { return false; } }
    static EtagResult Header(IResult result, string etag) => new EtagResult(result, etag);
    static IResult Problem(int status, string title, string code) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { { "errorCode", code } });
    sealed class EtagResult(IResult inner, string etag) : IResult { public async Task ExecuteAsync(HttpContext c) { c.Response.Headers.ETag = $"\"{etag}\""; await inner.ExecuteAsync(c); } }
}
