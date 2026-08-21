# Loan Management System — REST API Boundary Design

## 1. Purpose

This document defines the external HTTP API boundary used by the React frontend.

It establishes:
- resource routes;
- workflow command endpoints;
- status codes;
- error model;
- concurrency strategy;
- idempotency;
- pagination;
- authorization boundary.

It is not yet the generated OpenAPI file.

## 2. API Style

Base path:

```text
/api/v1
```

Style:
- REST-oriented resources;
- explicit action endpoints for workflow transitions;
- JSON;
- camelCase properties;
- ISO-8601 UTC timestamps;
- server-side authorization;
- Problem Details errors.

Do not expose domain or EF entities directly.

---

# 3. HTTP Status Strategy

| Status | Usage |
|---|---|
| 200 | Successful query/command returning data |
| 201 | Resource created |
| 202 | Accepted; async module processing pending |
| 204 | Successful command with no body |
| 400 | Request validation/syntax error |
| 401 | Not authenticated |
| 403 | Not permitted |
| 404 | Resource not found |
| 409 | Conflict / duplicate / invalid state |
| 412 | Optimistic concurrency precondition failed |
| 422 | Business validation violation where appropriate |
| 500 | Unexpected failure |

Recommended:
- 409 for state/conflict violations;
- 422 for semantic business validation not tied to state conflict.

---

# 4. Error Contract

Use ASP.NET Core `ProblemDetails`.

Example:

```json
{
  "type": "https://errors.loan-system.local/business-rule",
  "title": "Business rule violation",
  "status": 409,
  "detail": "Committee approval requires unit approval.",
  "instance": "/api/v1/loan-applications/...",
  "errorCode": "loan_application.invalid_transition",
  "correlationId": "...",
  "errors": []
}
```

`errorCode` must be stable for frontend logic and tests.

---

# 5. Correlation

Header:

```text
X-Correlation-Id
```

If absent, API generates it.

It flows through:
- logs;
- integration events;
- audit;
- payment calls;
- errors.

---

# 6. Optimistic Concurrency

Mutable resources expose a version/ETag.

Response:

```text
ETag: "7"
```

Mutation:

```text
If-Match: "7"
```

Stale version:

```text
412 Precondition Failed
```

Priority:
- Loan Application
- Loan Product configuration
- Disbursement
- Treasury Payment
- contested financial state.

---

# 7. Idempotency

Financially significant POST commands require:

```text
Idempotency-Key: <client-generated-key>
```

Mandatory for:
- create disbursement;
- execute/retry treasury payment;
- record manual repayment;
- create/post salary deduction batch;
- other externally retried financial commands.

Same key + same payload:
- return original logical result.

Same key + different payload:
- 409 conflict.

---

# 8. Pagination

MVP standard:

```text
?pageNumber=1&pageSize=25
```

