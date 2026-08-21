# Loan Management System — Application Use Cases

## 1. Purpose

This document translates the domain model into implementation-oriented application use cases for the MVP.

It defines:
- application commands and queries;
- actors and permissions;
- preconditions;
- aggregate ownership;
- expected outputs;
- important failure cases;
- cross-module side effects.

This document is the primary baseline for application handlers and later implementation tasks.

## 2. Application Layer Style

The MVP uses **lightweight CQRS**:

- Commands mutate state.
- Queries return read models.
- Each use case has one explicit handler.
- MediatR is optional and is not an architectural requirement.
- Handlers orchestrate repositories, domain aggregates, authorization, transactions, and module contracts.
- Business rules remain inside domain objects/policies whenever possible.
- Controllers/endpoints remain thin.

Recommended conceptual structure:

```text
Use Case
  ├── Command / Query
  ├── Handler
  ├── Validator
  ├── Result DTO
  └── Permission
```

## 3. Cross-Cutting Application Rules

Every state-changing use case must consider:

1. Authentication.
2. Permission authorization.
3. Input validation.
4. Aggregate loading.
5. Optimistic concurrency.
6. Domain invariant enforcement.
7. Transaction commit.
8. Domain/integration event persistence.
9. Audit correlation.
10. Idempotency where required.

Cross-module state changes must not directly modify another module's database.

---

# 4. Identity & Access

## UC-IAM-001 — Login
**Actor:** Internal User  
**Input:** username + password/development credential  
**Output:** authenticated session/token, current identity, roles/permissions  
**Failures:** invalid credentials, disabled user

## UC-IAM-002 — Get Current User
**Query:** `GetCurrentUser`

Returns:
- UserId
- DisplayName
- Roles
- Permissions

## UC-IAM-003 — Create User
**Permission:** `identity.users.manage`

## UC-IAM-004 — Assign Roles
**Permission:** `identity.users.manage`

## UC-IAM-005 — Activate / Deactivate User
**Permission:** `identity.users.manage`

---

# 5. Borrowers

## UC-BOR-001 — Register Borrower
**Permission:** `borrowers.create`  
**Aggregate:** `Borrower`

Input:
- Civil Number
- Employee Number
- Full Name
- Phone
- Organization
- Rank / Grade
- Employment Information

Failures:
- duplicate Civil Number;
- duplicate Employee Number;
- invalid required data.

## UC-BOR-002 — Update Borrower
**Permission:** `borrowers.update`

Failures:
- not found;
- invalid data;
- concurrency conflict.

## UC-BOR-003 — Get Borrower
**Permission:** `borrowers.read`

## UC-BOR-004 — Search Borrowers
**Permission:** `borrowers.read`

Filters:
- Civil Number
- Employee Number
- Name
- Organization
- Status

## UC-BOR-005 — Deactivate Borrower
**Permission:** `borrowers.manageStatus`

Deactivation affects new application creation, not historical applications/loans.

## UC-BOR-006 — Validate Borrower Excel Import
**Permission:** `borrowers.import`

Produces:
- total rows;
- valid rows;
- invalid rows;
- duplicates;
- validation messages.

## UC-BOR-007 — Execute Borrower Excel Import
**Permission:** `borrowers.import`

Execution must be idempotent by import batch identifier.

---

# 6. Loan Products

## UC-LPR-001 — Create Loan Product
**Permission:** `loanProducts.manage`

## UC-LPR-002 — Create Draft Product Version
**Permission:** `loanProducts.manage`

Defines:
- Maximum Amount
- Deduction Percentage
- Financing Types
- Eligibility Configuration
- Effective Dates

## UC-LPR-003 — Publish Product Version
**Permission:** `loanProducts.publish`

A published version is immutable.

## UC-LPR-004 — Activate Product
**Permission:** `loanProducts.manageStatus`

## UC-LPR-005 — Deactivate Product
**Permission:** `loanProducts.manageStatus`

## UC-LPR-006 — List Available Product Versions
**Permission:** `loanProducts.read`

---

# 7. Loan Origination

## UC-LOR-001 — Create Loan Application
**Permission:** `loanApplications.create`  
**Aggregate:** `LoanApplication`

Input:
- BorrowerId
- LoanProductVersionId
- FinancingType
- RequestedAmount

Cross-module reads:
- Borrower snapshot from Borrowers
- Product/version snapshot from Loan Products

Output:
- LoanApplicationId
- Draft application

Failures:
- borrower not found/inactive;
- product version unavailable;
- invalid requested amount.

## UC-LOR-002 — Update Draft Application
**Permission:** `loanApplications.update`

Only Draft applications are freely editable.

## UC-LOR-003 — Evaluate Eligibility
**Permission:** `loanApplications.evaluateEligibility`

Output:
- Eligible / Ineligible
- Decision reasons
- Applied rule/product version
- Suggested/allowed amount where applicable

Eligibility is persisted as a historical decision.

## UC-LOR-004 — Attach Application Document
**Permission:** `loanApplications.update`

