# Loan Management System — Persistence Design

## 1. Purpose

This document defines the SQL Server and EF Core persistence baseline for the MVP while preserving DDD module boundaries, concurrency, idempotency, auditability, and reliable integration events.

## 2. Database Strategy

Use:

```text
One SQL Server database
+ one schema per module
+ one DbContext per module
```

Schemas:

```text
identity
borrowers
loan_products
loan_origination
loan_accounts
disbursements
treasury
repayments
documents
audit
reporting
platform
```

`platform` is reserved for host-wide technical state such as HTTP idempotency. Business tables never belong there.

## 3. DbContexts

```text
IdentityAccessDbContext
BorrowersDbContext
LoanProductsDbContext
LoanOriginationDbContext
LoanAccountsDbContext
DisbursementsDbContext
TreasuryDbContext
RepaymentsDbContext
DocumentsDbContext
AuditDbContext
ReportingDbContext
```

Rules:

- a DbContext owns only its module schema;
- no cross-module `DbSet`;
- no cross-module navigation property;
- no direct write to another module's tables.

## 4. Cross-Module Foreign Keys

Do **not** create SQL foreign keys across module schemas for business references.

Example:

```text
loan_origination.loan_applications.borrower_id
```

is a logical reference to Borrowers, not a relational FK to `borrowers.borrowers`.

Reasons:

- preserve bounded-context ownership;
- avoid persistence coupling;
- keep future module extraction possible;
- avoid cross-module cascades.

Within one module/schema, normal FKs are encouraged.

## 5. IDs

Use application-generated `Guid` identifiers persisted as:

```text
uniqueidentifier
```

Domain code may wrap them as strongly typed IDs:

```text
BorrowerId
LoanApplicationId
LoanId
DisbursementId
TreasuryPaymentId
RepaymentId
```

## 6. Money

Use:

```text
decimal(19,4)
```

for monetary columns unless the organization later specifies a different standard.

Currency:

```text
char(3)
```

Money is never stored as `float` or `double`.

## 7. Time

System timestamps:

```text
datetime2
```

stored in UTC.

Business-only dates such as effective/payment date may use:

```text
date
```

## 8. Optimistic Concurrency

Use SQL Server `rowversion` on contested mutable aggregates, including:

- LoanApplication
- LoanAccount
- Disbursement
- TreasuryPayment
- Repayment
- LoanProduct configuration

EF Core maps it as a concurrency token. A stale update becomes an application concurrency conflict and API `412 Precondition Failed` where applicable.

---

# 9. Borrowers

## `borrowers.borrowers`

```text
borrower_id             uniqueidentifier PK
civil_number            nvarchar(50) NOT NULL
employee_number         nvarchar(50) NULL
full_name               nvarchar(250) NOT NULL
phone_number            nvarchar(50) NULL
organization            nvarchar(200) NULL
rank_grade              nvarchar(100) NULL
employment_info         nvarchar(max) NULL
status                  nvarchar(30) NOT NULL
created_at_utc          datetime2 NOT NULL
updated_at_utc          datetime2 NOT NULL
row_version             rowversion NOT NULL
```

Indexes:

```text
UNIQUE civil_number
UNIQUE employee_number WHERE employee_number IS NOT NULL
INDEX full_name
INDEX organization
INDEX status
```

## Borrower Imports

`borrowers.import_batches`

```text
batch_id
source_document_id
status
total_rows
valid_rows
invalid_rows
created_by
created_at_utc
completed_at_utc
idempotency_key
```

`borrowers.import_rows`

```text
batch_id
row_number
raw_payload
status
error_code
error_message
borrower_id
```

Composite key:

```text
(batch_id, row_number)
```

---

# 10. Loan Products

## `loan_products.loan_products`

```text
loan_product_id
name
status
created_at_utc
updated_at_utc
row_version
```

## `loan_products.loan_product_versions`

```text
version_id
loan_product_id
version_number
maximum_amount decimal(19,4)
currency char(3)
deduction_percentage decimal(9,4)
eligibility_configuration nvarchar(max)
effective_from date
effective_to date NULL
status
published_at_utc
created_at_utc
row_version
```

Unique:

```text
(loan_product_id, version_number)
```

Published versions are immutable at application level.

Financing types may use a child table:

```text
loan_products.loan_product_financing_types
(version_id, financing_type)
```

---

# 11. Loan Origination

## `loan_origination.loan_applications`

