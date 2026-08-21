# Loan Management System — React Frontend Architecture

## 1. Purpose

This document defines the React frontend architecture for the internal Loan Management System MVP.

The frontend is a business-oriented SPA. It must reflect backend module boundaries without duplicating backend domain logic.

---

## 2. Baseline Stack

Recommended baseline:

- React
- TypeScript
- Vite
- React Router
- TanStack Query for server state
- React Hook Form or equivalent form library
- schema-based input validation such as Zod or equivalent
- OpenAPI-derived API types/client after backend contracts stabilize

Create React App is not used.

---

## 3. Core Principles

1. Organize by business feature, not technical file type.
2. Server state is not copied into global client state unnecessarily.
3. Backend is authoritative for business rules and authorization.
4. React validation improves UX but does not replace backend validation.
5. API DTOs are not treated as domain objects.
6. Cross-feature imports are controlled.
7. Shared components contain generic UI only.

---

## 4. Recommended Structure

```text
frontend/loan-system-web/

├── src/
│   ├── app/
│   │   ├── router/
│   │   ├── providers/
│   │   ├── layouts/
│   │   ├── auth/
│   │   └── bootstrap/
│   │
│   ├── features/
│   │   ├── auth/
│   │   ├── borrowers/
│   │   ├── loan-products/
│   │   ├── loan-applications/
│   │   ├── inspections/
│   │   ├── loans/
│   │   ├── disbursements/
│   │   ├── treasury/
│   │   ├── repayments/
│   │   ├── documents/
│   │   ├── audit/
│   │   └── reporting/
│   │
│   ├── shared/
│   │   ├── api/
│   │   ├── ui/
│   │   ├── forms/
│   │   ├── hooks/
│   │   ├── lib/
│   │   ├── auth/
│   │   ├── errors/
│   │   └── types/
│   │
│   ├── main.tsx
│   └── vite-env.d.ts
│
├── tests/
└── package.json
```

---

## 5. Feature Structure

Example:

```text
features/loan-applications/

├── api/
│   ├── queries.ts
│   ├── mutations.ts
│   └── keys.ts
│
├── components/
│   ├── ApplicationStatusBadge.tsx
│   ├── ApprovalHistory.tsx
│   └── EligibilityResult.tsx
│
├── forms/
│   ├── applicationFormSchema.ts
│   └── ApplicationForm.tsx
│
├── pages/
│   ├── LoanApplicationListPage.tsx
│   ├── LoanApplicationCreatePage.tsx
│   └── LoanApplicationDetailsPage.tsx
│
├── model/
│   └── ui-types.ts
│
└── index.ts
```

Only expose intentional feature entrypoints from `index.ts` where useful.

---

## 6. Server State

Use TanStack Query for:

- fetching;
- caching;
- mutation state;
- query invalidation;
- refetching;
- background synchronization.

Examples of server state:

```text
Borrower list
Loan application details
Loan account balance
Disbursement status
Treasury payment status
Repayment list
```

Do not copy these into Redux/global stores by default.

---

## 7. Client State

Keep UI-only state local where possible:

```text
open dialog
selected tab
temporary filters
wizard step
unsaved draft form state
```

A global state library is not required for MVP unless a concrete cross-application client-state problem appears.

---

## 8. Routing

Routes should reflect user tasks.

Example:

```text
/login

/borrowers
/borrowers/:borrowerId

/loan-products

/loan-applications
/loan-applications/new
/loan-applications/:applicationId

/loans
/loans/:loanId

/disbursements
/disbursements/:disbursementId

/treasury
/treasury/:paymentId

/repayments

/audit
/reports
```

Route-level authorization improves UX, but endpoint authorization remains authoritative.

---

## 9. API Client Boundary

All network calls go through `shared/api` and feature API adapters.

Forbidden:

```text
Page Component -> raw fetch scattered inline
```

Preferred:

```text
Page
  ↓
Feature query/mutation hook
  ↓
Generated/typed API client
  ↓
ASP.NET Core API
```

When OpenAPI contracts stabilize, generate or derive TypeScript API types rather than manually duplicating every DTO.

---

## 10. Error Handling

Backend `ProblemDetails` should map to a common frontend error model.

React must distinguish:

```text
Validation errors
Forbidden
Not found
Business conflict
Concurrency conflict
Unexpected error
```

Stable backend `errorCode` values can drive user-friendly messages.

---

## 11. Concurrency UX

When backend returns `412 Precondition Failed`:

- inform user the record changed;
- offer reload/refetch;
- do not silently overwrite newer state.

The API version/ETag should be preserved by feature mutations where needed.

---

## 12. Idempotency UX

For financial mutations, the frontend creates and retains an `Idempotency-Key` for the logical user operation until a final response is received.

Examples:

- create disbursement;
- execute payment;
- post manual repayment.

Double-clicking or network retry must not create duplicate financial operations.

---

## 13. Authorization UX

Frontend obtains permissions from `/auth/me`.

Use permission checks for:

- route visibility;
- button visibility;
- action availability.

Example:

```text
loanApplications.committeeApprove
```

Do not write:

```text
if roleName == "CommitteeMember"
```

unless the UI is literally displaying role administration.

---

## 14. Forms

Forms should have:

- client-side structural validation;
- server-side error mapping;
- dirty-state handling;
- disabled duplicate-submit state;
- accessible labels/errors;
- explicit confirmation for irreversible financial actions.

Domain decisions still come from the backend.

---

## 15. Workflow UI

Do not implement a generic workflow engine in React.

The backend response should expose the relevant state and, where useful, allowed actions.

The frontend renders explicit business actions such as:

```text
Submit
Unit Approve
Committee Approve
Reject
Create Disbursement
Treasury Audit
Execute Payment
Post Repayment
```

---

## 16. Component Boundaries

### Shared UI

Examples:

```text
Button
Modal
Table
Pagination
DateInput
MoneyInput
FileUpload
ErrorPanel
PermissionGuard
```

### Feature Components

Examples:

```text
ApplicationApprovalHistory
LoanBalanceSummary
DisbursementApprovalPanel
TreasuryPaymentPanel
RepaymentBatchSummary
```

Feature components do not move into `shared/ui` merely because they are reused twice.

---

## 17. Testing

Frontend testing baseline:

- unit tests for pure utilities;
- component tests for complex forms/workflow UI;
- API mocking at feature boundary;
- E2E tests for critical user flows.

Do not attempt exhaustive snapshot testing.

---

## 18. Build and Environment

Use environment configuration only for public frontend configuration such as API base URL.

Never put secrets in Vite/React environment variables shipped to the browser.

---

## 19. Baseline Decision

```text
React SPA
+ TypeScript
+ Vite
+ business-feature folders
+ TanStack Query server state
+ no Redux by default
+ permission-driven UI
+ typed API boundary
```
