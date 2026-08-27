namespace LoanSystem.Modules.LoanProducts.Domain;

public enum LoanProductStatus { Active, Inactive }
public enum LoanProductVersionStatus { Draft, Published }
public sealed record RankGradeAmountRule(string RankGrade, decimal MaximumAmount);
public sealed record TermConfiguration(int MaximumTermMonths, string DueDateRule);

public sealed class EligibilityConfiguration
{
    private readonly RankGradeAmountRule[] rankGradeAmountRules;

    public EligibilityConfiguration(string requiredNationality, int maximumApplicationCount, IEnumerable<RankGradeAmountRule> rankGradeAmountRules, TermConfiguration term)
    {
        RequiredNationality = requiredNationality;
        MaximumApplicationCount = maximumApplicationCount;
        this.rankGradeAmountRules = rankGradeAmountRules?.ToArray() ?? [];
        Term = term;
    }

    public string RequiredNationality { get; }
    public int MaximumApplicationCount { get; }
    public IReadOnlyList<RankGradeAmountRule> RankGradeAmountRules => Array.AsReadOnly(rankGradeAmountRules);
    public TermConfiguration Term { get; }
}

public sealed class LoanProduct
{
    private readonly List<LoanProductVersion> versions = [];

    private LoanProduct() { }

    private LoanProduct(string name, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = Required(name, 200);
        Status = LoanProductStatus.Active;
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public LoanProductStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LoanProductVersion> Versions => versions.AsReadOnly();

    public static LoanProduct Create(string name, DateTimeOffset? now = null) => new(name, now ?? DateTimeOffset.UtcNow);

    public LoanProductVersion CreateDraftVersion(int versionNumber, decimal maximumAmount, string currency, decimal deductionPercentage, IEnumerable<string> financingTypes, EligibilityConfiguration eligibility, DateOnly effectiveFrom, DateOnly? effectiveTo, DateTimeOffset? now = null)
    {
        var version = LoanProductVersion.CreateDraft(Id, versionNumber, maximumAmount, currency, deductionPercentage, financingTypes, eligibility, effectiveFrom, effectiveTo, now);
        versions.Add(version);
        return version;
    }

    public void Activate(DateTimeOffset? now = null) { Status = LoanProductStatus.Active; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }
    public void Deactivate(DateTimeOffset? now = null) { Status = LoanProductStatus.Inactive; UpdatedAtUtc = now ?? DateTimeOffset.UtcNow; }

    internal static string Required(string value, int max)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > max) throw new LoanProductValidationException();
        return normalized;
    }
}

public sealed class LoanProductVersion
{
    private readonly List<LoanProductFinancingType> financingTypes = [];

    private LoanProductVersion() { }

    private LoanProductVersion(Guid productId, int number, decimal amount, string currency, decimal percentage, IEnumerable<string> types, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        LoanProductId = productId;
        VersionNumber = number;
        Status = LoanProductVersionStatus.Draft;
        CreatedAtUtc = now;
        Apply(amount, currency, percentage, types, eligibility, from, to);
    }

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
    public IReadOnlyCollection<LoanProductFinancingType> FinancingTypes => financingTypes.AsReadOnly();

    internal static LoanProductVersion CreateDraft(Guid productId, int number, decimal amount, string currency, decimal percentage, IEnumerable<string> types, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to, DateTimeOffset? now = null) => new(productId, number, amount, currency, percentage, types, eligibility, from, to, now ?? DateTimeOffset.UtcNow);

    public void Edit(decimal amount, string currency, decimal percentage, IEnumerable<string> types, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to)
    {
        if (Status != LoanProductVersionStatus.Draft) throw new LoanProductVersionImmutableException();
        Apply(amount, currency, percentage, types, eligibility, from, to);
    }

    public void Publish(DateTimeOffset? now = null)
    {
        if (Status != LoanProductVersionStatus.Draft) throw new LoanProductInvalidStateException();
        Validate();
        Status = LoanProductVersionStatus.Published;
        PublishedAtUtc = now ?? DateTimeOffset.UtcNow;
    }

    private void Apply(decimal amount, string currency, decimal percentage, IEnumerable<string> types, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to)
    {
        var normalizedTypes = (types ?? []).Select(value => LoanProduct.Required(value, 100)).ToArray();
        Validate(amount, currency, percentage, normalizedTypes, eligibility, from, to);
        MaximumAmount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        DeductionPercentage = percentage;
        EligibilityConfiguration = new EligibilityConfiguration(eligibility.RequiredNationality, eligibility.MaximumApplicationCount, eligibility.RankGradeAmountRules, eligibility.Term);
        EffectiveFrom = from;
        EffectiveTo = to;
        financingTypes.Clear();
        financingTypes.AddRange(normalizedTypes.Select(value => new LoanProductFinancingType(Id, value)));
    }

    private void Validate() => Validate(MaximumAmount, Currency, DeductionPercentage, financingTypes.Select(value => value.Value), EligibilityConfiguration, EffectiveFrom, EffectiveTo);

    private static void Validate(decimal amount, string currency, decimal percentage, IEnumerable<string> types, EligibilityConfiguration eligibility, DateOnly from, DateOnly? to)
    {
        var typeValues = types.ToArray();
        if (amount <= 0 || string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3 || !currency.Trim().All(char.IsLetter) || percentage < 0 || percentage > 100 || to < from || typeValues.Length == 0) throw new LoanProductValidationException();
        if (typeValues.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new LoanProductValidationException();
        if (eligibility is null || string.IsNullOrWhiteSpace(eligibility.RequiredNationality) || eligibility.RequiredNationality.Length > 100 || eligibility.MaximumApplicationCount <= 0 || eligibility.Term is null || eligibility.Term.MaximumTermMonths <= 0 || string.IsNullOrWhiteSpace(eligibility.Term.DueDateRule)) throw new LoanProductValidationException();
        if (eligibility.RankGradeAmountRules.Count == 0 || eligibility.RankGradeAmountRules.Any(rule => string.IsNullOrWhiteSpace(rule.RankGrade) || rule.MaximumAmount <= 0 || rule.MaximumAmount > amount) || eligibility.RankGradeAmountRules.GroupBy(rule => rule.RankGrade.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new LoanProductValidationException();
    }
}

public sealed class LoanProductFinancingType
{
    private LoanProductFinancingType() { }
    internal LoanProductFinancingType(Guid versionId, string value) { VersionId = versionId; Value = value; }
    public Guid VersionId { get; private set; }
    public string Value { get; private set; } = "";
}

public sealed class LoanProductValidationException : Exception;
public sealed class LoanProductVersionImmutableException : Exception;
public sealed class LoanProductInvalidStateException : Exception;
