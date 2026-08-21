# Loan Management System — Event Storming

## 1. Purpose

This document captures the first implementation-oriented Event Storming model for the Loan Management System MVP.

It translates the previously defined bounded contexts into:

- Commands
- Domain Events
- Policies / Process Managers
- Actors
- Aggregate ownership
- Cross-context integration events
- Important exception paths

This is a baseline for aggregate design, application use cases, API contracts, and implementation tasks.

---

## 2. Modeling Conventions

### Command

An explicit intention to change business state.

Examples:

- `SubmitLoanApplication`
- `ApproveApplicationByCommittee`
- `RequestDisbursement`
- `PostRepayment`

### Domain Event

A business fact that has already occurred.

Examples:

- `LoanApplicationSubmitted`
- `DisbursementCapacityReserved`
- `TreasuryPaymentCompleted`

### Policy / Process Manager

A reaction to an event that decides what should happen next.

Example:

```text
LoanApplicationApproved
        ↓
Open Loan Account
```

### External Event

An event originating outside the bounded context, such as:

- HR data import
- Excel salary deduction batch
- Bank/B2B payment response

---

## 3. End-to-End Happy Path

```mermaid
flowchart LR
    A[Register Borrower]
    B[Create Application]
    C[Evaluate Eligibility]
    D[Submit Application]
    E[Unit Approves]
    F[Committee Approves]
    G[Record Inspection]
    H[Approve Inspection]
    I[Complete Mortgage Readiness]
    J[Approve Application]
    K[Open Loan Account]
    L[Request Disbursement]
    M[Reserve Loan Capacity]
    N[Administrative Approvals]
    O[Create Treasury Payment]
    P[Treasury Approval]
    Q[Execute Payment]
    R[Confirm Disbursement]
    S[Post Repayment]
    T[Update Loan Balance]

    A --> B --> C --> D --> E --> F --> G --> H --> I --> J --> K --> L --> M --> N --> O --> P --> Q --> R --> S --> T
```

---

# 4. Borrowers Context

## 4.1 Register Borrower

| Element | Value |
|---|---|
| Actor | Loan Officer / Authorized User |
| Command | `RegisterBorrower` |
| Aggregate | `Borrower` |
| Event | `BorrowerRegistered` |

### Invariants

- Civil Number must be valid according to configured format.
- Civil Number must be unique.
- Employee Number must be unique when provided.
- Required identity/employment fields must be present.

---

## 4.2 Update Borrower

```text
UpdateBorrower
      ↓
BorrowerUpdated
```

Only master data is changed here.

Eligibility decisions are not recalculated automatically by the Borrowers context.

---

## 4.3 Import Borrowers

```text
UploadBorrowerExcel
       ↓
ValidateBorrowerImport
       ↓
BorrowerImportValidated
       ↓
ImportValidBorrowers
       ↓
BorrowerRegistered / BorrowerUpdated
```

Invalid rows produce import results rather than corrupting borrower master data.

The import process is an application workflow; the `Borrower` remains the primary domain aggregate.

---

# 5. Loan Products Context

## 5.1 Create Product

```text
CreateLoanProduct
      ↓
LoanProductCreated
```

## 5.2 Publish Product Version

```text
ConfigureLoanProduct
      ↓
PublishLoanProductVersion
      ↓
LoanProductVersionPublished
```

A published product version is treated as immutable for historical consistency.

Future edits create a new version.

---

## 5.3 Activate Product

```text
ActivateLoanProduct
      ↓
LoanProductActivated
```

## 5.4 Deactivate Product

```text
DeactivateLoanProduct
      ↓
LoanProductDeactivated
```

Existing applications referencing an older published version remain historically valid.

---

# 6. Loan Origination Context

## 6.1 Create Application

| Element | Value |
|---|---|
| Actor | Loan Officer |
| Command | `CreateLoanApplication` |
| Aggregate | `LoanApplication` |
| Event | `LoanApplicationCreated` |

Inputs include:

- Borrower reference
- Loan product/version reference
- Financing type
- Requested amount

The application captures required borrower/product snapshots as defined by business traceability needs.

---

## 6.2 Evaluate Eligibility

```text
EvaluateEligibility
      ↓
EligibilityEvaluated
```

Result:

```text
Eligible
```

or:

```text
Ineligible
```

The evaluation captures the rules/product version applied at the time of the decision.

