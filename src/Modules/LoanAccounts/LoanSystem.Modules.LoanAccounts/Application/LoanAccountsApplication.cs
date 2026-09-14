using LoanSystem.Contracts;
using LoanSystem.Modules.LoanAccounts.Domain;

namespace LoanSystem.Modules.LoanAccounts.Application;

public static class LoanAccountPermissions { public const string Read = "loans.read", ChangeFinancingType = "loans.changeFinancingType"; }
public sealed class LoanAccountConcurrencyException : Exception;
public sealed record LoanAccountDto(Guid LoanId, Guid SourceApplicationId, Guid BorrowerId, Guid LoanProductId, Guid LoanProductVersionId, decimal ApprovedAmount, string Currency, string FinancingType, decimal ReservedDisbursementAmount, decimal TotalDisbursed, decimal AvailableToDisburse, decimal TotalRepaid, decimal OutstandingBalance, string Status, DateTimeOffset OpenedAtUtc, string ETag);
public sealed record LoanAccountSearch(Guid? LoanId, Guid? SourceApplicationId, Guid? BorrowerId, LoanAccountStatus? Status, int PageNumber = 1, int PageSize = 25);
public sealed record LoanAccountPage(IReadOnlyList<LoanAccountDto> Items, int PageNumber, int PageSize, int TotalCount);
public interface ILoanAccountStore { Task<LoanAccount?> FindAsync(Guid id, CancellationToken ct); Task<LoanAccount?> FindBySourceAsync(Guid sourceId, CancellationToken ct); Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct); Task OpenOnceAsync(LoanAccount account, Guid eventId, DateTimeOffset occurredAt, CancellationToken ct); Task ReserveAsync(DisbursementCapacityRequestedV1 message, CancellationToken ct); void Expect(LoanAccount account, byte[] expected); Task SaveAsync(CancellationToken ct); }
public sealed class LoanAccountService(ILoanAccountStore store, IHistoricalLoanProductVersions? products = null) : ILoanApplicationApprovedConsumer, IDisbursementCapacityRequestedConsumer, ILoanAccountsModule
{
    public Task<LoanAccount?> FindBySourceAsync(Guid id, CancellationToken ct) => store.FindBySourceAsync(id, ct);
    public async Task<LoanAccountDto?> GetAsync(Guid id, CancellationToken ct) { var x = await store.FindAsync(id, ct); return x is null ? null : Map(x); }
    public Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct) => store.SearchAsync(search with { PageNumber = Math.Max(1, search.PageNumber), PageSize = Math.Clamp(search.PageSize, 1, 100) }, ct);
    public async Task ConsumeAsync(LoanApplicationApprovedV1 message, CancellationToken cancellationToken = default)
    {
        var loan = LoanAccount.Open(message.LoanApplicationId, message.BorrowerId, message.LoanProductId, message.LoanProductVersionId, message.ApprovedAmount, message.Currency, message.FinancingType, message.ApprovedAtUtc);
        await store.OpenOnceAsync(loan, message.EventId, message.OccurredAtUtc, cancellationToken);
    }
    public Task ConsumeAsync(DisbursementCapacityRequestedV1 message, CancellationToken cancellationToken = default) => store.ReserveAsync(message, cancellationToken);
    async Task<LoanAccountContract?> ILoanAccountsModule.GetAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var x = await store.FindAsync(loanId, cancellationToken); return x is null ? null : new(x.LoanId, x.BorrowerId, x.LoanProductId, x.LoanProductVersionId, x.ApprovedAmount, x.Currency, x.FinancingType, x.ReservedDisbursementAmount, x.TotalDisbursed, x.AvailableToDisburse, x.Status.ToString(), Convert.ToBase64String(x.RowVersion));
    }
    public async Task<LoanAccountDto?> ChangeFinancingTypeAsync(Guid loanId, string financingType, Guid actor, string correlationId, byte[] expected, CancellationToken ct)
    {
        var loan = await store.FindAsync(loanId, ct); if (loan is null) return null;
        // Enforce the HTTP precondition against the version that was actually read before
        // evaluating business rules. This keeps stale If-Match semantics deterministic (412)
        // even when another operation has already created a reservation that would also make
        // the financing-type change invalid. SaveAsync still protects the race after this check.
        store.Expect(loan, expected);
        var version = products is null ? null : await products.GetDefinitionAsync(loan.LoanProductVersionId, ct);
        if (version is null) throw new UnsupportedFinancingTypeException();
        loan.ChangeFinancingType(financingType, version.FinancingTypes, actor, correlationId, DateTimeOffset.UtcNow);
        await store.SaveAsync(ct); return Map(loan);
    }
    public static LoanAccountDto Map(LoanAccount x) => new(x.LoanId, x.SourceApplicationId, x.BorrowerId, x.LoanProductId, x.LoanProductVersionId, x.ApprovedAmount, x.Currency, x.FinancingType, x.ReservedDisbursementAmount, x.TotalDisbursed, x.AvailableToDisburse, x.TotalRepaid, x.OutstandingBalance, x.Status.ToString(), x.OpenedAtUtc, Convert.ToBase64String(x.RowVersion));
}
