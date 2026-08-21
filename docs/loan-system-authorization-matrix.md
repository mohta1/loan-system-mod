# Loan Management System — Authorization Matrix

## 1. Purpose

This document defines the initial MVP role-to-permission baseline.

Roles are operational groupings. Backend authorization should evaluate permissions rather than hard-coded role names.

## 2. Initial Roles

1. System Administrator
2. Loan Management Officer
3. Unit Officer
4. Loan Committee Member
5. Property Inspector / Technical Affairs Officer
6. Accounting Officer
7. Higher Administrative Approver
8. Treasury Input User
9. Treasury Auditor
10. Treasury Approver
11. Reporting / Audit Viewer

## 3. Permission Matrix

| Permission | Admin | Loan Mgmt | Unit | Committee | Inspector/Technical | Accounting | Higher | Treasury Input | Treasury Auditor | Treasury Approver | Viewer |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| identity.users.manage | ✓ |  |  |  |  |  |  |  |  |  |  |
| borrowers.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  |  | ✓ |
| borrowers.create | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| borrowers.update | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| borrowers.manageStatus | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| borrowers.import | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanProducts.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  |  | ✓ |
| loanProducts.manage | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanProducts.publish | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanProducts.manageStatus | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanApplications.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  |  | ✓ |
| loanApplications.create | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanApplications.update | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanApplications.evaluateEligibility | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanApplications.submit | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| loanApplications.unitApprove | ✓ |  | ✓ |  |  |  |  |  |  |  |  |
| loanApplications.committeeApprove | ✓ |  |  | ✓ |  |  |  |  |  |  |  |
| loanApplications.mortgageManage | ✓ | ✓ |  |  | ✓ |  |  |  |  |  |  |
| loanApplications.finalApprove | ✓ |  |  |  |  |  | ✓ |  |  |  |  |
| loanApplications.cancel | ✓ | ✓ |  |  |  |  |  |  |  |  |  |
| inspections.create | ✓ |  |  |  | ✓ |  |  |  |  |  |  |
| inspections.approve | ✓ |  |  |  | ✓ |  |  |  |  |  |  |
| loans.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| loans.close | ✓ | ✓ |  |  |  | ✓ | ✓ |  |  |  |  |
| disbursements.read | ✓ | ✓ |  |  | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| disbursements.create | ✓ | ✓ |  |  | ✓ |  |  |  |  |  |  |
| disbursements.technicalApprove | ✓ |  |  |  | ✓ |  |  |  |  |  |  |
| disbursements.accountingApprove | ✓ |  |  |  |  | ✓ |  |  |  |  |  |
| disbursements.finalApprove | ✓ |  |  |  |  |  | ✓ |  |  |  |  |
| disbursements.cancel | ✓ | ✓ |  |  | ✓ | ✓ | ✓ |  |  |  |  |
| treasury.read | ✓ |  |  |  |  | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| treasury.input | ✓ |  |  |  |  |  |  | ✓ |  |  |  |
| treasury.audit | ✓ |  |  |  |  |  |  |  | ✓ |  |  |
| treasury.approve | ✓ |  |  |  |  |  |  |  |  | ✓ |  |
| treasury.execute | ✓ |  |  |  |  |  |  |  |  | ✓ |  |
| repayments.read | ✓ | ✓ |  |  |  | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| repayments.create | ✓ | ✓ |  |  |  | ✓ |  |  |  |  |  |
| repayments.import | ✓ | ✓ |  |  |  | ✓ |  |  |  |  |  |
| audit.read | ✓ |  |  |  |  |  | ✓ |  | ✓ | ✓ | ✓ |
| reports.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

## 4. Separation of Duties

Potential server-side rules:

```text
Treasury Input Actor != Treasury Audit Actor
Treasury Audit Actor != Treasury Final Approver
```

Potentially:

```text
Disbursement Creator != Final Administrative Approver
```

These rules are separate from role/permission assignment.

## 5. Permission Naming

Use:

```text
<module>.<capability>
```

Examples:

```text
borrowers.read
loanApplications.committeeApprove
disbursements.accountingApprove
treasury.execute
```

Do not use UI page names as authorization primitives.

## 6. Backend Enforcement

Authorization has two separate checks:

```text
Permission:
Can this actor request the operation?

Domain invariant:
Can the resource perform the operation now?
```

Both must succeed.

## 7. Baseline

This matrix is an MVP baseline.

Changing role assignments later should not require changing domain logic.
