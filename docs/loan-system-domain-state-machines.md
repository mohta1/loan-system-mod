# Loan Management System — Domain State Machines

## 1. Purpose

This document defines the initial state transitions for the primary MVP aggregates.

The state machines are implementation baselines. Invalid transitions must be rejected by domain logic rather than only by UI rules.

---

# 2. Loan Application State Machine

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Submitted: Submit
    Draft --> Cancelled: Cancel

    Submitted --> UnitApproved: Unit Approve
    Submitted --> Rejected: Unit Reject

    UnitApproved --> CommitteeApproved: Committee Approve
    UnitApproved --> Rejected: Committee Reject

    CommitteeApproved --> PrerequisitesPending

    PrerequisitesPending --> ReadyForFinalApproval: Inspection + Documents + Mortgage satisfied
    PrerequisitesPending --> Rejected: Reject

    ReadyForFinalApproval --> Approved: Final Approve
    ReadyForFinalApproval --> Rejected: Reject

    Approved --> [*]
    Rejected --> [*]
    Cancelled --> [*]
```

### Terminal states

- Approved
- Rejected
- Cancelled

Application does not become a Loan Account by changing its state.

`Approved` triggers creation of a separate Loan Account.

---

# 3. Property Inspection State Machine

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Recorded: Complete Inspection
    Recorded --> Approved: Approve
    Recorded --> Rejected: Reject
    Approved --> [*]
    Rejected --> [*]
```

If reinspection is later required, create an explicit reinspection workflow rather than editing a finalized inspection in place.

---

# 4. Loan Account State Machine

```mermaid
stateDiagram-v2
    [*] --> Active

    Active --> FullyDisbursed: AvailableToDisburse = 0
    Active --> FullyRepaid: OutstandingBalance = 0 and no future obligation
    FullyDisbursed --> FullyRepaid: OutstandingBalance = 0

    FullyRepaid --> Closed: Close Loan

    Closed --> [*]
```

The exact naming may evolve when interest/accounting requirements are finalized.

Financial totals and reservations are invariants independent of display status.

---

# 5. Disbursement State Machine

```mermaid
stateDiagram-v2
    [*] --> PendingCapacity

    PendingCapacity --> Requested: Capacity Reserved
    PendingCapacity --> Rejected: Capacity Rejected
    PendingCapacity --> Cancelled: Cancel

    Requested --> TechnicalApproved: Technical Approve
    Requested --> Rejected: Technical Reject

    TechnicalApproved --> AccountingApproved: Accounting Approve
    TechnicalApproved --> Rejected: Accounting Reject

    AccountingApproved --> ReadyForTreasury: Higher Approve
    AccountingApproved --> Rejected: Higher Reject

    ReadyForTreasury --> TreasuryProcessing: Treasury Payment Created
    TreasuryProcessing --> Completed: Payment Completed
    TreasuryProcessing --> PaymentFailed: Payment Failed

    PaymentFailed --> TreasuryProcessing: Retry
    PaymentFailed --> Cancelled: Permanent Cancel

    Completed --> [*]
    Rejected --> [*]
    Cancelled --> [*]
```

### Capacity rule

Capacity is reserved from `PendingCapacity → Requested`.

Capacity is released if the disbursement reaches `Rejected` or `Cancelled` before successful payment.

On `Completed`, the reservation becomes confirmed disbursed principal.

---

# 6. Treasury Payment State Machine

```mermaid
stateDiagram-v2
    [*] --> Pending

    Pending --> Entered: Input
    Pending --> Rejected: Reject

    Entered --> Audited: Audit Approve
    Entered --> Rejected: Audit Reject

    Audited --> Approved: Final Approve
    Audited --> Rejected: Final Reject

    Approved --> Processing: Execute

    Processing --> Paid: Gateway Success
    Processing --> Failed: Gateway Failure

    Failed --> Processing: Retry
    Failed --> Rejected: Permanently Cancel/Reject

    Paid --> [*]
    Rejected --> [*]
```

`Paid` is terminal for the same payment order.

Repeated external success callbacks must be idempotent.

---

# 7. Repayment State Machine

```mermaid
stateDiagram-v2
    [*] --> Recorded

    Recorded --> Validated: Validate
    Recorded --> Rejected: Invalid

    Validated --> Posted: Post
    Validated --> Rejected: Posting Rejected

    Posted --> [*]
    Rejected --> [*]
```

A posted repayment is immutable.

Corrections, if later supported, should use explicit reversal/adjustment transactions rather than editing posted history.

---

# 8. Salary Deduction Batch State Machine

```mermaid
stateDiagram-v2
    [*] --> Uploaded
    Uploaded --> Validating: Validate
    Validating --> Validated: Validation Complete
    Validating --> Failed: File/Schema Failure
    Validated --> Posting: Post Valid Rows
    Posting --> Completed: All Rows Processed
    Posting --> PartiallyCompleted: Some Rows Failed
    Completed --> [*]
    PartiallyCompleted --> [*]
    Failed --> [*]
```

Individual row failures do not require rolling back successful independent repayments unless the business later requires all-or-nothing batch semantics.

---

# 9. Transition Enforcement

Every state-changing domain method must verify:

1. current state;
2. actor authorization at application boundary;
3. business prerequisites;
4. idempotency where applicable;
5. concurrency token/version;
6. invariant preservation.

Frontend controls may hide unavailable actions, but backend/domain logic remains authoritative.

---

# 10. Baseline

These state machines should be reflected in:

- aggregate methods;
- API command handlers;
- validation tests;
- architecture documentation;
- negative-path integration tests.
