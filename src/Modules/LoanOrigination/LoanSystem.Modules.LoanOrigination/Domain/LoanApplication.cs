namespace LoanSystem.Modules.LoanOrigination.Domain;

public enum LoanApplicationStatus { Draft }
public sealed record BorrowerSnapshot(string CivilNumber, string? EmployeeNumber, string FullName, string? PhoneNumber, string Nationality, string Organization, string? RankGrade, string? EmploymentInformation, string Status);
public sealed record ProductRankGradeRuleSnapshot(string RankGrade, decimal MaximumAmount);
public sealed record ProductEligibilitySnapshot(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<ProductRankGradeRuleSnapshot> RankGradeAmountRules, int MaximumTermMonths, string DueDateRule);
public sealed record ProductSnapshot(Guid LoanProductId, Guid LoanProductVersionId, string ProductName, int VersionNumber, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, ProductEligibilitySnapshot EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ProductStatus, string VersionStatus, DateTimeOffset? PublishedAtUtc);
public sealed class LoanApplication
{
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
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static LoanApplication Create(Guid borrowerId, ProductSnapshot product, decimal amount, string financingType, BorrowerSnapshot borrower, DateTimeOffset? now = null)
    { ArgumentNullException.ThrowIfNull(product); ArgumentNullException.ThrowIfNull(borrower); Validate(amount, financingType, product); return new(Guid.NewGuid(), borrowerId, Copy(product), amount, financingType.Trim(), borrower with { }, now ?? DateTimeOffset.UtcNow); }
    public void EditDraft(decimal amount, string financingType, DateTimeOffset? now = null)
    { if (Status != LoanApplicationStatus.Draft) throw new LoanApplicationStateException(); Validate(amount, financingType, ProductSnapshot); RequestedAmount = amount; FinancingType = financingType.Trim(); UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    private static void Validate(decimal amount, string type, ProductSnapshot product)
    { if (amount <= 0 || amount > product.MaximumAmount) throw new LoanApplicationValidationException("requestedAmount"); if (string.IsNullOrWhiteSpace(type) || !product.FinancingTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase)) throw new LoanApplicationValidationException("financingType"); }
    private static ProductSnapshot Copy(ProductSnapshot p) => p with { FinancingTypes = p.FinancingTypes.ToArray(), EligibilityConfiguration = p.EligibilityConfiguration with { RankGradeAmountRules = p.EligibilityConfiguration.RankGradeAmountRules.ToArray() } };
}
public sealed class LoanApplicationValidationException(string field) : Exception(field) { public string Field { get; } = field; }
public sealed class LoanApplicationStateException : Exception;