Response:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 25,
  "totalCount": 0
}
```

Bound maximum page size.

---

# 9. Authentication

## POST `/api/v1/auth/login`
## POST `/api/v1/auth/logout`
## GET `/api/v1/auth/me`

`/me` returns:
- User
- Roles
- Permissions

---

# 10. User Administration

## GET `/api/v1/users`
## POST `/api/v1/users`
## GET `/api/v1/users/{userId}`
## PUT `/api/v1/users/{userId}`
## POST `/api/v1/users/{userId}/activate`
## POST `/api/v1/users/{userId}/deactivate`
## PUT `/api/v1/users/{userId}/roles`

Permission:
`identity.users.manage`

---

# 11. Borrowers

## GET `/api/v1/borrowers`

Filters:
- civilNumber
- employeeNumber
- name
- organization
- status
- pageNumber
- pageSize

## POST `/api/v1/borrowers`

Returns:
`201 Created`

## GET `/api/v1/borrowers/{borrowerId}`

## PUT `/api/v1/borrowers/{borrowerId}`

Requires concurrency token/ETag.

## POST `/api/v1/borrowers/{borrowerId}/activate`

## POST `/api/v1/borrowers/{borrowerId}/deactivate`

---

# 12. Borrower Imports

## POST `/api/v1/borrower-imports/validate`

Multipart upload.

## POST `/api/v1/borrower-imports/{batchId}/execute`

Idempotent by BatchId.

## GET `/api/v1/borrower-imports/{batchId}`

---

# 13. Loan Products

## GET `/api/v1/loan-products`
## POST `/api/v1/loan-products`
## GET `/api/v1/loan-products/{loanProductId}`
## POST `/api/v1/loan-products/{loanProductId}/versions`
## PUT `/api/v1/loan-products/{loanProductId}/versions/{versionId}`
## POST `/api/v1/loan-products/{loanProductId}/versions/{versionId}/publish`
## POST `/api/v1/loan-products/{loanProductId}/activate`
## POST `/api/v1/loan-products/{loanProductId}/deactivate`
## GET `/api/v1/loan-products/available`

Only draft versions are editable.

---

# 14. Loan Applications

## GET `/api/v1/loan-applications`

Filters:
- borrowerId
- status
- productId
- organization
- fromDate
- toDate
- pageNumber
- pageSize

## POST `/api/v1/loan-applications`

Creates Draft.

Conceptual request:

```json
{
  "borrowerId": "...",
  "loanProductVersionId": "...",
  "financingType": "Housing",
  "requestedAmount": {
    "amount": 10000,
    "currency": "..."
  }
}
```

## GET `/api/v1/loan-applications/{applicationId}`

## PUT `/api/v1/loan-applications/{applicationId}`

Draft edits only.

## POST `/api/v1/loan-applications/{applicationId}/evaluate-eligibility`

## POST `/api/v1/loan-applications/{applicationId}/submit`

## POST `/api/v1/loan-applications/{applicationId}/unit-decision`

## POST `/api/v1/loan-applications/{applicationId}/committee-decision`

## POST `/api/v1/loan-applications/{applicationId}/mortgage/completed`

## POST `/api/v1/loan-applications/{applicationId}/mortgage/not-required`

## POST `/api/v1/loan-applications/{applicationId}/final-decision`

## POST `/api/v1/loan-applications/{applicationId}/cancel`

Decision body:

```json
{
  "decision": "approve",
  "comment": "..."
}
```

or:

```json
{
  "decision": "reject",
  "reason": "...",
  "comment": "..."
}
```

No generic `PUT status` endpoint is allowed.

---

# 15. Inspections

## POST `/api/v1/loan-applications/{applicationId}/inspections`
## GET `/api/v1/loan-applications/{applicationId}/inspections`
## GET `/api/v1/inspections/{inspectionId}`
## PUT `/api/v1/inspections/{inspectionId}`
## POST `/api/v1/inspections/{inspectionId}/decision`

Finalized inspections are not silently overwritten.

---

# 16. Documents

## POST `/api/v1/documents`

Multipart upload.

## GET `/api/v1/documents/{documentId}`

Metadata.

## GET `/api/v1/documents/{documentId}/content`

Streams content after authorization.

## DELETE `/api/v1/documents/{documentId}`

Only where retention/business rules allow.

---

# 17. Loan Accounts

Loan Accounts is mostly query-oriented externally.

## GET `/api/v1/loans`

Filters:
- borrowerId
- status
- product
- balance range
- pageNumber
- pageSize

## GET `/api/v1/loans/{loanId}`

Returns:
- Approved Amount
- Reserved Amount
- Total Disbursed
- Available To Disburse
- Total Repaid
- Outstanding Balance
- Status

## POST `/api/v1/loans/{loanId}/close`

Permission:
`loans.close`

Financial totals are not directly mutable through public HTTP endpoints.

---

# 18. Disbursements

## GET `/api/v1/disbursements`

## POST `/api/v1/disbursements`

Header:
`Idempotency-Key` required.

Returns a resource initially in:

```text
PendingCapacity
```

Recommended response:
`201 Created`

## GET `/api/v1/disbursements/{disbursementId}`

## POST `/api/v1/disbursements/{disbursementId}/technical-decision`

## POST `/api/v1/disbursements/{disbursementId}/accounting-decision`

## POST `/api/v1/disbursements/{disbursementId}/final-decision`

## POST `/api/v1/disbursements/{disbursementId}/cancel`

---

# 19. Treasury

## GET `/api/v1/treasury-payments`
## GET `/api/v1/treasury-payments/{paymentId}`
## POST `/api/v1/treasury-payments/{paymentId}/input`
## POST `/api/v1/treasury-payments/{paymentId}/audit-decision`
## POST `/api/v1/treasury-payments/{paymentId}/approval-decision`
## POST `/api/v1/treasury-payments/{paymentId}/execute`
## POST `/api/v1/treasury-payments/{paymentId}/retry`

`execute` and `retry` require `Idempotency-Key`.

---

# 20. Repayments

## GET `/api/v1/repayments`

Filters:
- loanId
- borrowerId
- source
- date range
- status
- pageNumber
- pageSize

## POST `/api/v1/repayments/manual`

Header:
`Idempotency-Key` required.

## GET `/api/v1/repayments/{repaymentId}`

---

# 21. Salary Deduction Batches

## POST `/api/v1/salary-deduction-batches`

Multipart upload.

Header:
`Idempotency-Key` required.

## GET `/api/v1/salary-deduction-batches/{batchId}`

## GET `/api/v1/salary-deduction-batches/{batchId}/rows`

## POST `/api/v1/salary-deduction-batches/{batchId}/post`

Idempotency required.

---

# 22. Audit

## GET `/api/v1/audit`

Permission:
`audit.read`

Filters:
- actorId
- entityType
- entityId
- action
- from
- to
- correlationId

Read-only.

---

# 23. Reporting

## GET `/api/v1/reports/applications`
## GET `/api/v1/reports/active-loans`
## GET `/api/v1/reports/disbursements`
## GET `/api/v1/reports/repayments`
## GET `/api/v1/reports/outstanding-balances`

Initial output is JSON consumed by React.

---

# 24. Money DTO

Do not use floating-point numbers.

Conceptual representation:

```json
{
  "amount": 10000.000,
  "currency": "XXX"
}
```

Server uses `decimal`.

Exact SQL precision and default currency are finalized in persistence design.

---

# 25. Date / Time

- business dates: `YYYY-MM-DD`;
- timestamps: UTC ISO-8601;
- storage/comparison: UTC;
- display timezone: frontend/user concern unless business rules require otherwise.

---

# 26. Security Boundary

React is not trusted for authorization.

Every endpoint checks permission server-side.

Frontend permissions only improve UX.

Knowing an identifier does not grant access to its resource.

---

# 27. API-to-Module Rule

Correct:

```text
POST /disbursements
      ↓
Disbursements.Application
      ↓
Disbursement Aggregate
```

Forbidden:

```text
Disbursement Endpoint
   ├── LoanAccounts Repository
   ├── Treasury Repository
   └── Reporting Repository
```

Cross-module effects occur through public contracts/events.

---

# 28. OpenAPI

ASP.NET Core will generate OpenAPI from implemented endpoints/DTOs.

After implementation begins, generated OpenAPI becomes the runtime API truth.

This document remains the architectural boundary baseline.

---

# 29. Baseline Decisions

```text
API version: /api/v1

Workflow transitions:
explicit action endpoints

Errors:
ProblemDetails + stable errorCode

Concurrency:
ETag / If-Match

Financial retries:
Idempotency-Key

Cross-module state changes:
Integration Events

React:
API DTOs only
```