```text
loan_application_id
borrower_id
loan_product_id
loan_product_version_id

requested_amount decimal(19,4)
approved_amount decimal(19,4) NULL
currency char(3)
financing_type

status

borrower_snapshot nvarchar(max)
product_snapshot nvarchar(max)
eligibility_snapshot nvarchar(max) NULL

unit_approval nvarchar(max) NULL
committee_approval nvarchar(max) NULL
mortgage_status
inspection_prerequisite_status

created_at_utc
submitted_at_utc NULL
approved_at_utc NULL
rejected_at_utc NULL
cancelled_at_utc NULL
row_version
```

Snapshots deliberately preserve historical decision inputs.

## `loan_origination.application_documents`

```text
loan_application_id
document_id
document_type
is_required
attached_at_utc
```

`document_id` is a logical cross-module reference.

## `loan_origination.property_inspections`

```text
property_inspection_id
loan_application_id
inspector_id
inspection_date
property_details nvarchar(max)
result
status
notes
created_at_utc
finalized_at_utc
row_version
```

`loan_application_id` may have a normal same-module FK.

---

# 12. Loan Accounts

## `loan_accounts.loan_accounts`

```text
loan_id
source_application_id
borrower_id
approved_amount decimal(19,4)
currency char(3)

reserved_disbursement_amount decimal(19,4)
total_disbursed decimal(19,4)
total_repaid decimal(19,4)

status
opened_at_utc
fully_repaid_at_utc NULL
closed_at_utc NULL
row_version
```

Unique:

```text
source_application_id
```

This supports idempotent loan opening from an approved application.

## `loan_accounts.disbursement_reservations`

```text
disbursement_id PK
loan_id
amount decimal(19,4)
currency char(3)
status
reserved_at_utc
released_at_utc NULL
confirmed_at_utc NULL
```

Same-module FK:

```text
loan_id -> loan_accounts.loan_accounts
```

Critical invariant:

```text
TotalDisbursed + ReservedDisbursementAmount <= ApprovedAmount
```

is enforced in the `LoanAccount` aggregate with concurrency control.

---

# 13. Disbursements

## `disbursements.disbursements`

```text
disbursement_id
loan_id
amount decimal(19,4)
currency char(3)
beneficiary_snapshot nvarchar(max)

status
capacity_status

technical_approval nvarchar(max) NULL
accounting_approval nvarchar(max) NULL
higher_approval nvarchar(max) NULL

created_by
created_at_utc
ready_for_treasury_at_utc NULL
completed_at_utc NULL
row_version
```

Indexes:

```text
loan_id
status
created_at_utc
```

No SQL FK to Loan Accounts.

---

# 14. Treasury

## `treasury.treasury_payments`

```text
treasury_payment_id
disbursement_id
loan_id
amount decimal(19,4)
currency char(3)
beneficiary_snapshot nvarchar(max)

status

input_decision nvarchar(max) NULL
audit_decision nvarchar(max) NULL
approval_decision nvarchar(max) NULL

payment_reference nvarchar(200) NULL
failure_code nvarchar(100) NULL
failure_reason nvarchar(1000) NULL

created_at_utc
processing_started_at_utc NULL
paid_at_utc NULL
failed_at_utc NULL
row_version
```

Unique:

```text
disbursement_id
```

This prevents more than one logical payment order per disbursement.

## `treasury.payment_attempts`

```text
payment_attempt_id
treasury_payment_id
attempt_number
idempotency_key
gateway_request_reference
gateway_response_reference
status
requested_at_utc
completed_at_utc
failure_reason
```

Unique:

```text
(treasury_payment_id, attempt_number)
idempotency_key
```

---

# 15. Repayments

## `repayments.repayments`

```text
repayment_id
loan_id
amount decimal(19,4)
currency char(3)
payment_date date
source
external_reference
receipt_document_id
status
notes
created_by
created_at_utc
posted_at_utc
row_version
```

Indexes:

```text
loan_id
payment_date
source
status
```

Duplicate rules for external references should reflect the actual repayment source.

## Salary Deduction

`repayments.salary_deduction_batches`

```text
batch_id
source_document_id
status
total_rows
success_count
failure_count
idempotency_key
uploaded_by
uploaded_at_utc
completed_at_utc
row_version
```

`repayments.salary_deduction_rows`

```text
batch_id
row_number
employee_number
civil_number
loan_id
amount
status
repayment_id
error_code
error_message
```

Composite key:

```text
(batch_id, row_number)
```

