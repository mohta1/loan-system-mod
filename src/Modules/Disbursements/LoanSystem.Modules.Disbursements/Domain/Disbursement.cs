namespace LoanSystem.Modules.Disbursements.Domain;

public enum DisbursementStatus { PendingCapacity, Requested, Rejected }
public enum CapacityReservationStatus { Pending, Reserved, Rejected }
public enum BeneficiaryType { Contractor, PropertyOwner }
public sealed record BeneficiarySnapshot(BeneficiaryType Type, string DisplayName, string AccountHolderName, string BankName, string BankAccountIdentifier);
public sealed class DisbursementDocument
{
    private DisbursementDocument() { }
    internal DisbursementDocument(Guid disbursementId, Guid documentId) { DisbursementId = disbursementId; DocumentId = documentId; }
    public Guid DisbursementId { get; private set; }
    public Guid DocumentId { get; private set; }
}
public sealed class Disbursement
{
    private readonly List<DisbursementDocument> _supportingDocuments = [];
    private Disbursement() { }
    public Guid DisbursementId { get; private set; }
    public Guid LoanId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = ""; public BeneficiarySnapshot Beneficiary { get; private set; } = null!; public DisbursementStatus Status { get; private set; }
    public CapacityReservationStatus CapacityReservationStatus { get; private set; }
    public string? CapacityRejectionReasonCode { get; private set; }
    public string? CapacityRejectionReason { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = ""; public string CreationTokenHash { get; private set; } = ""; public byte[] RowVersion { get; private set; } = []; public IReadOnlyList<DisbursementDocument> SupportingDocuments => _supportingDocuments;
    public static Disbursement Create(Guid id, Guid loanId, decimal amount, string currency, BeneficiarySnapshot beneficiary, IEnumerable<Guid> documents, Guid actor, string correlationId, string creationTokenHash, DateTimeOffset at)
    {
        if (id == Guid.Empty || loanId == Guid.Empty || actor == Guid.Empty) throw new DisbursementValidationException("identity");
        if (amount <= 0) throw new DisbursementValidationException("amount");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) throw new DisbursementValidationException("currency");
        if (beneficiary is null || string.IsNullOrWhiteSpace(beneficiary.DisplayName) || string.IsNullOrWhiteSpace(beneficiary.AccountHolderName) || string.IsNullOrWhiteSpace(beneficiary.BankName) || string.IsNullOrWhiteSpace(beneficiary.BankAccountIdentifier)) throw new DisbursementValidationException("beneficiary");
        var value = new Disbursement { DisbursementId = id, LoanId = loanId, Amount = amount, Currency = currency.Trim().ToUpperInvariant(), Beneficiary = beneficiary with { DisplayName = beneficiary.DisplayName.Trim(), AccountHolderName = beneficiary.AccountHolderName.Trim(), BankName = beneficiary.BankName.Trim(), BankAccountIdentifier = beneficiary.BankAccountIdentifier.Trim() }, ActorUserId = actor, CorrelationId = correlationId, CreationTokenHash = creationTokenHash, Status = DisbursementStatus.PendingCapacity, CapacityReservationStatus = CapacityReservationStatus.Pending, CreatedAtUtc = at, UpdatedAtUtc = at };
        foreach (var document in documents.Distinct()) { if (document == Guid.Empty) throw new DisbursementValidationException("documents"); value._supportingDocuments.Add(new(id, document)); }
        return value;
    }
    public void CapacityReserved(Guid loanId, decimal amount, DateTimeOffset at)
    {
        EnsureMatch(loanId, amount); if (CapacityReservationStatus == CapacityReservationStatus.Reserved) return; if (CapacityReservationStatus != CapacityReservationStatus.Pending) throw new DisbursementResponseConflictException(); Status = DisbursementStatus.Requested; CapacityReservationStatus = CapacityReservationStatus.Reserved; UpdatedAtUtc = at;
    }
    public void CapacityRejected(Guid loanId, decimal amount, string reasonCode, string reason, DateTimeOffset at)
    {
        EnsureMatch(loanId, amount); if (CapacityReservationStatus == CapacityReservationStatus.Rejected) { if (CapacityRejectionReasonCode != reasonCode) throw new DisbursementResponseConflictException(); return; }
        if (CapacityReservationStatus != CapacityReservationStatus.Pending || string.IsNullOrWhiteSpace(reasonCode) || string.IsNullOrWhiteSpace(reason)) throw new DisbursementResponseConflictException(); Status = DisbursementStatus.Rejected; CapacityReservationStatus = CapacityReservationStatus.Rejected; CapacityRejectionReasonCode = reasonCode; CapacityRejectionReason = reason; UpdatedAtUtc = at;
    }
    private void EnsureMatch(Guid loanId, decimal amount) { if (LoanId != loanId || Amount != amount) throw new DisbursementResponseConflictException(); }
}
public sealed class DisbursementValidationException(string field) : Exception(field) { public string Field { get; } = field; }
public sealed class DisbursementResponseConflictException : Exception;
