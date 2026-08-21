# Loan Management System — Module Contracts

## 1. Purpose

This document defines the allowed communication boundaries between modules in the Modular Monolith.

It specifies:
- synchronous public contracts;
- integration events;
- ownership rules;
- reliability requirements;
- prohibited dependencies.

The objective is to preserve bounded-context autonomy without introducing microservice operational complexity.

## 2. Core Rule

A module may depend on another module only through:

1. an explicit **Public Contract** for synchronous reads/services; or
2. an **Integration Event** for cross-module state propagation.

Forbidden:

```text
Module A -> Module B Infrastructure
Module A -> Module B DbContext
Module A -> Module B tables
Module A -> Module B domain entities
```

## 3. Contract Placement

Recommended logical layout:

```text
Modules/
└── Borrowers/
    ├── Domain/
    ├── Application/
    ├── Infrastructure/
    ├── Presentation/
    └── Contracts/
```

`Contracts` must not expose:
- EF entities;
- repositories;
- aggregate roots;
- internal value objects;
- handler implementations.

## 4. Synchronous vs Event-Driven Rule

### Use synchronous contracts when:
- immediate read data is required;
- the other module is not being mutated;
- eventual consistency would create unnecessary ambiguity.

Examples:
- borrower snapshot;
- product version snapshot;
- loan summary for display.

### Use integration events when:
- another module must change its state;
- reaction may occur after the originating transaction commits;
- module ownership must be preserved.

Examples:
- approved application opens Loan Account;
- disbursement requests capacity reservation;
- treasury completion confirms disbursement;
- repayment posting updates Loan Account.

---

# 5. Identity & Access Contracts

```csharp
public interface ICurrentUser
{
    Guid UserId { get; }
    string UserName { get; }
    bool IsAuthenticated { get; }
}
```

```csharp
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(
        string permission,
        CancellationToken cancellationToken);
}
```

Business modules depend on these abstractions, not Identity persistence.

---

# 6. Borrowers Public Contract

```csharp
public interface IBorrowersModule
{
    Task<BorrowerSnapshot?> GetSnapshotAsync(
        Guid borrowerId,
        CancellationToken cancellationToken);
}
```

Conceptual DTO:

```csharp
public sealed record BorrowerSnapshot(
    Guid BorrowerId,
    string CivilNumber,
    string? EmployeeNumber,
    string FullName,
    string? Organization,
    string? RankGrade,
    bool IsActive,
    long Version);
```

Loan Origination may persist the relevant snapshot fields at decision time.

---

# 7. Loan Products Public Contract

```csharp
public interface ILoanProductsModule
{
    Task<LoanProductVersionSnapshot?> GetVersionAsync(
        Guid loanProductVersionId,
        CancellationToken cancellationToken);
}
```

Conceptual DTO:

```csharp
public sealed record LoanProductVersionSnapshot(
    Guid LoanProductId,
    Guid VersionId,
    string Name,
    decimal MaximumAmount,
    decimal DeductionPercentage,
    IReadOnlyCollection<string> FinancingTypes,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsAvailable);
```

Eligibility configuration should use a dedicated contract DTO, not domain objects.

---

# 8. Loan Origination Integration Contract

## `LoanApplicationApprovedV1`

Publisher:
- Loan Origination

Consumer:
- Loan Accounts

Minimum payload:

```text
EventId
OccurredAtUtc
CorrelationId

LoanApplicationId
BorrowerId
LoanProductId
LoanProductVersionId
ApprovedAmount
Currency
FinancingType
ApprovedAtUtc
```

Idempotency key:
- `LoanApplicationId`

---

# 9. Loan Accounts Public Read Contract

```csharp
public interface ILoanAccountsModule
{
    Task<LoanSummary?> GetSummaryAsync(
        Guid loanId,
        CancellationToken cancellationToken);
}
```

Conceptual DTO:

```csharp
public sealed record LoanSummary(
    Guid LoanId,
    Guid BorrowerId,
    decimal ApprovedAmount,
    decimal ReservedDisbursementAmount,
    decimal TotalDisbursed,
    decimal AvailableToDisburse,
    decimal TotalRepaid,
    decimal OutstandingBalance,
    string Status,
    long Version);
```

This is a read contract only.

Loan-account mutations are driven by integration events.

---

# 10. Disbursement Capacity Contracts

## `DisbursementCapacityRequestedV1`

Publisher:
- Disbursements

Consumer:
- Loan Accounts

Payload:

```text
EventId
CorrelationId
DisbursementId
LoanId
Amount
Currency
RequestedAtUtc
```

## `DisbursementCapacityReservedV1`

Publisher:
- Loan Accounts

Consumer:
- Disbursements

Payload:

```text
EventId
CorrelationId
DisbursementId
LoanId
ReservedAmount
ReservedAtUtc
LoanAccountVersion
```

## `DisbursementCapacityRejectedV1`

Payload:

```text
DisbursementId
LoanId
RequestedAmount
ReasonCode
Reason
```

## `DisbursementCapacityReleaseRequestedV1`

Publisher:
- Disbursements

Consumer:
- Loan Accounts

Used when rejected/cancelled before successful payment.

## `DisbursementCapacityReleasedV1`

Publisher:
- Loan Accounts

Consumer:
- Disbursements / Audit where needed.

---

# 11. Disbursement-to-Treasury Contract

## `DisbursementReadyForTreasuryV1`

Publisher:
- Disbursements

Consumer:
- Treasury

Payload:

```text
DisbursementId
LoanId
Amount
Currency
BeneficiarySnapshot
ApprovedAtUtc
CorrelationId
```

Treasury must create at most one active logical payment order for one DisbursementId.

---

