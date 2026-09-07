using System.Text.Json.Serialization;
namespace LoanSystem.Modules.LoanOrigination.Domain;

public enum LoanApplicationStatus { Draft, Submitted, UnitApproved, Rejected }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnitDecision { Approved, Rejected }
public sealed record UnitApprovalDecision(UnitDecision Decision, Guid ActorUserId, DateTimeOffset DecidedAtUtc, string? Comment, string? RejectionReason);
public interface ILoanApplicationDomainEvent;
public sealed record BorrowerSnapshot(string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation, string Status);
public sealed record ProductRankGradeRuleSnapshot(string RankGrade, decimal MaximumAmount);
public sealed record ProductEligibilitySnapshot(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<ProductRankGradeRuleSnapshot> RankGradeAmountRules, int MaximumTermMonths, string DueDateRule);
public sealed record ProductSnapshot(Guid LoanProductId, Guid LoanProductVersionId, string ProductName, int VersionNumber, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, ProductEligibilitySnapshot EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ProductStatus, string VersionStatus, DateTimeOffset? PublishedAtUtc);
public sealed record EligibilityRuleResult(string Rule, bool Passed, string ReasonCode, string? ExpectedValue = null, string? ActualValue = null);
public sealed record EligibilityDecision(bool IsEligible, DateTimeOffset EvaluatedAtUtc, Guid AppliedLoanProductVersionId, int AppliedProductVersionNumber, decimal RequestedAmountAtEvaluation, string FinancingTypeAtEvaluation, decimal PermittedAmount, int ObservedConflictingApplicationCount, int MaximumApplicationCount, IReadOnlyList<EligibilityRuleResult> RuleResults);
public sealed record LoanApplicationSubmitted(Guid LoanApplicationId, Guid BorrowerId, Guid LoanProductVersionId, DateTimeOffset SubmittedAtUtc) : ILoanApplicationDomainEvent;
public sealed record UnitApprovalGranted(Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp) : ILoanApplicationDomainEvent;
public sealed record LoanApplicationRejected(Guid LoanApplicationId, Guid ActorUserId, DateTimeOffset Timestamp, string Reason, string Stage = "Unit") : ILoanApplicationDomainEvent;

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