---

# 16. Documents

## `documents.documents`

```text
document_id
file_name
content_type
file_size
storage_key
status
uploaded_by
uploaded_at_utc
deleted_at_utc
row_version
```

Binary content is not stored in SQL Server for the MVP.

---

# 17. Audit

## `audit.audit_entries`

```text
audit_entry_id
actor_id
action
entity_type
entity_id
occurred_at_utc
previous_state
new_state
reason
correlation_id
event_id
```

Indexes:

```text
(entity_type, entity_id)
actor_id
occurred_at_utc
correlation_id
event_id
```

Audit is append-only.

---

# 18. Reporting

Reporting uses denormalized projection tables, for example:

```text
reporting.loan_application_summary
reporting.active_loan_summary
reporting.disbursement_summary
reporting.repayment_summary
reporting.outstanding_balance_summary
```

They are read models, never transactional source of truth.

---

# 19. Outbox

Each event-producing module owns:

```text
<schema>.outbox_messages
```

Columns:

```text
event_id PK
event_type
event_version
payload
occurred_at_utc
correlation_id
causation_id
processed_at_utc NULL
retry_count
next_attempt_at_utc NULL
last_error NULL
```

Indexes:

```text
processed_at_utc
next_attempt_at_utc
```

Aggregate state and Outbox event are committed in the same transaction.

---

# 20. Inbox

Each consuming module owns:

```text
<schema>.inbox_messages
```

Columns:

```text
event_id PK
event_type
received_at_utc
processed_at_utc NULL
retry_count
last_error NULL
```

The EventId PK protects against duplicate delivery.

---

# 21. HTTP Idempotency

Host-wide HTTP idempotency uses:

## `platform.idempotency_records`

```text
idempotency_key PK
scope
request_hash
response_status NULL
response_payload NULL
created_at_utc
expires_at_utc
completed_at_utc NULL
```

This schema is technical, not business-owned.

---

# 22. JSON Usage

Good JSON candidates:

- borrower snapshot;
- loan-product snapshot;
- eligibility snapshot;
- approval decision details;
- beneficiary snapshot;
- property details;
- variable eligibility configuration.

Do not use JSON for highly queried relational facts or values requiring strong relational constraints.

---

# 23. Delete Strategy

Financial/business history is not physically deleted.

Prefer:

```text
Borrower -> Deactivated
Application -> Cancelled
Document -> Deleted/Archived
```

Posted repayments, paid treasury transactions, and audit records are immutable history.

---

# 24. EF Core Mapping

Use explicit `IEntityTypeConfiguration<T>` mappings.

Explicitly configure:

- schema/table;
- key;
- column length;
- money precision;
- indexes;
- unique constraints;
- concurrency token;
- value conversions;
- same-module FKs.

Do not rely on conventions for critical financial mappings.

---

# 25. Repository Strategy

Repositories exist for aggregate roots when domain persistence abstraction is useful.

Examples:

```text
IBorrowerRepository
ILoanApplicationRepository
ILoanAccountRepository
IDisbursementRepository
ITreasuryPaymentRepository
IRepaymentRepository
```

Do not create `IGenericRepository<T>`.

Queries may project directly from EF Core to read DTOs.

---

# 26. Migrations

Each module owns migrations for its own schema.

Examples:

```text
Borrowers_Initial
LoanOrigination_Initial
LoanAccounts_AddReservation
Treasury_AddPaymentAttempts
```

Development/test may auto-apply migrations.

Production should use an explicit migration step before application rollout.

---

# 27. Transaction Rule

One module DbContext per transaction.

Correct:

```text
LoanApplication state
+ LoanOrigination Outbox row
= one transaction
```

Forbidden:

```text
LoanOriginationDbContext
+ LoanAccountsDbContext
= one transaction
```

Cross-module consistency uses events.

---

# 28. Required Concurrency Test

Scenario:

```text
ApprovedAmount = 100,000

Request A = 70,000
Request B = 50,000
```

Concurrent reservations must never produce:

```text
Reserved + Disbursed = 120,000
```

One transaction commits first; the other reloads/re-evaluates and fails if capacity is insufficient.

This is a mandatory integration test.

---

# 29. Baseline

```text
SQL Server
One database
Schema per module
DbContext per module
No cross-module SQL FK
Guid identifiers
decimal(19,4) money
rowversion concurrency
Outbox/Inbox per module
platform idempotency store
Explicit EF mapping
Module-owned migrations
```
