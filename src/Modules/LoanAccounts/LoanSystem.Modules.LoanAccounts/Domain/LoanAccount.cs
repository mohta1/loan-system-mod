namespace LoanSystem.Modules.LoanAccounts.Domain;

public enum LoanAccountStatus { Active, FullyRepaid, Closed, Cancelled }
public interface ILoanAccountDomainEvent;
public sealed record LoanAccountOpened(Guid LoanId, Guid SourceApplicationId, DateTimeOffset OpenedAtUtc) : ILoanAccountDomainEvent;
public enum ReservationStatus { Reserved }
public enum ReservationDecision { Reserved, Duplicate, Rejected }
public sealed record ReservationResult(ReservationDecision Decision, string? ReasonCode = null, string? Reason = null);
public sealed class DisbursementReservation
{
    private DisbursementReservation() { }
    internal DisbursementReservation(Guid loanId, Guid disbursementId, decimal amount, string currency, DateTimeOffset at) { LoanId = loanId; DisbursementId = disbursementId; Amount = amount; Currency = currency; Status = ReservationStatus.Reserved; ReservedAtUtc = at; UpdatedAtUtc = at; }
    public Guid LoanId { get; private set; }
    public Guid DisbursementId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset ReservedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
public sealed class FinancingTypeChange
{
    private FinancingTypeChange() { }
    internal FinancingTypeChange(Guid id, Guid loanId, Guid actor, string previous, string next, DateTimeOffset at, string correlationId) { Id = id; LoanId = loanId; ActorUserId = actor; PreviousFinancingType = previous; NewFinancingType = next; ChangedAtUtc = at; CorrelationId = correlationId; }
    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string PreviousFinancingType { get; private set; } = ""; public string NewFinancingType { get; private set; } = ""; public DateTimeOffset ChangedAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = "";
}

public sealed class LoanAccount
{
    private readonly List<ILoanAccountDomainEvent> _domainEvents = [];
    private readonly List<DisbursementReservation> _reservations = [];
    private readonly List<FinancingTypeChange> _financingTypeChanges = [];
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
    public IReadOnlyList<DisbursementReservation> Reservations => _reservations;
    public IReadOnlyList<FinancingTypeChange> FinancingTypeChanges => _financingTypeChanges;
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

    public ReservationResult ReserveDisbursementCapacity(Guid disbursementId, decimal amount, string currency, DateTimeOffset at)
    {
        if (disbursementId == Guid.Empty || amount <= 0) return new(ReservationDecision.Rejected, "capacity.invalidRequest", "The capacity request is invalid.");
        var existing = _reservations.SingleOrDefault(x => x.DisbursementId == disbursementId);
        if (existing is not null)
            return existing.Amount == amount && string.Equals(existing.Currency, currency, StringComparison.OrdinalIgnoreCase)
                ? new(ReservationDecision.Duplicate)
                : new(ReservationDecision.Rejected, "capacity.inconsistentDuplicate", "The disbursement identifier was previously used with different financial data.");
        if (Status != LoanAccountStatus.Active) return new(ReservationDecision.Rejected, "capacity.loanInactive", "The loan cannot accept a capacity reservation in its current state.");
        if (!string.Equals(Currency, currency?.Trim(), StringComparison.OrdinalIgnoreCase)) return new(ReservationDecision.Rejected, "capacity.currencyMismatch", "The requested currency does not match the loan currency.");
        if (TotalDisbursed + ReservedDisbursementAmount + amount > ApprovedAmount) return new(ReservationDecision.Rejected, "capacity.insufficient", "The requested amount exceeds available loan capacity.");
        _reservations.Add(new(LoanId, disbursementId, amount, Currency, at));
        ReservedDisbursementAmount += amount;
        return new(ReservationDecision.Reserved);
    }

    public void ChangeFinancingType(string newType, IReadOnlyCollection<string> allowedTypes, Guid actorUserId, string correlationId, DateTimeOffset at)
    {
        if (actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId)) throw new LoanAccountValidationException("audit");
        if (Status != LoanAccountStatus.Active || TotalDisbursed != 0 || ReservedDisbursementAmount != 0 || _reservations.Count != 0) throw new FinancingTypeChangeNotAllowedException();
        var normalized = newType?.Trim() ?? "";
        if (!allowedTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase)) throw new UnsupportedFinancingTypeException();
        if (string.Equals(FinancingType, normalized, StringComparison.OrdinalIgnoreCase)) return;
        var previous = FinancingType; FinancingType = allowedTypes.First(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        _financingTypeChanges.Add(new(Guid.NewGuid(), LoanId, actorUserId, previous, FinancingType, at, correlationId));
    }
}
public sealed class LoanAccountValidationException(string field) : Exception(field) { public string Field { get; } = field; }
public sealed class FinancingTypeChangeNotAllowedException : Exception;
public sealed class UnsupportedFinancingTypeException : Exception;
