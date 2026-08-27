namespace LoanSystem.Modules.LoanProducts.Domain;

public enum LoanProductStatus { Active, Inactive }
public enum LoanProductVersionStatus { Draft, Published }
public sealed record RankGradeAmountRule(string RankGrade, decimal MaximumAmount);
public sealed record TermConfiguration(int MaximumTermMonths, string DueDateRule);
public sealed record EligibilityConfiguration(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<RankGradeAmountRule> RankGradeAmountRules, TermConfiguration Term);

public sealed class LoanProduct
{
    private LoanProduct() { }
    private LoanProduct(string name, DateTimeOffset now) { Id = Guid.NewGuid(); Name = Required(name, 200); Status = LoanProductStatus.Active; CreatedAtUtc = UpdatedAtUtc = now; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public LoanProductStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<LoanProductVersion> Versions { get; } = [];
    public static LoanProduct Create(string name, DateTimeOffset? now = null) => new(name, now ?? DateTimeOffset.UtcNow);
    public void Activate(DateTimeOffset? now = null) { Status = LoanProductStatus.Active; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void Deactivate(DateTimeOffset? now = null) { Status = LoanProductStatus.Inactive; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    internal static string Required(string value, int max) { var v = value?.Trim(); if (string.IsNullOrWhiteSpace(v) || v.Length > max) throw new LoanProductValidationException(); return v; }
}

public sealed class LoanProductVersion
{
    private LoanProductVersion() { }
    private LoanProductVersion(Guid productId, int number, decimal amount, string currency, decimal percentage, IEnumerable<string> financingTypes, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to, DateTimeOffset now)
    { Id = Guid.NewGuid(); LoanProductId = productId; VersionNumber = number; Status = LoanProductVersionStatus.Draft; CreatedAtUtc = now; Apply(amount, currency, percentage, financingTypes, eligibility, from, to); }
    public Guid Id { get; private set; }
    public Guid LoanProductId { get; private set; }
    public int VersionNumber { get; private set; }
    public decimal MaximumAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public decimal DeductionPercentage { get; private set; }
    public EligibilityConfiguration EligibilityConfiguration { get; private set; } = null!;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public LoanProductVersionStatus Status { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<LoanProductFinancingType> FinancingTypes { get; } = [];
    public static LoanProductVersion CreateDraft(Guid productId, int number, decimal amount, string currency, decimal percentage, IEnumerable<string> financingTypes, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to, DateTimeOffset? now = null) => new(productId, number, amount, currency, percentage, financingTypes, eligibility, from, to, now ?? DateTimeOffset.UtcNow);
    public void Edit(decimal amount, string currency, decimal percentage, IEnumerable<string> financingTypes, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to)
    { if (Status != LoanProductVersionStatus.Draft) throw new LoanProductVersionImmutableException(); Apply(amount, currency, percentage, financingTypes, eligibility, from, to); }
    public void Publish(DateTimeOffset? now = null) { if (Status != LoanProductVersionStatus.Draft) throw new LoanProductInvalidStateException(); Validate(); Status = LoanProductVersionStatus.Published; PublishedAtUtc = now ?? DateTimeOffset.UtcNow; }
    private void Apply(decimal amount, string currency, decimal percentage, IEnumerable<string> financingTypes, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to)
    { MaximumAmount = amount; Currency = (currency ?? "").Trim().ToUpperInvariant(); DeductionPercentage = percentage; EligibilityConfiguration = eligibility; EffectiveFrom = from; EffectiveTo = to; FinancingTypes.Clear(); foreach (var value in financingTypes ?? []) FinancingTypes.Add(new(Id, LoanProduct.Required(value, 100))); Validate(); }
    private void Validate()
    {
        if (MaximumAmount <= 0 || Currency.Length != 3 || !Currency.All(char.IsLetter) || DeductionPercentage < 0 || DeductionPercentage > 100 || EffectiveTo < EffectiveFrom || FinancingTypes.Count == 0) throw new LoanProductValidationException();
        if (FinancingTypes.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) throw new LoanProductValidationException();
        if (EligibilityConfiguration is null || string.IsNullOrWhiteSpace(EligibilityConfiguration.RequiredNationality) || EligibilityConfiguration.RequiredNationality.Length > 100 || EligibilityConfiguration.MaximumApplicationCount <= 0 || EligibilityConfiguration.Term is null || EligibilityConfiguration.Term.MaximumTermMonths <= 0 || string.IsNullOrWhiteSpace(EligibilityConfiguration.Term.DueDateRule)) throw new LoanProductValidationException();
        if (EligibilityConfiguration.RankGradeAmountRules is null || EligibilityConfiguration.RankGradeAmountRules.Count == 0 || EligibilityConfiguration.RankGradeAmountRules.Any(x => string.IsNullOrWhiteSpace(x.RankGrade) || x.MaximumAmount <= 0 || x.MaximumAmount > MaximumAmount) || EligibilityConfiguration.RankGradeAmountRules.GroupBy(x => x.RankGrade.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) throw new LoanProductValidationException();
    }
}
public sealed class LoanProductFinancingType { private LoanProductFinancingType() { } internal LoanProductFinancingType(Guid versionId, string value) { VersionId = versionId; Value = value; } public Guid VersionId { get; private set; } public string Value { get; private set; } = ""; }
public sealed class LoanProductValidationException : Exception;
public sealed class LoanProductVersionImmutableException : Exception;
public sealed class LoanProductInvalidStateException : Exception;
