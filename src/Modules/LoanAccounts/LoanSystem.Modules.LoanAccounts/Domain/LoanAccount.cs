namespace LoanSystem.Modules.LoanAccounts.Domain;

public enum LoanAccountStatus { Active, FullyRepaid, Closed, Cancelled }
public interface ILoanAccountDomainEvent;
public sealed record LoanAccountOpened(Guid LoanId, Guid SourceApplicationId, DateTimeOffset OpenedAtUtc) : ILoanAccountDomainEvent;

public sealed class LoanAccount
{
    private readonly List<ILoanAccountDomainEvent> _domainEvents = [];
    private LoanAccount() { }
    public Guid LoanId { get; private set; }
    public Guid SourceApplicationId { get; private set; }
    public Guid BorrowerId { get; private set; }
    public Guid LoanProductId { get; private set; }
    public Guid LoanProductVersionId { get; private set; }
    public decimal ApprovedAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public string FinancingType { get; private set; } = "";
    public decimal ReservedDisbursementAmount { get; private set; }
    public decimal TotalDisbursed { get; private set; }
    public decimal TotalRepaid { get; private set; }
    public decimal AvailableToDisburse => ApprovedAmount - TotalDisbursed - ReservedDisbursementAmount;
    public decimal OutstandingBalance => TotalDisbursed - TotalRepaid;
    public LoanAccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyList<ILoanAccountDomainEvent> DomainEvents => _domainEvents;
    public static LoanAccount Open(Guid sourceApplicationId, Guid borrowerId, Guid productId, Guid productVersionId, decimal approvedAmount, string currency, string financingType, DateTimeOffset? openedAt = null)
    {
        if (sourceApplicationId == Guid.Empty || borrowerId == Guid.Empty || productId == Guid.Empty || productVersionId == Guid.Empty) throw new LoanAccountValidationException("source");
        if (approvedAmount <= 0) throw new LoanAccountValidationException("approvedAmount");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) throw new LoanAccountValidationException("currency");
        if (string.IsNullOrWhiteSpace(financingType)) throw new LoanAccountValidationException("financingType");
        var at = openedAt ?? DateTimeOffset.UtcNow;
        var loan = new LoanAccount { LoanId = Guid.NewGuid(), SourceApplicationId = sourceApplicationId, BorrowerId = borrowerId, LoanProductId = productId, LoanProductVersionId = productVersionId, ApprovedAmount = approvedAmount, Currency = currency.Trim().ToUpperInvariant(), FinancingType = financingType.Trim(), Status = LoanAccountStatus.Active, OpenedAtUtc = at };
        loan._domainEvents.Add(new LoanAccountOpened(loan.LoanId, sourceApplicationId, at)); return loan;
    }
}
public sealed class LoanAccountValidationException(string field) : Exception(field) { public string Field { get; } = field; }