---

## 6.3 Submit Application

```text
SubmitLoanApplication
      ↓
LoanApplicationSubmitted
```

### Preconditions

- Application is in Draft.
- Required data is complete.
- Applicable loan product version is valid.
- Eligibility result permits submission.
- Required initial documents are present.

---

## 6.4 Unit Approval

```text
ApproveApplicationByUnit
      ↓
UnitApprovalGranted
```

or:

```text
RejectApplicationByUnit
      ↓
LoanApplicationRejected
```

---

## 6.5 Committee Approval

```text
ApproveApplicationByCommittee
      ↓
CommitteeApprovalGranted
```

or:

```text
RejectApplicationByCommittee
      ↓
LoanApplicationRejected
```

---

## 6.6 Property Inspection

### Record Inspection

```text
RecordPropertyInspection
      ↓
PropertyInspectionRecorded
```

### Approve Inspection

```text
ApprovePropertyInspection
      ↓
PropertyInspectionApproved
```

### Reject Inspection

```text
RejectPropertyInspection
      ↓
PropertyInspectionRejected
```

### Policy

```text
PropertyInspectionApproved
      ↓
Mark application inspection prerequisite satisfied
```

An inspection rejection prevents final application approval until the business process explicitly allows a new inspection.

---

## 6.7 Mortgage Readiness

For the MVP, mortgage readiness is maintained within the Loan Origination context.

```text
MarkMortgageCompleted
      ↓
MortgageCompleted
```

If mortgage is not required:

```text
MarkMortgageNotRequired
      ↓
MortgageRequirementWaived
```

This is a business waiver/state, not an authorization bypass.

---

## 6.8 Final Application Approval

```text
ApproveLoanApplication
      ↓
LoanApplicationApproved
```

### Preconditions

- Unit approval completed.
- Committee approval completed.
- Required inspection approved.
- Required documents available.
- Mortgage requirement satisfied.
- Approved amount is valid.
- Application has not been rejected or cancelled.

---

## 6.9 Open Loan Account Policy

```text
LoanApplicationApproved
      ↓
[Policy]
      ↓
OpenLoanAccount
      ↓
LoanAccountOpened
```

`LoanApplication` does not transform into `LoanAccount`.

They are separate domain concepts in separate bounded contexts.

---

## 6.10 Cancel Application

```text
CancelLoanApplication
      ↓
LoanApplicationCancelled
```

Cancellation after an active loan/disbursement exists is not handled as simple application cancellation.

---

# 7. Loan Accounts Context

## 7.1 Open Account

```text
OpenLoanAccount
      ↓
LoanAccountOpened
```

Created from an approved application snapshot.

Initial values:

```text
ApprovedAmount = final approved amount
ReservedDisbursementAmount = 0
TotalDisbursed = 0
TotalRepaid = 0
```

---

## 7.2 Request Disbursement Capacity

When the Disbursements context creates a new request:

```text
DisbursementCapacityRequested
      ↓
[Policy]
      ↓
ReserveDisbursementCapacity
```

Possible outcomes:

```text
DisbursementCapacityReserved
```

or:

```text
DisbursementCapacityRejected
```

### Critical invariant

```text
TotalDisbursed
+ ReservedDisbursementAmount
+ NewReservation
<= ApprovedAmount
```

The Loan Account is the single owner of this invariant.

---

## 7.3 Release Capacity

If a disbursement is rejected/cancelled before payment:

```text
DisbursementCancelled
      ↓
ReleaseDisbursementCapacity
      ↓
DisbursementCapacityReleased
```

---

## 7.4 Confirm Paid Disbursement

```text
TreasuryPaymentCompleted
      ↓
ConfirmDisbursement
      ↓
LoanDisbursementConfirmed
```

Effect:

```text
ReservedDisbursementAmount -= payment amount
TotalDisbursed += payment amount
```

---

## 7.5 Apply Repayment

```text
RepaymentPosted
      ↓
ApplyRepayment
      ↓
LoanRepaymentApplied
```

Effect:

```text
TotalRepaid += repayment amount
```

If fully repaid:

```text
LoanFullyRepaid
```

A later explicit command may produce:

```text
LoanClosed
```

---

# 8. Disbursements Context

## 8.1 Request Disbursement

```text
RequestDisbursement
      ↓
DisbursementCreated
      ↓
DisbursementCapacityRequested
```