# 12. Treasury Result Contracts

## `TreasuryPaymentCompletedV1`

Publisher:
- Treasury

Consumers:
- Loan Accounts
- Disbursements
- Audit
- Reporting

Payload:

```text
TreasuryPaymentId
DisbursementId
LoanId
Amount
Currency
PaymentReference
PaidAtUtc
CorrelationId
```

## `TreasuryPaymentFailedV1`

Publisher:
- Treasury

Consumers:
- Disbursements
- Audit
- Reporting

Payload:

```text
TreasuryPaymentId
DisbursementId
FailureCode
FailureReason
FailedAtUtc
Retryable
CorrelationId
```

A retry must not create a second logical disbursement.

---

# 13. Repayment Contract

## `RepaymentPostedV1`

Publisher:
- Repayments

Consumers:
- Loan Accounts
- Audit
- Reporting

Payload:

```text
RepaymentId
LoanId
Amount
Currency
PaymentDate
Source
ExternalReference
PostedAtUtc
CorrelationId
```

Idempotency:
- RepaymentId;
- plus business duplicate rules for external references.

---

# 14. Loan Account Result Events

## `LoanDisbursementConfirmedV1`

Payload:
- LoanId
- DisbursementId
- Amount
- TotalDisbursed
- OutstandingBalance

## `LoanRepaymentAppliedV1`

Payload:
- LoanId
- RepaymentId
- Amount
- TotalRepaid
- OutstandingBalance

## `LoanFullyRepaidV1`

Payload:
- LoanId
- FullyRepaidAtUtc

## `LoanClosedV1`

Payload:
- LoanId
- ClosedAtUtc

---

# 15. Documents Contract

Conceptual API:

```csharp
public interface IDocumentsModule
{
    Task<StoredDocument> StoreAsync(
        StoreDocumentRequest request,
        CancellationToken cancellationToken);

    Task<DocumentMetadata?> GetMetadataAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}
```

Business modules store only `DocumentId` references.

Actual streaming may be handled at API boundary to avoid unnecessary file copies through domain layers.

---

# 16. Audit Contract

Preferred:
- consume business/integration events;
- enrich with actor/correlation metadata;
- append immutable `AuditEntry`.

Business modules must not query Audit to decide business validity.

---

# 17. Reporting Contract

Reporting owns read projections.

Transactional modules do not depend on Reporting.

Reporting may consume:
- integration events;
- purpose-built read feeds;
- projection data.

Reporting never mutates core domain state.

---

# 18. Integration Event Envelope

Every integration event should carry:

```text
EventId
EventType
EventVersion
OccurredAtUtc
CorrelationId
CausationId
ProducerModule
Payload
```

Properties:
- EventId: globally unique;
- EventVersion: explicit contract version;
- CorrelationId: end-to-end business trace;
- CausationId: originating event/command.

---

# 19. Event Versioning

Integration events are contracts.

Once consumed, do not silently mutate them.

Use explicit versions:

```text
LoanApplicationApprovedV1
LoanApplicationApprovedV2
```

Breaking changes require a new version.

---

# 20. Outbox / Inbox

## Transactional Outbox

Producer commits:

```text
Aggregate changes
+
Integration event
```

in the same local transaction.

## Idempotent Inbox

Consumer records processed EventId.

Duplicate delivery must not duplicate state changes.

Priority events:

- LoanApplicationApproved
- DisbursementCapacityRequested
- DisbursementCapacityReserved
- DisbursementCapacityReleaseRequested
- DisbursementReadyForTreasury
- TreasuryPaymentCompleted
- RepaymentPosted

---

# 21. MVP Event Dispatch

No external broker is required.

Recommended:

```text
Module Transaction
      ↓
Outbox
      ↓
Background Dispatcher
      ↓
In-Process Event Bus
      ↓
Consumer Module
      ↓
Inbox + Local Transaction
```

A broker can be added later without changing domain contracts.

---

# 22. Cross-Module Failure Semantics

If a consumer fails:

- producer transaction stays committed;
- event remains retryable;
- Inbox is not marked complete;
- error is observable;
- retries are idempotent.

Example:

```text
LoanApplicationApproved committed
LoanAccount consumer fails temporarily

→ Application remains Approved
→ Outbox retries
→ LoanAccount eventually opens
```

---

# 23. Contract Dependency Direction

Recommended:

```text
Module.Application
    ↓
Own Domain

Module.Application
    ↓
OtherModule.Contracts

Module.Infrastructure
    ↓
Own Application / Domain

Host/API
    ↓
Module Presentation / Registration
```

Forbidden:

```text
Module.Domain
    ↓
OtherModule.Contracts
```

Domain models remain isolated from transport/integration contracts.

---

# 24. Shared Building Blocks

A minimal shared library may contain:

- Result
- base DomainEvent abstractions
- integration-event envelope
- strongly typed ID infrastructure
- clock abstraction
- transaction/outbox abstractions
- common technical errors

`Money` should only be shared if its semantics are truly identical across contexts.

Do not put business rules in `BuildingBlocks`.

---

# 25. Architecture Tests

Automated tests should verify:

1. Domain does not reference Infrastructure.
2. Domain does not reference Presentation.
3. Modules do not reference other modules' Infrastructure.
4. Modules do not reference other modules' Domain.
5. Only Contracts can be referenced cross-module.
6. Reporting is not referenced by transactional modules.
7. Audit is not a business-state source.
8. API host contains no domain business rules.

---

# 26. Baseline Decision

```text
Cross-module reads
    → synchronous Public Contracts

Cross-module state changes
    → persisted Integration Events

Business-critical delivery
    → Outbox + Inbox

Message transport
    → in-process dispatcher

External broker
    → not required
```