Stores a `DocumentId` reference from Documents.

## UC-LOR-005 — Submit Loan Application
**Permission:** `loanApplications.submit`

Preconditions:
- Draft;
- required fields complete;
- Eligible;
- required initial documents present;
- product snapshot valid.

Event:
`LoanApplicationSubmitted`

## UC-LOR-006 — Unit Approve Application
**Permission:** `loanApplications.unitApprove`

Precondition:
`Submitted`

## UC-LOR-007 — Unit Reject Application
**Permission:** `loanApplications.unitApprove`

Requires rejection reason.

## UC-LOR-008 — Committee Approve Application
**Permission:** `loanApplications.committeeApprove`

Precondition:
Unit approval completed.

## UC-LOR-009 — Committee Reject Application
**Permission:** `loanApplications.committeeApprove`

Requires rejection reason.

## UC-LOR-010 — Record Property Inspection
**Permission:** `inspections.create`  
**Aggregate:** `PropertyInspection`

## UC-LOR-011 — Approve Property Inspection
**Permission:** `inspections.approve`

Updates the related application prerequisite through a local policy.

## UC-LOR-012 — Reject Property Inspection
**Permission:** `inspections.approve`

Requires reason/result.

## UC-LOR-013 — Mark Mortgage Completed
**Permission:** `loanApplications.mortgageManage`

## UC-LOR-014 — Mark Mortgage Not Required
**Permission:** `loanApplications.mortgageManage`

Only when business policy allows it.

## UC-LOR-015 — Final Approve Loan Application
**Permission:** `loanApplications.finalApprove`

Preconditions:
- Unit approval complete;
- Committee approval complete;
- required inspection approved;
- required documents available;
- mortgage prerequisite satisfied;
- approved amount valid.

Event:
`LoanApplicationApproved`

Policy:
Loan Accounts opens a separate `LoanAccount`.

## UC-LOR-016 — Cancel Loan Application
**Permission:** `loanApplications.cancel`

Simple MVP cancellation applies only before irreversible financial obligation.

## UC-LOR-017 — Get Loan Application
**Permission:** `loanApplications.read`

Returns:
- borrower snapshot;
- product snapshot;
- eligibility;
- approvals;
- inspection state;
- mortgage state;
- documents;
- history.

## UC-LOR-018 — Search Loan Applications
**Permission:** `loanApplications.read`

---

# 8. Loan Accounts

Loan Accounts is primarily mutated through integration events.

## UC-LAC-001 — Open Loan Account
**Trigger:** `LoanApplicationApproved`

Idempotent by `LoanApplicationId`.

## UC-LAC-002 — Get Loan Account
**Permission:** `loans.read`

Returns:
- Approved Amount
- Reserved Amount
- Total Disbursed
- Available To Disburse
- Total Repaid
- Outstanding Balance
- Status

## UC-LAC-003 — Search Loan Accounts
**Permission:** `loans.read`

## UC-LAC-004 — Reserve Disbursement Capacity
**Trigger:** `DisbursementCapacityRequested`

Outcome:
- `DisbursementCapacityReserved`
- or `DisbursementCapacityRejected`

`LoanAccount` is the authoritative owner of this invariant.

## UC-LAC-005 — Release Disbursement Capacity
**Trigger:** disbursement rejected/cancelled

Idempotent by `DisbursementId`.

## UC-LAC-006 — Confirm Disbursement
**Trigger:** `TreasuryPaymentCompleted`

Moves reservation to confirmed disbursed amount.

## UC-LAC-007 — Apply Repayment
**Trigger:** `RepaymentPosted`

Idempotent by `RepaymentId`.

## UC-LAC-008 — Close Loan
**Permission:** `loans.close`

Only when eligible for closure.

---

# 9. Disbursements

## UC-DIS-001 — Request Disbursement
**Permission:** `disbursements.create`  
**Aggregate:** `Disbursement`

Input:
- LoanId
- Amount
- Beneficiary
- Supporting Document IDs

Initial state:
`PendingCapacity`

Event:
`DisbursementCapacityRequested`

Idempotency:
Required.

## UC-DIS-002 — Handle Capacity Reserved
**Trigger:** `DisbursementCapacityReserved`

Moves to `Requested`.

## UC-DIS-003 — Handle Capacity Rejected
**Trigger:** `DisbursementCapacityRejected`

Moves to `Rejected`.

## UC-DIS-004 — Technical Approve
**Permission:** `disbursements.technicalApprove`

## UC-DIS-005 — Technical Reject
**Permission:** `disbursements.technicalApprove`

Releases capacity.

## UC-DIS-006 — Accounting Approve
**Permission:** `disbursements.accountingApprove`

## UC-DIS-007 — Accounting Reject
**Permission:** `disbursements.accountingApprove`

Releases capacity.

## UC-DIS-008 — Higher Officer Approve
**Permission:** `disbursements.finalApprove`

Event:
`DisbursementReadyForTreasury`

## UC-DIS-009 — Higher Officer Reject
**Permission:** `disbursements.finalApprove`