Initial state:

```text
PendingCapacity
```

---

## 8.2 Capacity Reservation Policy

### Success

```text
DisbursementCapacityReserved
      ↓
ActivateDisbursementRequest
      ↓
DisbursementRequested
```

### Failure

```text
DisbursementCapacityRejected
      ↓
RejectDisbursementForInsufficientCapacity
      ↓
DisbursementRejected
```

This avoids cross-module database access and avoids placing loan-capacity logic in the Disbursement aggregate.

---

## 8.3 Technical Approval

```text
ApproveDisbursementByTechnicalAffairs
      ↓
DisbursementTechnicalApprovalGranted
```

or rejection:

```text
RejectDisbursementByTechnicalAffairs
      ↓
DisbursementRejected
```

A rejection triggers capacity release.

---

## 8.4 Accounting Approval

```text
ApproveDisbursementByAccounting
      ↓
DisbursementAccountingApprovalGranted
```

or:

```text
RejectDisbursementByAccounting
      ↓
DisbursementRejected
```

---

## 8.5 Higher Officer Approval

```text
ApproveDisbursementByHigherOfficer
      ↓
DisbursementHigherApprovalGranted
      ↓
DisbursementReadyForTreasury
```

or:

```text
RejectDisbursementByHigherOfficer
      ↓
DisbursementRejected
```

---

## 8.6 Treasury Completion Feedback

```text
TreasuryPaymentCompleted
      ↓
MarkDisbursementCompleted
      ↓
DisbursementCompleted
```

Payment failure may place the disbursement in:

```text
PaymentFailed
```

while retaining its reservation if retry is allowed.

Permanent cancellation/rejection releases the reservation.

---

# 9. Treasury Context

## 9.1 Create Payment Order

Policy:

```text
DisbursementReadyForTreasury
      ↓
CreateTreasuryPayment
      ↓
TreasuryPaymentCreated
```

---

## 9.2 Treasury Input

```text
EnterTreasuryPayment
      ↓
TreasuryPaymentEntered
```

---

## 9.3 Treasury Audit

```text
AuditTreasuryPayment
      ↓
TreasuryPaymentAudited
```

or:

```text
RejectTreasuryPayment
      ↓
TreasuryPaymentRejected
```

---

## 9.4 Treasury Approval

```text
ApproveTreasuryPayment
      ↓
TreasuryPaymentApproved
```

---

## 9.5 Execute Payment

```text
ExecuteTreasuryPayment
      ↓
PaymentExecutionRequested
```

The Treasury context calls:

```text
IPaymentGateway
```

### Successful external response

```text
TreasuryPaymentCompleted
```

### Failed external response

```text
TreasuryPaymentFailed
```

A failed payment may be retried according to explicit retry/idempotency rules.

---

# 10. Repayments Context

## 10.1 Manual Repayment

```text
RecordManualRepayment
      ↓
RepaymentRecorded
      ↓
ValidateRepayment
      ↓
RepaymentPosted
```

### Minimum checks

- amount > 0;
- reference/receipt requirements met;
- target loan exists and can accept repayment;
- duplicate repayment is rejected;
- posting must not violate the configured overpayment policy.

For the MVP, the default policy is:

```text
Repayment must not make Outstanding Balance negative.
```

---

## 10.2 Salary Deduction Batch

```text
UploadSalaryDeductionBatch
      ↓
ValidateSalaryDeductionBatch
      ↓
SalaryDeductionBatchValidated
```

For each valid row:

```text
CreateSalaryDeductionRepayment
      ↓
RepaymentPosted
```

For invalid rows:

```text
SalaryDeductionRowRejected
```

The batch maintains a summary of successful, failed, duplicate, and unmatched rows.

---

# 11. Documents Context

Typical flow:

```text
UploadDocument
      ↓
DocumentStored
      ↓
DocumentAttachedToBusinessRecord
```

Business contexts reference `DocumentId`.

Deleting/replacing documents must respect business retention and audit rules.

---

# 12. Audit Context

The Audit context consumes important events:

```text
LoanApplicationSubmitted
LoanApplicationApproved
LoanApplicationRejected
LoanApplicationCancelled

DisbursementRequested
DisbursementRejected
DisbursementReadyForTreasury
DisbursementCompleted

TreasuryPaymentApproved
TreasuryPaymentCompleted
TreasuryPaymentFailed

RepaymentPosted

LoanFullyRepaid
LoanClosed
```

