# Loan Management System — Testing Strategy

## 1. Purpose

This document defines the testing strategy for the MVP with one primary goal:

> Validate each vertical slice continuously and avoid a big-bang testing phase at the end.

---

## 2. Testing Pyramid for This System

```text
             E2E
          /       \
       API / Flow Tests
      /             \
   Integration / Persistence
  /                   \
Domain + Application Unit Tests
```

Architecture tests run alongside all levels and enforce structural rules.

---

## 3. Domain Tests

Project:

```text
LoanSystem.Domain.Tests
```

Purpose:

- aggregate invariants;
- value-object rules;
- state transitions;
- domain policies.

Examples:

```text
Application cannot committee-approve before unit approval
Disbursement reservation cannot exceed loan capacity
Loan cannot confirm a released reservation
Repayment cannot be applied twice
Loan cannot close with positive outstanding balance
```

Domain tests should be fast and database-free.

---

## 4. Application Tests

Project:

```text
LoanSystem.Application.Tests
```

Purpose:

- command/query handler orchestration;
- permission handling;
- contract interaction;
- expected result mapping;
- policy invocation.

Use fakes/stubs only at clear module boundaries.

Do not mock aggregate behavior.

---

## 5. Integration Tests

Project:

```text
LoanSystem.IntegrationTests
```

Use a real SQL Server-compatible integration environment, preferably disposable/containerized.

Cover:

- EF mappings;
- constraints;
- indexes where behavior matters;
- migrations;
- repository queries;
- transaction behavior;
- concurrency;
- Outbox writes;
- Inbox idempotency;
- module event processing.

Critical concurrency test:

```text
Two disbursement reservations race against the same loan capacity.
```

Only valid capacity reservations may commit.

---

## 6. API Tests

Project:

```text
LoanSystem.Api.Tests
```

Use ASP.NET Core application-host integration testing.

Cover:

- authentication;
- authorization;
- status codes;
- ProblemDetails;
- ETag/If-Match;
- Idempotency-Key;
- request/response contracts;
- endpoint-to-module wiring.

Example negative tests:

```text
403 when committee approval permission is missing
409 on invalid workflow transition
412 on stale ETag
409 on Idempotency-Key reuse with different payload
```

---

## 7. Architecture Tests

Project:

```text
LoanSystem.ArchitectureTests
```

These are mandatory from the beginning.

Rules include:

- Domain cannot depend on Infrastructure.
- Domain cannot depend on Presentation.
- Application cannot depend on Infrastructure.
- Module implementation A cannot reference module implementation B.
- Cross-module references must use `LoanSystem.Contracts`.
- API host cannot contain domain logic.
- DbContexts remain module-internal.
- Reporting cannot become a dependency of transactional modules.

A build fails if architecture boundaries are violated.

---

## 8. E2E Tests

Project/folder:

```text
LoanSystem.E2E.Tests
```

Use browser automation for a small number of critical workflows.

Primary E2E:

```text
Login
→ Create borrower
→ Create application
→ Eligibility
→ Unit approval
→ Committee approval
→ Inspection
→ Final approval
→ Disbursement
→ Treasury payment
→ Repayment
→ Updated balance
```

Do not use E2E tests to cover every field validation and edge case.

---

## 9. Frontend Tests

Recommended categories:

### Unit

- formatters;
- pure validation helpers;
- permission helpers.

### Component

- application form;
- approval panel;
- disbursement decision UI;
- payment execution confirmation;
- import result display.

### E2E

Only critical user journeys.

---

## 10. Test per Vertical Slice

Every implementation slice must end with its own verification.

Example: `Register Borrower`

```text
1. Domain tests
2. Persistence integration test
3. API test
4. Frontend component/page test where valuable
5. Manual smoke check
```

Then merge.

Do not implement ten modules and test them together afterward.

---

## 11. Financial Safety Test Categories

Mandatory for financial paths:

### Idempotency

- same request retried;
- same event delivered twice;
- same bank response received twice.

### Concurrency

- parallel disbursement reservation;
- parallel approval attempts;
- payment retry racing with completion.

### State Machine

- invalid transitions rejected.

### Data Integrity

- no negative outstanding balance under MVP policy;
- no over-disbursement;
- no duplicate repayment posting;
- no duplicate payment completion.

---

## 12. Outbox / Inbox Tests

Required scenarios:

```text
Producer state and Outbox commit together
Consumer failure does not lose event
Retry eventually succeeds
Duplicate event does not duplicate state
Inbox marks event only after successful local transaction
```

---

## 13. Migration Tests

Before release:

- clean database can migrate to latest;
- current baseline can migrate forward;
- application starts against migrated database;
- required seed/reference data is deterministic.

Production schema changes are forward migrations, not manual SQL edits.

---

## 14. Test Data

Use deterministic builders/factories.

Examples:

```text
BorrowerBuilder
LoanApplicationBuilder
LoanAccountBuilder
DisbursementBuilder
TreasuryPaymentBuilder
RepaymentBuilder
```

Avoid giant shared fixture graphs that couple unrelated tests.

---

## 15. CI Quality Gates

A pull request should not merge unless:

```text
Build passes
Unit tests pass
Architecture tests pass
Relevant integration tests pass
API contract tests pass
Frontend checks pass
```

E2E scope can be split into fast PR smoke and fuller main-branch runs if runtime becomes significant.

---

## 16. Coverage Policy

Do not optimize for a single global coverage percentage.

Prioritize coverage of:

- invariants;
- financial calculations;
- state transitions;
- event idempotency;
- concurrency;
- authorization;
- integration boundaries.

Coverage metrics are diagnostic, not the definition of quality.

---

## 17. Definition of Done per Use Case

A use case is not done until:

- business behavior is implemented;
- domain tests cover important rules;
- persistence/API integration is tested where applicable;
- negative paths are tested;
- architecture rules still pass;
- frontend state/error behavior is verified if UI exists;
- documentation/contract changes are updated.

---

## 18. Baseline

Testing is incremental and vertical-slice-oriented.

There is no separate end-of-project "testing phase" for core correctness.