Releases capacity.

## UC-DIS-010 — Cancel Disbursement
**Permission:** `disbursements.cancel`

Only before successful payment.

## UC-DIS-011 — Get Disbursement
**Permission:** `disbursements.read`

## UC-DIS-012 — Search Disbursements
**Permission:** `disbursements.read`

---

# 10. Treasury

## UC-TRY-001 — Create Treasury Payment
**Trigger:** `DisbursementReadyForTreasury`

Idempotent by DisbursementId.

## UC-TRY-002 — Enter Treasury Payment
**Permission:** `treasury.input`

## UC-TRY-003 — Audit Treasury Payment
**Permission:** `treasury.audit`

Maker/auditor separation may be enforced.

## UC-TRY-004 — Reject During Audit
**Permission:** `treasury.audit`

Requires reason.

## UC-TRY-005 — Final Approve Treasury Payment
**Permission:** `treasury.approve`

## UC-TRY-006 — Execute Treasury Payment
**Permission:** `treasury.execute`

External dependency:
`IPaymentGateway`

Idempotency:
Mandatory.

Events:
- `TreasuryPaymentCompleted`
- `TreasuryPaymentFailed`

## UC-TRY-007 — Retry Failed Payment
**Permission:** `treasury.execute`

Must not create a second logical payment.

## UC-TRY-008 — Get Treasury Payment
**Permission:** `treasury.read`

## UC-TRY-009 — Search Treasury Payments
**Permission:** `treasury.read`

---

# 11. Repayments

## UC-REP-001 — Record Manual Repayment
**Permission:** `repayments.create`

Input:
- LoanId
- Amount
- PaymentDate
- ExternalReference
- ReceiptDocumentId
- Notes

Idempotency:
Mandatory.

## UC-REP-002 — Validate Manual Repayment

Checks:
- loan exists;
- amount valid;
- duplicate reference absent;
- loan can accept payment;
- overpayment policy.

## UC-REP-003 — Post Repayment

Event:
`RepaymentPosted`

Posting is immutable.

## UC-REP-004 — Upload Salary Deduction Batch
**Permission:** `repayments.import`

Idempotency required.

## UC-REP-005 — Validate Salary Deduction Batch
**Permission:** `repayments.import`

## UC-REP-006 — Post Salary Deduction Batch
**Permission:** `repayments.import`

Valid rows create independent repayments.

## UC-REP-007 — Get Repayment
**Permission:** `repayments.read`

## UC-REP-008 — Search Repayments
**Permission:** `repayments.read`

---

# 12. Documents

## UC-DOC-001 — Upload Document
Returns DocumentId and metadata.

## UC-DOC-002 — Download / Read Document
Authorization must include linked business-resource permission.

## UC-DOC-003 — Delete / Replace Document
Only where retention/business rules permit.

---

# 13. Audit

## UC-AUD-001 — Search Audit Trail
**Permission:** `audit.read`

Filters:
- Actor
- Entity type
- Entity ID
- Action
- Date range
- Correlation ID

Audit is read-only.

---

# 14. Reporting

## UC-RPT-001 — Application Report
## UC-RPT-002 — Active Loan Report
## UC-RPT-003 — Disbursement Report
## UC-RPT-004 — Repayment Report
## UC-RPT-005 — Outstanding Balance Report

**Permission:** `reports.read`

---

# 15. Permission Catalog

```text
identity.users.manage

borrowers.read
borrowers.create
borrowers.update
borrowers.manageStatus
borrowers.import

loanProducts.read
loanProducts.manage
loanProducts.publish
loanProducts.manageStatus

loanApplications.read
loanApplications.create
loanApplications.update
loanApplications.evaluateEligibility
loanApplications.submit
loanApplications.unitApprove
loanApplications.committeeApprove
loanApplications.mortgageManage
loanApplications.finalApprove
loanApplications.cancel

inspections.create
inspections.approve

loans.read
loans.close

disbursements.read
disbursements.create
disbursements.technicalApprove
disbursements.accountingApprove
disbursements.finalApprove
disbursements.cancel

treasury.read
treasury.input
treasury.audit
treasury.approve
treasury.execute

repayments.read
repayments.create
repayments.import

audit.read
reports.read
```

Roles group permissions; business code should not hard-code role names.

---

# 16. Maker-Checker Separation

Where required, enforce:

```text
Treasury Input Actor != Treasury Audit Actor
Treasury Audit Actor != Treasury Final Approver
```

Potentially:

```text
Disbursement Creator != Final Administrative Approver
```

Exact organizational rules remain configurable.

---

# 17. Use Case Result Model

Expected application results:

```text
Success
ValidationFailure
NotFound
Forbidden
Conflict
BusinessRuleViolation
ConcurrencyConflict
IdempotencyConflict
```

Expected business failures should not be represented as generic server exceptions.

---

# 18. Implementation Baseline

Each implementation use case should contain:

```text
Command/Query
Handler
Validator
Permission
DTO/Result
Tests
```

The `UC-*` identifiers should remain in later task/traceability documents.