and creates:

```text
AuditEntryRecorded
```

Audit failure must be operationally visible; business-event delivery should be reliable.

---

# 13. Reporting Context

Reporting consumes integration events or read-model updates.

Examples:

```text
LoanApplicationApproved
      ↓
Update Application Report Projection

LoanDisbursementConfirmed
      ↓
Update Loan Financial Projection

LoanRepaymentApplied
      ↓
Update Outstanding Balance Projection
```

Reporting must never become the source of truth for transactional business state.

---

# 14. Primary Integration Events

The initial cross-context event vocabulary is:

```text
BorrowerRegistered

LoanProductVersionPublished

LoanApplicationSubmitted
LoanApplicationRejected
LoanApplicationCancelled
LoanApplicationApproved

LoanAccountOpened

DisbursementCapacityRequested
DisbursementCapacityReserved
DisbursementCapacityRejected
DisbursementCapacityReleased

DisbursementRequested
DisbursementRejected
DisbursementReadyForTreasury
DisbursementCompleted

TreasuryPaymentCreated
TreasuryPaymentApproved
TreasuryPaymentCompleted
TreasuryPaymentFailed
TreasuryPaymentRejected

RepaymentPosted
LoanRepaymentApplied

LoanFullyRepaid
LoanClosed
```

Not every domain event must be exposed as an integration event.

Only events required outside the owning bounded context should be published across module boundaries.

---

# 15. Important Policies / Process Managers

## 15.1 Origination-to-Loan Policy

```text
LoanApplicationApproved
      ↓
OpenLoanAccount
```

## 15.2 Disbursement Capacity Policy

```text
DisbursementCreated
      ↓
Request loan capacity reservation
      ↓
Reserved / Rejected
```

## 15.3 Disbursement-to-Treasury Policy

```text
DisbursementReadyForTreasury
      ↓
CreateTreasuryPayment
```

## 15.4 Treasury Completion Policy

```text
TreasuryPaymentCompleted
      ├──► Confirm disbursement on LoanAccount
      └──► Mark Disbursement completed
```

## 15.5 Repayment Posting Policy

```text
RepaymentPosted
      ↓
Apply repayment to LoanAccount
```

## 15.6 Capacity Release Policy

```text
DisbursementRejected / Cancelled
      ↓
Release reserved loan capacity
```

---

# 16. Consistency Model

## Strong Consistency

Strong consistency is required inside an aggregate transaction.

Examples:

- application state transition;
- loan capacity reservation;
- treasury payment state transition;
- repayment posting state.

## Eventual Consistency

Cross-bounded-context updates may be eventually consistent.

Examples:

- approved application creating a LoanAccount;
- completed treasury payment updating LoanAccount;
- repayment posting updating LoanAccount;
- reporting projections;
- audit projection.

No Kafka or external message broker is required for the MVP.

Reliable in-process module integration with persisted Outbox/Inbox semantics is sufficient.

---

# 17. Failure and Recovery Requirements

Cross-context event handling must be:

- idempotent;
- retryable;
- traceable;
- safe against duplicate delivery.

The system must avoid silent loss of:

- application approval events;
- capacity reservation events;
- payment completion events;
- repayment posting events.

A persisted Outbox/Inbox pattern is the preferred implementation baseline for financial/business-critical integration events.

---

# 18. Open Business Questions

The following items are not blockers for coding the domain skeleton but must be confirmed before production completion:

1. Exact eligibility rules for every loan product.
2. Whether repayment overpayment is forbidden, allowed, or refunded.
3. Whether failed treasury payments retain reserved capacity indefinitely or for a configurable period.
4. Whether rejected inspections can be resubmitted under the same application.
5. Exact mortgage prerequisites by loan product.
6. Exact cancellation rules after approval but before first disbursement.
7. Whether partial repayments/disbursements have minimum amounts.
8. Whether any approval step supports "return for correction" in addition to approve/reject.

These should be represented as explicit domain policies rather than assumptions hidden in UI or controllers.

---

# 19. Baseline

This Event Storming model is the baseline for:

- Aggregate Design
- State Machines
- Module APIs
- Integration Events
- Process Managers
- Outbox/Inbox behavior
- Implementation task breakdown
