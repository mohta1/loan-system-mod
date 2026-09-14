using System.Text.Json.Serialization;
namespace LoanSystem.Modules.LoanOrigination.Domain;

public enum LoanApplicationStatus { Draft, Submitted, UnitApproved, CommitteeApproved, PrerequisitesPending, ReadyForFinalApproval, Approved, Rejected, Cancelled }
public enum InspectionPrerequisiteStatus { NotStarted, Pending, Approved, Rejected }
public enum MortgageStatus { NotStarted, Pending, Completed, NotRequired, Released }
public enum DocumentPrerequisiteStatus { NotStarted, Pending, Satisfied }
public enum LoanApplicationDocumentType { Ownership, Survey, EngineeringDrawing, Other }
public sealed record MortgageDecision(MortgageStatus Status, Guid ActorUserId, DateTimeOffset DecidedAtUtc, string? Reason, string? Comment);
public sealed class LoanApplicationDocumentReference
{
    private LoanApplicationDocumentReference() { }
    internal LoanApplicationDocumentReference(Guid applicationId, Guid documentId, LoanApplicationDocumentType type, DateTimeOffset at) { LoanApplicationId = applicationId; DocumentId = documentId; DocumentType = type; IsRequired = LoanDocumentPrerequisitePolicy.IsRequired(type); AttachedAtUtc = at; }
    public Guid LoanApplicationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public LoanApplicationDocumentType DocumentType { get; private set; }
    public bool IsRequired { get; private set; }
    public DateTimeOffset AttachedAtUtc { get; private set; }
}
public static class LoanDocumentPrerequisitePolicy
{
    public static IReadOnlySet<LoanApplicationDocumentType> RequiredTypes { get; } = new HashSet<LoanApplicationDocumentType> { LoanApplicationDocumentType.Ownership, LoanApplicationDocumentType.Survey, LoanApplicationDocumentType.EngineeringDrawing };
    public static bool IsRequired(LoanApplicationDocumentType type) => RequiredTypes.Contains(type);
    public static bool IsSatisfied(IEnumerable<LoanApplicationDocumentReference> documents) => RequiredTypes.All(type => documents.Any(x => x.DocumentType == type));
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnitDecision { Approved, Rejected }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommitteeDecision { Approved, Rejected }
public sealed record UnitApprovalDecision(UnitDecision Decision, Guid ActorUserId, DateTimeOffset DecidedAtUtc, string? Comment, string? RejectionReason);
public sealed record CommitteeApprovalDecision(CommitteeDecision Decision, Guid ActorUserId, DateTimeOffset DecidedAtUtc, string? Comment, string? RejectionReason);
public interface ILoanApplicationDomainEvent;
public sealed record BorrowerSnapshot(string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation, string Status);
public sealed record ProductRankGradeRuleSnapshot(string RankGrade, decimal MaximumAmount);
public sealed record ProductEligibilitySnapshot(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<ProductRankGradeRuleSnapshot> RankGradeAmountRules, int MaximumTermMonths, string DueDateRule);
public sealed record ProductSnapshot(Guid LoanProductId, Guid LoanProductVersionId, string ProductName, int VersionNumber, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, ProductEligibilitySnapshot EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ProductStatus, string VersionStatus, DateTimeOffset? PublishedAtUtc);
public sealed record EligibilityRuleResult(string Rule, bool Passed, string ReasonCode, string? ExpectedValue = null, string? ActualValue = null);
public sealed record EligibilityDecision(bool IsEligible, DateTimeOffset EvaluatedAtUtc, Guid AppliedLoanProductVersionId, int AppliedProductVersionNumber, decimal RequestedAmountAtEvaluation, string FinancingTypeAtEvaluation, decimal PermittedAmount, int ObservedConflictingApplicationCount, int MaximumApplicationCount, IReadOnlyList<EligibilityRuleResult> RuleResults);
public sealed record LoanApplicationSubmitted(Guid LoanApplicationId, Guid BorrowerId, Guid LoanProductVersionId, DateTimeOffset SubmittedAtUtc) : ILoanApplicationDomainEvent;
public sealed record UnitApprovalGranted(Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp) : ILoanApplicationDomainEvent;
public sealed record CommitteeApprovalGranted(Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp) : ILoanApplicationDomainEvent;
public sealed record LoanApplicationRejected(Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp, string Reason, string Stage = "Unit") : ILoanApplicationDomainEvent;
public sealed record LoanApplicationApproved(Guid EventId, Guid LoanApplicationId, Guid BorrowerId, Guid LoanProductId, Guid LoanProductVersionId, decimal ApprovedAmount, string Currency, string FinancingType, Guid ActorUserId, DateTimeOffset Timestamp) : ILoanApplicationDomainEvent;

public static class EligibilityReasonCodes
{
    public const string NationalitySatisfied = "eligibility.nationalitySatisfied", NationalityMismatch = "eligibility.nationalityMismatch", ApplicationCountSatisfied = "eligibility.applicationCountSatisfied", ApplicationCountExceeded = "eligibility.applicationCountExceeded", RankGradeSatisfied = "eligibility.rankGradeSatisfied", RankGradeNotConfigured = "eligibility.rankGradeNotConfigured", RequestedAmountSatisfied = "eligibility.requestedAmountSatisfied", RequestedAmountExceedsPermitted = "eligibility.requestedAmountExceedsPermitted";
}

public static class LoanApplicationEligibilityEvaluator
{
    public static EligibilityDecision Evaluate(LoanApplication application, int conflictingApplications, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentOutOfRangeException.ThrowIfNegative(conflictingApplications);
        var configuration = application.ProductSnapshot.EligibilityConfiguration;
        var rules = new List<EligibilityRuleResult>();
        var requiredNationality = configuration.RequiredNationality?.Trim() ?? "";
        var nationalityPassed = requiredNationality.Length == 0 || string.Equals(requiredNationality, application.BorrowerSnapshot.Nationality.Trim(), StringComparison.OrdinalIgnoreCase);
        rules.Add(new("nationality", nationalityPassed, nationalityPassed ? EligibilityReasonCodes.NationalitySatisfied : EligibilityReasonCodes.NationalityMismatch, requiredNationality, application.BorrowerSnapshot.Nationality));
        var total = conflictingApplications + 1;
        var countPassed = configuration.MaximumApplicationCount <= 0 || total <= configuration.MaximumApplicationCount;
        rules.Add(new("applicationCount", countPassed, countPassed ? EligibilityReasonCodes.ApplicationCountSatisfied : EligibilityReasonCodes.ApplicationCountExceeded, configuration.MaximumApplicationCount.ToString(System.Globalization.CultureInfo.InvariantCulture), total.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var rankRules = configuration.RankGradeAmountRules;
        var matching = rankRules.FirstOrDefault(x => string.Equals(x.RankGrade.Trim(), application.BorrowerSnapshot.RankGrade?.Trim(), StringComparison.OrdinalIgnoreCase));
        var rankPassed = rankRules.Count == 0 || matching is not null;
        var permitted = matching is null ? application.ProductSnapshot.MaximumAmount : Math.Min(application.ProductSnapshot.MaximumAmount, matching.MaximumAmount);
        rules.Add(new("rankGrade", rankPassed, rankPassed ? EligibilityReasonCodes.RankGradeSatisfied : EligibilityReasonCodes.RankGradeNotConfigured, matching?.RankGrade ?? string.Join(", ", rankRules.Select(x => x.RankGrade)), application.BorrowerSnapshot.RankGrade));
        var amountPassed = application.RequestedAmount <= permitted;
        rules.Add(new("requestedAmount", amountPassed, amountPassed ? EligibilityReasonCodes.RequestedAmountSatisfied : EligibilityReasonCodes.RequestedAmountExceedsPermitted, permitted.ToString(System.Globalization.CultureInfo.InvariantCulture), application.RequestedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return new(rules.All(x => x.Passed), now ?? DateTimeOffset.UtcNow, application.LoanProductVersionId, application.ProductSnapshot.VersionNumber, application.RequestedAmount, application.FinancingType, permitted, conflictingApplications, configuration.MaximumApplicationCount, rules);
    }
}

public sealed class LoanApplication
{
    private readonly List<ILoanApplicationDomainEvent> _domainEvents = [];
    private readonly List<LoanApplicationDocumentReference> _applicationDocuments = [];
    private LoanApplication() { }
    private LoanApplication(Guid id, Guid borrowerId, ProductSnapshot product, decimal amount, string financingType, BorrowerSnapshot borrower, DateTimeOffset created)
    { Id = id; BorrowerId = borrowerId; LoanProductId = product.LoanProductId; LoanProductVersionId = product.LoanProductVersionId; RequestedAmount = amount; Currency = product.Currency; FinancingType = financingType; Status = LoanApplicationStatus.Draft; BorrowerSnapshot = borrower; ProductSnapshot = product; CreatedAtUtc = UpdatedAtUtc = created; }
    public Guid Id { get; private set; }
    public Guid BorrowerId { get; private set; }
    public Guid LoanProductId { get; private set; }
    public Guid LoanProductVersionId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public string FinancingType { get; private set; } = "";
    public LoanApplicationStatus Status { get; private set; }
    public BorrowerSnapshot BorrowerSnapshot { get; private set; } = null!;
    public ProductSnapshot ProductSnapshot { get; private set; } = null!;
    public EligibilityDecision? EligibilityDecision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public UnitApprovalDecision? UnitApproval { get; private set; }
    public CommitteeApprovalDecision? CommitteeApproval { get; private set; }
    public InspectionPrerequisiteStatus InspectionPrerequisiteStatus { get; private set; }
    public MortgageStatus MortgageStatus { get; private set; }
    public MortgageDecision? MortgageDecision { get; private set; }
    public DocumentPrerequisiteStatus DocumentPrerequisiteStatus { get; private set; }
    public IReadOnlyList<LoanApplicationDocumentReference> ApplicationDocuments => _applicationDocuments.AsReadOnly();
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyList<ILoanApplicationDomainEvent> DomainEvents => _domainEvents;
    public static LoanApplication Create(Guid borrowerId, ProductSnapshot product, decimal amount, string financingType, BorrowerSnapshot borrower, DateTimeOffset? now = null)
    { ArgumentNullException.ThrowIfNull(product); ArgumentNullException.ThrowIfNull(borrower); Validate(amount, financingType, product); return new(Guid.NewGuid(), borrowerId, Copy(product), amount, financingType.Trim(), borrower with { }, now ?? DateTimeOffset.UtcNow); }
    public void EditDraft(decimal amount, string financingType, DateTimeOffset? now = null)
    { EnsureDraft(); Validate(amount, financingType, ProductSnapshot); if (RequestedAmount != amount || !string.Equals(FinancingType, financingType.Trim(), StringComparison.Ordinal)) EligibilityDecision = null; RequestedAmount = amount; FinancingType = financingType.Trim(); UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public EligibilityDecision EvaluateEligibility(int conflictingApplications, DateTimeOffset? now = null)
    { EnsureDraft(); EligibilityDecision = LoanApplicationEligibilityEvaluator.Evaluate(this, conflictingApplications, now); UpdatedAtUtc = EligibilityDecision.EvaluatedAtUtc; return EligibilityDecision; }
    public LoanApplicationSubmitted Submit(DateTimeOffset? now = null)
    { EnsureDraft(); if (EligibilityDecision is null) throw new EligibilityRequiredException(); if (!EligibilityDecision.IsEligible || EligibilityDecision.RequestedAmountAtEvaluation != RequestedAmount || !string.Equals(EligibilityDecision.FinancingTypeAtEvaluation, FinancingType, StringComparison.Ordinal) || EligibilityDecision.AppliedLoanProductVersionId != LoanProductVersionId) throw new LoanApplicationIneligibleException(); SubmittedAtUtc = now ?? DateTimeOffset.UtcNow; UpdatedAtUtc = SubmittedAtUtc.Value; Status = LoanApplicationStatus.Submitted; var fact = new LoanApplicationSubmitted(Id, BorrowerId, LoanProductVersionId, SubmittedAtUtc.Value); _domainEvents.Add(fact); return fact; }
    public UnitApprovalGranted ApproveByUnit(Guid actorUserId, string? comment = null, DateTimeOffset? now = null)
    { EnsureSubmitted(); EnsureActor(actorUserId); var at = now ?? DateTimeOffset.UtcNow; UnitApproval = new(UnitDecision.Approved, actorUserId, at, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), null); Status = LoanApplicationStatus.UnitApproved; UpdatedAtUtc = at; var fact = new UnitApprovalGranted(Id, actorUserId, at); _domainEvents.Add(fact); return fact; }
    public LoanApplicationRejected RejectByUnit(Guid actorUserId, string? reason, DateTimeOffset? now = null)
    { EnsureSubmitted(); EnsureActor(actorUserId); if (string.IsNullOrWhiteSpace(reason)) throw new RejectionReasonRequiredException(); var at = now ?? DateTimeOffset.UtcNow; var trimmed = reason.Trim(); UnitApproval = new(UnitDecision.Rejected, actorUserId, at, null, trimmed); RejectedAtUtc = at; Status = LoanApplicationStatus.Rejected; UpdatedAtUtc = at; var fact = new LoanApplicationRejected(Id, actorUserId, at, trimmed); _domainEvents.Add(fact); return fact; }
    public CommitteeApprovalGranted ApproveByCommittee(Guid actorUserId, string? comment = null, DateTimeOffset? now = null)
    { EnsureUnitApproved(); EnsureActor(actorUserId); var at = now ?? DateTimeOffset.UtcNow; CommitteeApproval = new(CommitteeDecision.Approved, actorUserId, at, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), null); Status = LoanApplicationStatus.CommitteeApproved; UpdatedAtUtc = at; var fact = new CommitteeApprovalGranted(Id, actorUserId, at); _domainEvents.Add(fact); return fact; }
    public LoanApplicationRejected RejectByCommittee(Guid actorUserId, string? reason, DateTimeOffset? now = null)
    { EnsureUnitApproved(); EnsureActor(actorUserId); if (string.IsNullOrWhiteSpace(reason)) throw new RejectionReasonRequiredException(); var at = now ?? DateTimeOffset.UtcNow; var trimmed = reason.Trim(); CommitteeApproval = new(CommitteeDecision.Rejected, actorUserId, at, null, trimmed); RejectedAtUtc = at; Status = LoanApplicationStatus.Rejected; UpdatedAtUtc = at; var fact = new LoanApplicationRejected(Id, actorUserId, at, trimmed, "Committee"); _domainEvents.Add(fact); return fact; }
    public void BeginPrerequisites(DateTimeOffset? now = null) { if (Status != LoanApplicationStatus.CommitteeApproved) throw new LoanApplicationStateException(); Status = LoanApplicationStatus.PrerequisitesPending; InspectionPrerequisiteStatus = global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Pending; MortgageStatus = global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Pending; DocumentPrerequisiteStatus = global::LoanSystem.Modules.LoanOrigination.Domain.DocumentPrerequisiteStatus.Pending; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void MarkInspectionApproved(DateTimeOffset? now = null) { EnsurePrerequisiteStage(); InspectionPrerequisiteStatus = global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Approved; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; EvaluateReadiness(); }
    public void MarkInspectionRejected(DateTimeOffset? now = null) { EnsurePrerequisiteStage(); InspectionPrerequisiteStatus = global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Rejected; Status = LoanApplicationStatus.PrerequisitesPending; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void AttachDocument(Guid documentId, LoanApplicationDocumentType type, DateTimeOffset? now = null) { EnsureApprovedInspection(); if (documentId == Guid.Empty) throw new InvalidDocumentTypeException(); if (_applicationDocuments.Any(x => x.DocumentId == documentId)) throw new DocumentAlreadyAttachedException(); var at = now ?? DateTimeOffset.UtcNow; _applicationDocuments.Add(new(Id, documentId, type, at)); RecalculateDocuments(); UpdatedAtUtc = at; }
    public void DetachDocument(Guid documentId, DateTimeOffset? now = null) { EnsurePrerequisiteStage(); var found = _applicationDocuments.SingleOrDefault(x => x.DocumentId == documentId) ?? throw new DocumentNotAttachedException(); _applicationDocuments.Remove(found); RecalculateDocuments(); UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void CompleteMortgage(Guid actor, string? comment = null, DateTimeOffset? now = null) { EnsureApprovedInspection(); EnsureMortgagePending(); EnsureActor(actor); var at = now ?? DateTimeOffset.UtcNow; MortgageStatus = global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Completed; MortgageDecision = new(global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Completed, actor, at, null, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()); UpdatedAtUtc = at; EvaluateReadiness(); }
    public void WaiveMortgage(Guid actor, string? reason, string? comment = null, DateTimeOffset? now = null) { EnsureApprovedInspection(); EnsureMortgagePending(); EnsureActor(actor); if (string.IsNullOrWhiteSpace(reason)) throw new MortgageReasonRequiredException(); var at = now ?? DateTimeOffset.UtcNow; MortgageStatus = global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.NotRequired; MortgageDecision = new(global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.NotRequired, actor, at, reason.Trim(), string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()); UpdatedAtUtc = at; EvaluateReadiness(); }
    public LoanApplicationApproved FinalApprove(Guid actor, DateTimeOffset? now = null)
    {
        EnsureActor(actor);
        if (Status != LoanApplicationStatus.ReadyForFinalApproval || UnitApproval?.Decision != UnitDecision.Approved || CommitteeApproval?.Decision != CommitteeDecision.Approved || InspectionPrerequisiteStatus != global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Approved || DocumentPrerequisiteStatus != global::LoanSystem.Modules.LoanOrigination.Domain.DocumentPrerequisiteStatus.Satisfied || MortgageStatus is not (global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Completed or global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.NotRequired)) throw new FinalApprovalPrerequisitesException();
        if (RequestedAmount <= 0 || RequestedAmount > ProductSnapshot.MaximumAmount) throw new InvalidApprovedAmountException();
        var at = now ?? DateTimeOffset.UtcNow; Status = LoanApplicationStatus.Approved; UpdatedAtUtc = at;
        var fact = new LoanApplicationApproved(Guid.NewGuid(), Id, BorrowerId, LoanProductId, LoanProductVersionId, RequestedAmount, Currency, FinancingType, actor, at); _domainEvents.Add(fact); return fact;
    }
    private void RecalculateDocuments() { DocumentPrerequisiteStatus = LoanDocumentPrerequisitePolicy.IsSatisfied(_applicationDocuments) ? global::LoanSystem.Modules.LoanOrigination.Domain.DocumentPrerequisiteStatus.Satisfied : global::LoanSystem.Modules.LoanOrigination.Domain.DocumentPrerequisiteStatus.Pending; EvaluateReadiness(); }
    private void EvaluateReadiness() { EnsurePrerequisiteStage(); Status = InspectionPrerequisiteStatus == global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Approved && DocumentPrerequisiteStatus == global::LoanSystem.Modules.LoanOrigination.Domain.DocumentPrerequisiteStatus.Satisfied && MortgageStatus is global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Completed or global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.NotRequired ? LoanApplicationStatus.ReadyForFinalApproval : LoanApplicationStatus.PrerequisitesPending; }
    private void EnsurePrerequisiteStage() { if (Status is not (LoanApplicationStatus.PrerequisitesPending or LoanApplicationStatus.ReadyForFinalApproval)) throw new NotInPrerequisiteStageException(); }
    private void EnsureMortgagePending() { if (MortgageStatus != global::LoanSystem.Modules.LoanOrigination.Domain.MortgageStatus.Pending) throw new MortgageAlreadyDecidedException(); }
    private void EnsureApprovedInspection() { EnsurePrerequisiteStage(); if (InspectionPrerequisiteStatus != global::LoanSystem.Modules.LoanOrigination.Domain.InspectionPrerequisiteStatus.Approved) throw new InspectionNotApprovedException(); }
    private void EnsureUnitApproved() { if (Status != LoanApplicationStatus.UnitApproved) throw new LoanApplicationStateException(); }
    private void EnsureSubmitted() { if (Status != LoanApplicationStatus.Submitted) throw new LoanApplicationStateException(); }
    private static void EnsureActor(Guid actorUserId) { if (actorUserId == Guid.Empty) throw new InvalidActorException(); }
    private void EnsureDraft() { if (Status != LoanApplicationStatus.Draft) throw new LoanApplicationStateException(); }
    private static void Validate(decimal amount, string type, ProductSnapshot product)
    { if (amount <= 0 || amount > product.MaximumAmount) throw new LoanApplicationValidationException("requestedAmount"); if (string.IsNullOrWhiteSpace(type) || !product.FinancingTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase)) throw new LoanApplicationValidationException("financingType"); }
    private static ProductSnapshot Copy(ProductSnapshot p) => p with { FinancingTypes = p.FinancingTypes.ToArray(), EligibilityConfiguration = p.EligibilityConfiguration with { RankGradeAmountRules = p.EligibilityConfiguration.RankGradeAmountRules.ToArray() } };
}
public sealed class LoanApplicationValidationException(string field) : Exception(field) { public string Field { get; } = field; }
public sealed class LoanApplicationStateException : Exception;
public sealed class EligibilityRequiredException : Exception;
public sealed class LoanApplicationIneligibleException : Exception;
public sealed class RejectionReasonRequiredException : Exception;
public sealed class InvalidActorException : Exception;
public sealed class NotInPrerequisiteStageException : Exception;
public sealed class InspectionNotApprovedException : Exception;
public sealed class DocumentAlreadyAttachedException : Exception;
public sealed class DocumentNotAttachedException : Exception;
public sealed class InvalidDocumentTypeException : Exception;
public sealed class MortgageReasonRequiredException : Exception;
public sealed class MortgageAlreadyDecidedException : Exception;
public sealed class FinalApprovalPrerequisitesException : Exception;
public sealed class InvalidApprovedAmountException : Exception;
