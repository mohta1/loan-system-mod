using LoanSystem.Modules.Disbursements.Domain;
using LoanSystem.Modules.LoanAccounts.Domain;

namespace LoanSystem.DomainTests;

public sealed class Task12DomainTests
{
    private static LoanAccount Loan(decimal amount = 100000m) => LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "OMR", "Build");
    [Fact]
    public void Reservation_enforces_capacity_currency_amount_and_duplicates()
    {
        var loan = Loan(); var id = Guid.NewGuid();
        Assert.Equal(ReservationDecision.Rejected, loan.ReserveDisbursementCapacity(Guid.NewGuid(), 0, "OMR", DateTimeOffset.UtcNow).Decision);
        Assert.Equal("capacity.currencyMismatch", loan.ReserveDisbursementCapacity(Guid.NewGuid(), 1, "USD", DateTimeOffset.UtcNow).ReasonCode);
        Assert.Equal(ReservationDecision.Reserved, loan.ReserveDisbursementCapacity(id, 70000, "OMR", DateTimeOffset.UtcNow).Decision);
        Assert.Equal(30000, loan.AvailableToDisburse); Assert.Equal(70000, loan.ReservedDisbursementAmount);
        Assert.Equal(ReservationDecision.Duplicate, loan.ReserveDisbursementCapacity(id, 70000, "OMR", DateTimeOffset.UtcNow).Decision);
        Assert.Equal("capacity.inconsistentDuplicate", loan.ReserveDisbursementCapacity(id, 50000, "OMR", DateTimeOffset.UtcNow).ReasonCode);
        Assert.Equal("capacity.insufficient", loan.ReserveDisbursementCapacity(Guid.NewGuid(), 50000, "OMR", DateTimeOffset.UtcNow).ReasonCode);
        Assert.True(loan.TotalDisbursed + loan.ReservedDisbursementAmount <= loan.ApprovedAmount); Assert.Single(loan.Reservations);
    }
    [Fact]
    public void Inactive_loan_rejects_reservation()
    {
        var loan = Loan(); Set(loan, nameof(LoanAccount.Status), LoanAccountStatus.Closed);
        Assert.Equal("capacity.loanInactive", loan.ReserveDisbursementCapacity(Guid.NewGuid(), 1, "OMR", DateTimeOffset.UtcNow).ReasonCode);
    }
    [Fact]
    public void Financing_type_change_is_audited_and_keeps_approval()
    {
        var loan = Loan(); var actor = Guid.NewGuid(); loan.ChangeFinancingType("Purchase", ["Build", "Purchase"], actor, "corr", DateTimeOffset.UtcNow);
        Assert.Equal("Purchase", loan.FinancingType); Assert.Equal(100000, loan.ApprovedAmount); var history = Assert.Single(loan.FinancingTypeChanges); Assert.Equal(actor, history.ActorUserId); Assert.Equal("Build", history.PreviousFinancingType);
        Assert.Throws<UnsupportedFinancingTypeException>(() => loan.ChangeFinancingType("Other", ["Build", "Purchase"], actor, "corr", DateTimeOffset.UtcNow));
    }
    [Fact]
    public void Financing_type_is_locked_by_reservation_or_disbursement()
    {
        var reserved = Loan(); reserved.ReserveDisbursementCapacity(Guid.NewGuid(), 1, "OMR", DateTimeOffset.UtcNow); Assert.Throws<FinancingTypeChangeNotAllowedException>(() => reserved.ChangeFinancingType("Purchase", ["Purchase"], Guid.NewGuid(), "corr", DateTimeOffset.UtcNow));
        var paid = Loan(); Set(paid, nameof(LoanAccount.TotalDisbursed), 1m); Assert.Throws<FinancingTypeChangeNotAllowedException>(() => paid.ChangeFinancingType("Purchase", ["Purchase"], Guid.NewGuid(), "corr", DateTimeOffset.UtcNow));
    }
    [Fact]
    public void Disbursement_transitions_and_duplicate_responses_are_safe()
    {
        var id = Guid.NewGuid(); var loan = Guid.NewGuid(); var at = DateTimeOffset.UtcNow; var beneficiary = new BeneficiarySnapshot(BeneficiaryType.Contractor, "Builder", "Builder LLC", "Bank", "ACCOUNT");
        var value = Disbursement.Create(id, loan, 25, "omr", beneficiary, [Guid.NewGuid()], Guid.NewGuid(), "corr", "token", at); Assert.Equal(DisbursementStatus.PendingCapacity, value.Status); Assert.Equal(CapacityReservationStatus.Pending, value.CapacityReservationStatus);
        value.CapacityReserved(loan, 25, at.AddMinutes(1)); value.CapacityReserved(loan, 25, at.AddMinutes(2)); Assert.Equal(DisbursementStatus.Requested, value.Status); Assert.Throws<DisbursementResponseConflictException>(() => value.CapacityRejected(loan, 25, "x", "x", at)); Assert.Throws<DisbursementResponseConflictException>(() => value.CapacityReserved(Guid.NewGuid(), 25, at));
        var rejected = Disbursement.Create(Guid.NewGuid(), loan, 30, "OMR", beneficiary, [], Guid.NewGuid(), "corr", "token2", at); rejected.CapacityRejected(loan, 30, "capacity.insufficient", "Insufficient", at); rejected.CapacityRejected(loan, 30, "capacity.insufficient", "Insufficient", at); Assert.Equal(DisbursementStatus.Rejected, rejected.Status); Assert.Throws<DisbursementResponseConflictException>(() => rejected.CapacityRejected(loan, 30, "other", "Other", at));
    }
    [Fact]
    public void Disbursement_rejects_invalid_amount_and_beneficiary()
    {
        var beneficiary = new BeneficiarySnapshot(BeneficiaryType.PropertyOwner, "Owner", "Owner", "Bank", "Account");
        Assert.Throws<DisbursementValidationException>(() => Disbursement.Create(Guid.NewGuid(), Guid.NewGuid(), 0, "OMR", beneficiary, [], Guid.NewGuid(), "c", "t", DateTimeOffset.UtcNow));
        Assert.Throws<DisbursementValidationException>(() => Disbursement.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "OMR", beneficiary with { BankName = "" }, [], Guid.NewGuid(), "c", "t", DateTimeOffset.UtcNow));
    }
    [Fact]
    public void Disbursement_rejects_invalid_identity_currency_documents_and_response_data()
    {
        var loanId = Guid.NewGuid(); var actor = Guid.NewGuid(); var at = DateTimeOffset.UtcNow;
        var beneficiary = new BeneficiarySnapshot(BeneficiaryType.Contractor, " Builder ", " Holder ", " Bank ", " Account ");
        Assert.Throws<DisbursementValidationException>(() => Disbursement.Create(Guid.Empty, loanId, 1, "OMR", beneficiary, [], actor, "c", "t", at));
        Assert.Throws<DisbursementValidationException>(() => Disbursement.Create(Guid.NewGuid(), loanId, 1, "OM", beneficiary, [], actor, "c", "t", at));
        Assert.Throws<DisbursementValidationException>(() => Disbursement.Create(Guid.NewGuid(), loanId, 1, "OMR", beneficiary, [Guid.Empty], actor, "c", "t", at));

        var value = Disbursement.Create(Guid.NewGuid(), loanId, 10, " omr ", beneficiary, [], actor, "c", "t", at);
        Assert.Equal("OMR", value.Currency);
        Assert.Equal("Builder", value.Beneficiary.DisplayName);
        Assert.Throws<DisbursementResponseConflictException>(() => value.CapacityReserved(loanId, 9, at));
        Assert.Throws<DisbursementResponseConflictException>(() => value.CapacityRejected(loanId, 10, "", "reason", at));
    }
    private static void Set<T>(LoanAccount loan, string property, T value) => typeof(LoanAccount).GetProperty(property)!.SetValue(loan, value);
}
