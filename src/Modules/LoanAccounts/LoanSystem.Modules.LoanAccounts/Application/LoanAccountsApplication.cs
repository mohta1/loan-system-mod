using LoanSystem.Contracts;
using LoanSystem.Modules.LoanAccounts.Domain;

namespace LoanSystem.Modules.LoanAccounts.Application;

public static class LoanAccountPermissions { public const string Read = "loans.read"; }
public sealed record LoanAccountDto(Guid LoanId, Guid SourceApplicationId, Guid BorrowerId, Guid LoanProductId, Guid LoanProductVersionId, decimal ApprovedAmount, string Currency, string FinancingType, decimal ReservedDisbursementAmount, decimal TotalDisbursed, decimal AvailableToDisburse, decimal TotalRepaid, decimal OutstandingBalance, string Status, DateTimeOffset OpenedAtUtc, string ETag);
public sealed record LoanAccountSearch(Guid? LoanId, Guid? SourceApplicationId, Guid? BorrowerId, LoanAccountStatus? Status, int PageNumber = 1, int PageSize = 25);
public sealed record LoanAccountPage(IReadOnlyList<LoanAccountDto> Items, int PageNumber, int PageSize, int TotalCount);
public interface ILoanAccountStore { Task<LoanAccount?> FindAsync(Guid id, CancellationToken ct); Task<LoanAccount?> FindBySourceAsync(Guid sourceId, CancellationToken ct); Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct); Task OpenOnceAsync(LoanAccount account, Guid eventId, DateTimeOffset occurredAt, CancellationToken ct); }
public sealed class LoanAccountService(ILoanAccountStore store) : ILoanApplicationApprovedConsumer
{
    public Task<LoanAccount?> FindBySourceAsync(Guid id, CancellationToken ct) => store.FindBySourceAsync(id, ct);
    public async Task<LoanAccountDto?> GetAsync(Guid id, CancellationToken ct) { var x = await store.FindAsync(id, ct); return x is null ? null : Map(x); }
    public Task<LoanAccountPage> SearchAsync(LoanAccountSearch search, CancellationToken ct) => store.SearchAsync(search with { PageNumber = Math.Max(1, search.PageNumber), PageSize = Math.Clamp(search.PageSize, 1, 100) }, ct);
    public async Task ConsumeAsync(LoanApplicationApprovedV1 message, CancellationToken cancellationToken = default)
    {
        var loan = LoanAccount.Open(message.LoanApplicationId, message.BorrowerId, message.LoanProductId, message.LoanProductVersionId, message.ApprovedAmount, message.Currency, message.FinancingType, message.ApprovedAtUtc);
        await store.OpenOnceAsync(loan, message.EventId, message.OccurredAtUtc, cancellationToken);
    }
    public static LoanAccountDto Map(LoanAccount x) => new(x.LoanId, x.SourceApplicationId, x.BorrowerId, x.LoanProductId, x.LoanProductVersionId, x.ApprovedAmount, x.Currency, x.FinancingType, x.ReservedDisbursementAmount, x.TotalDisbursed, x.AvailableToDisburse, x.TotalRepaid, x.OutstandingBalance, x.Status.ToString(), x.OpenedAtUtc, Convert.ToBase64String(x.RowVersion));
}
