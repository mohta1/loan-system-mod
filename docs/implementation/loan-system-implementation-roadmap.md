# Loan Management System — Implementation Roadmap

## 1. Purpose

This roadmap converts the completed architecture/domain design into small executable vertical slices intended to be given to Codex one by one.

Every task is independently gated by Git sync, backend/frontend completion, automated tests, measured coverage, code-quality checks, and runnable smoke verification.


## Mandatory Repository Workflow

Before business-feature work begins, TASK-00 establishes GitHub Pull Request CI and the main-branch merge guard. Every later task follows:

```text
latest merged main
  → fresh task branch
  → scope/document consistency check
  → backend + frontend vertical slice
  → local tests + measured coverage + quality gates
  → restart full application and leave it running
  → manual UI scenarios for user
  → commit + push
  → Pull Request to main
  → GitHub CI green
  → STOP (Codex never merges)
  → user manually tests/reviews/merges
```

Every UI slice is bilingual English/Arabic and must verify LTR/RTL.

## 2. Execution Rule

Execute tasks in numeric order unless the dependency graph explicitly permits parallel work. Do not start a task if one of its dependencies is incomplete.

## 3. Task Sequence

| Task | Title | Depends On | Deliverable |
|---|---|---|---|
| TASK-00 | Bootstrap Solution, Frontend, Tests, and Docker | — | `TASK-00-bootstrap-runtime.md` |
| TASK-01 | Local Identity, Permissions, and Login | TASK-00 | `TASK-01-identity-access.md` |
| TASK-02 | Document Upload and Retrieval | TASK-01 | `TASK-02-documents.md` |
| TASK-03 | Borrower Management CRUD | TASK-01 | `TASK-03-borrowers-crud.md` |
| TASK-04 | Borrower Excel Import | TASK-02, TASK-03 | `TASK-04-borrower-import.md` |
| TASK-05 | Loan Products and Versioning | TASK-01 | `TASK-05-loan-products.md` |
| TASK-06 | Loan Application Draft | TASK-03, TASK-05 | `TASK-06-loan-application-draft.md` |
| TASK-07 | Eligibility Evaluation and Application Submission | TASK-06 | `TASK-07-eligibility-submit.md` |
| TASK-08 | Unit Approval Workflow | TASK-07 | `TASK-08-unit-approval.md` |
| TASK-09 | Loan Committee Approval Workflow | TASK-08 | `TASK-09-committee-approval.md` |
| TASK-10 | Property Inspection and Mortgage Prerequisites | TASK-02, TASK-09 | `TASK-10-inspection-mortgage.md` |
| TASK-11 | Final Application Approval and Loan Account Opening | TASK-10 | `TASK-11-final-approval-loan-account.md` |
| TASK-12 | Disbursement Request and Capacity Reservation | TASK-11 | `TASK-12-disbursement-capacity.md` |
| TASK-13 | Administrative Disbursement Approvals | TASK-12 | `TASK-13-disbursement-approvals.md` |
| TASK-14 | Treasury Maker-Checker and Fake Payment Execution | TASK-13 | `TASK-14-treasury-payment.md` |
| TASK-15 | Manual Repayment | TASK-02, TASK-14 | `TASK-15-manual-repayment.md` |
| TASK-16 | Salary Deduction Batch Import | TASK-15 | `TASK-16-salary-deduction.md` |
| TASK-17 | Immutable Audit Trail | TASK-11, TASK-14, TASK-15 | `TASK-17-audit-trail.md` |
| TASK-18 | Operational Reporting | TASK-14, TASK-16 | `TASK-18-reporting.md` |
| TASK-19 | MVP End-to-End Hardening and Release Candidate | TASK-17, TASK-18 | `TASK-19-mvp-hardening.md` |

## 4. Milestones

### Milestone A — Runnable Platform

```text
TASK-00 → TASK-02
```

Result: runnable Docker/dev stack, authentication/authorization, reusable document storage.

### Milestone B — Origination Core

```text
TASK-03 → TASK-11
```

Result: borrower/product setup and complete application-to-LoanAccount flow.

### Milestone C — Financial Core

```text
TASK-12 → TASK-16
```

Result: disbursement reservation/approval, Treasury payment, manual and salary-deduction repayments.

### Milestone D — Operational MVP

```text
TASK-17 → TASK-19
```

Result: audit, reporting, full E2E hardening, Dockerized release candidate.

## 5. Non-Negotiable Task Gates

- Git must be synced from remote before each task.
- Every business slice must include backend and React UI where user interaction exists.
- Tests must be added in the same task as the feature.
- All existing tests must remain green.
- New/changed backend business code must achieve >=90% line coverage; Domain/Application target >=95%.
- New/changed frontend logic must achieve >=85% line coverage.
- Architecture tests, analyzers, formatter, frontend lint/typecheck, production builds must pass.
- A real smoke test must prove the slice runs.
- A task with failing tests or failed quality gates is incomplete.

## 6. Scope Control

Codex must not add unrelated product features while completing a task. If it discovers a missing prerequisite outside the task, it should report it rather than silently widening scope.

## 7. Completion

TASK-19 produces the MVP release candidate. After that, work should shift from initial implementation to defect fixing, stakeholder feedback, and explicitly prioritized post-MVP increments.