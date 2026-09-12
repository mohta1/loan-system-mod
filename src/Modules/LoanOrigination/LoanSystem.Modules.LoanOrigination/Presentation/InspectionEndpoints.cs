using System.Security.Claims;
using LoanSystem.Modules.LoanOrigination.Application;
using LoanSystem.Modules.LoanOrigination.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace LoanSystem.Modules.LoanOrigination.Presentation;

internal static class InspectionEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var apps = endpoints.MapGroup("/api/v1/loan-applications").WithTags("Loan Application Prerequisites");
        apps.MapPost("/{applicationId:guid}/inspections", Create).RequireAuthorization(InspectionPermissions.Create);
        apps.MapGet("/{applicationId:guid}/inspections", List).RequireAuthorization(LoanApplicationPermissions.Read);
        apps.MapPost("/{applicationId:guid}/documents", AttachDocument).RequireAuthorization(LoanApplicationPermissions.MortgageManage);
        apps.MapDelete("/{applicationId:guid}/documents/{documentId:guid}", DetachDocument).RequireAuthorization(LoanApplicationPermissions.MortgageManage);
        apps.MapPost("/{applicationId:guid}/mortgage/completed", MortgageCompleted).RequireAuthorization(LoanApplicationPermissions.MortgageManage);
        apps.MapPost("/{applicationId:guid}/mortgage/not-required", MortgageNotRequired).RequireAuthorization(LoanApplicationPermissions.MortgageManage);
        var inspections = endpoints.MapGroup("/api/v1/inspections").WithTags("Property Inspections");
        inspections.MapGet("/{id:guid}", Get).RequireAuthorization(LoanApplicationPermissions.Read);
        inspections.MapPut("/{id:guid}", Edit).RequireAuthorization(InspectionPermissions.Create);
        inspections.MapPost("/{id:guid}/complete", Complete).RequireAuthorization(InspectionPermissions.Create);
        inspections.MapPost("/{id:guid}/decision", Decision).RequireAuthorization(InspectionPermissions.Approve);
    }
    private sealed record AttachRequest(Guid DocumentId, string? DocumentType);
    private sealed record MortgageRequest(string? Reason, string? Comment);
    private sealed record DecisionRequest(string? Decision, string? Reason);
    private static async Task<IResult> Create(Guid applicationId, HttpContext c, PropertyInspectionService s, CancellationToken ct) => await Execute(async () => { if (!Version(c.Request, out var v)) return Error(428, "loanApplications.preconditionRequired", "If-Match is required"); var actor = Actor(c); var x = await s.CreateAsync(applicationId, actor, v, ct); return x is null ? Error(404, "loanApplications.notFound", "Loan application not found") : WithEtag(Results.Created($"/api/v1/inspections/{x.PropertyInspectionId}", x), x.ETag); });
    private static async Task<IResult> List(Guid applicationId, PropertyInspectionService s, CancellationToken ct) => Results.Ok(await s.ListAsync(applicationId, ct));
    private static async Task<IResult> Get(Guid id, PropertyInspectionService s, CancellationToken ct) { var x = await s.GetAsync(id, ct); return x is null ? Error(404, "inspections.notFound", "Inspection not found") : WithEtag(Results.Ok(x), x.ETag); }
    private static async Task<IResult> Edit(Guid id, EditInspection input, HttpContext c, PropertyInspectionService s, CancellationToken ct) => await InspectionMutation(c, v => s.EditAsync(id, input, v, ct));
    private static async Task<IResult> Complete(Guid id, HttpContext c, PropertyInspectionService s, CancellationToken ct) => await InspectionMutation(c, v => s.CompleteAsync(id, Actor(c), v, ct));
    private static async Task<IResult> Decision(Guid id, DecisionRequest input, HttpContext c, PropertyInspectionService s, CancellationToken ct) => await Execute(async () => { if (!Version(c.Request, out var v)) return Error(428, "inspections.preconditionRequired", "If-Match is required"); var approve = input.Decision?.Trim().ToLowerInvariant() switch { "approve" => true, "reject" => false, _ => throw new InvalidInspectionDecisionException() }; var x = await s.DecideAsync(id, Actor(c), approve, input.Reason, v, ct); return x is null ? Error(404, "inspections.notFound", "Inspection not found") : WithEtag(Results.Ok(x), x.ETag); });
    private static async Task<IResult> InspectionMutation(HttpContext c, Func<byte[], Task<PropertyInspectionDto?>> action) => await Execute(async () => { if (!Version(c.Request, out var v)) return Error(428, "inspections.preconditionRequired", "If-Match is required"); var x = await action(v); return x is null ? Error(404, "inspections.notFound", "Inspection not found") : WithEtag(Results.Ok(x), x.ETag); });
    private static async Task<IResult> AttachDocument(Guid applicationId, AttachRequest input, HttpContext c, LoanApplicationService s, CancellationToken ct) => await AppMutation(c, v => { if (!Enum.TryParse<ApplicationDocumentType>(input.DocumentType, true, out var type)) throw new InvalidDocumentTypeException(); return s.AttachDocumentAsync(applicationId, input.DocumentId, type, Actor(c), v, ct); });
    private static async Task<IResult> DetachDocument(Guid applicationId, Guid documentId, HttpContext c, LoanApplicationService s, CancellationToken ct) => await AppMutation(c, v => s.DetachDocumentAsync(applicationId, documentId, v, ct));
    private static async Task<IResult> MortgageCompleted(Guid applicationId, MortgageRequest input, HttpContext c, LoanApplicationService s, CancellationToken ct) => await AppMutation(c, v => s.CompleteMortgageAsync(applicationId, Actor(c), input.Comment, v, ct));
    private static async Task<IResult> MortgageNotRequired(Guid applicationId, MortgageRequest input, HttpContext c, LoanApplicationService s, CancellationToken ct) => await AppMutation(c, v => s.WaiveMortgageAsync(applicationId, Actor(c), input.Reason, input.Comment, v, ct));
    private static async Task<IResult> AppMutation(HttpContext c, Func<byte[], Task<LoanApplicationDto?>> action) => await Execute(async () => { if (!Version(c.Request, out var v)) return Error(428, "loanApplications.preconditionRequired", "If-Match is required"); var x = await action(v); return x is null ? Error(404, "loanApplications.notFound", "Loan application not found") : WithEtag(Results.Ok(x), x.ETag); });
    private static async Task<IResult> Execute(Func<Task<IResult>> action) { try { return await action(); } catch (LoanApplicationConcurrencyException) { return Error(412, "loanApplications.concurrencyConflict", "Resource changed"); } catch (InspectionConcurrencyException) { return Error(412, "inspections.concurrencyConflict", "Inspection changed"); } catch (LoanApplicationStateException) { return Error(409, "loanApplications.notInPrerequisiteStage", "Invalid prerequisite stage"); } catch (NotInPrerequisiteStageException) { return Error(409, "loanApplications.notInPrerequisiteStage", "Invalid prerequisite stage"); } catch (InspectionNotApprovedException) { return Error(409, "loanApplications.inspectionNotApproved", "Inspection is not approved"); } catch (DocumentUnavailableException) { return Error(422, "loanApplications.documentUnavailable", "Document is unavailable"); } catch (DocumentAlreadyAttachedException) { return Error(409, "loanApplications.documentAlreadyAttached", "Document already attached"); } catch (DocumentNotAttachedException) { return Error(404, "loanApplications.documentNotAttached", "Document is not attached"); } catch (InvalidDocumentTypeException) { return Error(400, "loanApplications.invalidDocumentType", "Document type is invalid"); } catch (MortgageReasonRequiredException) { return Error(422, "loanApplications.mortgageReasonRequired", "Waiver reason is required"); } catch (ReinspectionNotSupportedException) { return Error(409, "inspections.reinspectionNotSupported", "Reinspection is not supported"); } catch (InspectionValidationException x) { return Error(422, $"inspections.{x.Field}Required", "Inspection is incomplete"); } catch (InspectionFinalizedException) { return Error(409, "inspections.finalized", "Inspection is finalized"); } catch (InspectionStateException) { return Error(409, "inspections.invalidState", "Inspection state is invalid"); } catch (InspectionRejectionReasonRequiredException) { return Error(422, "inspections.rejectionReasonRequired", "Rejection reason is required"); } catch (InvalidInspectionDecisionException) { return Error(400, "inspections.invalidDecision", "Decision is invalid"); } }
    private static Guid Actor(HttpContext c) => Guid.TryParse(c.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id : throw new InvalidActorException();
    private static bool Version(HttpRequest r, out byte[] value) { value = []; try { value = Convert.FromBase64String(r.Headers.IfMatch.ToString().Trim('"')); return value.Length > 0; } catch (FormatException) { return false; } }
    private static IResult Error(int status, string code, string title) => Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["errorCode"] = code });
    private static EtagResult WithEtag(IResult inner, string etag) => new EtagResult(inner, etag);
    private sealed class EtagResult(IResult inner, string etag) : IResult { public async Task ExecuteAsync(HttpContext c) { c.Response.Headers.ETag = $"\"{etag}\""; await inner.ExecuteAsync(c); } }
    private sealed class InvalidInspectionDecisionException : Exception;
}
