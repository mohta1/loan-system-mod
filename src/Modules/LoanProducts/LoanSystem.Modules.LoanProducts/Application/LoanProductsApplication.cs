using LoanSystem.Contracts;
using LoanSystem.Modules.LoanProducts.Domain;
namespace LoanSystem.Modules.LoanProducts.Application;

public static class LoanProductPermissions { public const string Read = "loanProducts.read", Manage = "loanProducts.manage", Publish = "loanProducts.publish", ManageStatus = "loanProducts.manageStatus"; }
public sealed record EligibilityInput(string RequiredNationality, int MaximumApplicationCount, IReadOnlyList<RankGradeAmountRule> RankGradeAmountRules, TermConfiguration Term);
public sealed record VersionInput(decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, EligibilityInput EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record ProductListItem(Guid LoanProductId, string Name, string Status, int VersionCount, int? LatestVersionNumber);
public sealed record ProductVersionDto(Guid VersionId, int VersionNumber, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, EligibilityInput EligibilityConfiguration, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Status, DateTimeOffset? PublishedAtUtc, string ETag);
public sealed record ProductDetail(Guid LoanProductId, string Name, string Status, DateTimeOffset CreatedAtUtc, string ETag, IReadOnlyList<ProductVersionDto> Versions);
public sealed record AvailableProductVersion(Guid LoanProductId, Guid VersionId, string ProductName, int VersionNumber, decimal MaximumAmount, string Currency, decimal DeductionPercentage, IReadOnlyList<string> FinancingTypes, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public interface IBusinessClock { DateOnly Today { get; } DateTimeOffset UtcNow { get; } }
public sealed class SystemBusinessClock : IBusinessClock { public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow); public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public interface ILoanProductStore
{
    Task AddAsync(LoanProduct product, CancellationToken ct); Task<LoanProduct?> FindAsync(Guid id, CancellationToken ct); Task<LoanProductVersion?> FindVersionAsync(Guid productId, Guid versionId, CancellationToken ct); Task<IReadOnlyList<ProductListItem>> ListAsync(CancellationToken ct); Task<IReadOnlyList<AvailableProductVersion>> AvailableAsync(DateOnly today, CancellationToken ct); Task<int> NextVersionNumberAsync(Guid productId, CancellationToken ct); Task LockProductAsync(Guid productId, CancellationToken ct); Task EnsureVersionAsync(LoanProductVersion version, byte[] expected, CancellationToken ct); Task<bool> OverlapsAsync(Guid productId, Guid except, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken ct); void Expect(LoanProduct product, byte[] version); void Expect(LoanProductVersion version, byte[] expected); Task SaveAsync(CancellationToken ct);
}
public sealed class LoanProductConcurrencyException : Exception;
public sealed class LoanProductVersionConflictException : Exception;
public sealed class EffectivePeriodConflictException : Exception;
public sealed class LoanProductService(ILoanProductStore store, IBusinessClock clock)
{
    public async Task<ProductDetail> CreateAsync(string name, CancellationToken ct) { var p = LoanProduct.Create(name, clock.UtcNow); await store.AddAsync(p, ct); await store.SaveAsync(ct); return Map(p); }
    public Task<IReadOnlyList<ProductListItem>> ListAsync(CancellationToken ct) => store.ListAsync(ct);
    public async Task<ProductDetail?> GetAsync(Guid id, CancellationToken ct) { var p = await store.FindAsync(id, ct); return p is null ? null : Map(p); }
    public async Task<ProductVersionDto?> CreateDraftAsync(Guid id, VersionInput input, CancellationToken ct) { var p = await store.FindAsync(id, ct); if (p is null) return null; var number = await store.NextVersionNumberAsync(id, ct); var v = p.CreateDraftVersion(number, input.MaximumAmount, input.Currency, input.DeductionPercentage, input.FinancingTypes, Eligibility(input.EligibilityConfiguration), input.EffectiveFrom, input.EffectiveTo, clock.UtcNow); await store.SaveAsync(ct); return Map(v); }
    public async Task<ProductVersionDto?> EditAsync(Guid id, Guid versionId, VersionInput input, byte[] expected, CancellationToken ct) { var v = await store.FindVersionAsync(id, versionId, ct); if (v is null) return null; v.Edit(input.MaximumAmount, input.Currency, input.DeductionPercentage, input.FinancingTypes, Eligibility(input.EligibilityConfiguration), input.EffectiveFrom, input.EffectiveTo); store.Expect(v, expected); await store.SaveAsync(ct); return Map(v); }
    public async Task<ProductVersionDto?> PublishAsync(Guid id, Guid versionId, byte[] expected, CancellationToken ct) { var v = await store.FindVersionAsync(id, versionId, ct); if (v is null) return null; await store.EnsureVersionAsync(v, expected, ct); await store.LockProductAsync(id, ct); if (await store.OverlapsAsync(id, v.Id, v.EffectiveFrom, v.EffectiveTo, ct)) throw new EffectivePeriodConflictException(); v.Publish(clock.UtcNow); store.Expect(v, expected); await store.SaveAsync(ct); return Map(v); }
    public async Task<ProductDetail?> StatusAsync(Guid id, bool active, byte[] expected, CancellationToken ct) { var p = await store.FindAsync(id, ct); if (p is null) return null; if (active) p.Activate(clock.UtcNow); else p.Deactivate(clock.UtcNow); store.Expect(p, expected); await store.SaveAsync(ct); return Map(p); }
    public Task<IReadOnlyList<AvailableProductVersion>> AvailableAsync(CancellationToken ct) => store.AvailableAsync(clock.Today, ct);
    static EligibilityConfiguration Eligibility(EligibilityInput x) => new(x.RequiredNationality, x.MaximumApplicationCount, x.RankGradeAmountRules, x.Term);
    public static ProductVersionDto Map(LoanProductVersion v) => new(v.Id, v.VersionNumber, v.MaximumAmount, v.Currency, v.DeductionPercentage, v.FinancingTypes.Select(x => x.Value).Order().ToArray(), new(v.EligibilityConfiguration.RequiredNationality, v.EligibilityConfiguration.MaximumApplicationCount, v.EligibilityConfiguration.RankGradeAmountRules, v.EligibilityConfiguration.Term), v.EffectiveFrom, v.EffectiveTo, v.Status.ToString(), v.PublishedAtUtc, Convert.ToBase64String(v.RowVersion));
    public static ProductDetail Map(LoanProduct p) => new(p.Id, p.Name, p.Status.ToString(), p.CreatedAtUtc, Convert.ToBase64String(p.RowVersion), p.Versions.OrderByDescending(x => x.VersionNumber).Select(Map).ToArray());
}
