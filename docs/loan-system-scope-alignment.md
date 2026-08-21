# Loan Management System — Source Scope / MVP Alignment

## 1. Purpose

This document reconciles the authoritative source scope with the approved MVP and the implementation task plan.

It exists to prevent Codex or developers from silently implementing a task that contradicts the source tender requirements or previously approved architecture decisions.

---

## 2. Implementation Source-of-Truth Order

Before implementing any task, apply this order:

1. **`docs/loan-system-source-scope.md`** — authoritative statement of the full source business/technical requirements.
2. **`docs/loan-system-mvp-scope.md`** — explicit project-owner decisions about what is included/deferred for the current MVP.
3. **This alignment document** — explicit reconciliation of gaps and source requirements that were not previously called out strongly enough.
4. **`docs/loan-system-requirements.md`** — MVP requirements baseline.
5. Domain/architecture documents: Context Map, Event Storming, Aggregate Design, State Machines, Use Cases, Module Contracts, API Boundary, Authorization, Solution/Frontend/Persistence/Testing architecture.
6. Individual implementation Task file.

A lower-level document must not silently override a higher-level requirement.

If Codex finds a real contradiction that is not resolved here, it must **stop before coding**, identify the conflicting statements, and report the smallest compatible resolution. It must not guess by silently changing business behavior.

---

# 3. Source Requirements Explicitly Required in the MVP Task Plan

## 3.1 ASP.NET + MSSQL

Status: **Required in MVP**

Implementation:

- ASP.NET Core backend
- Microsoft SQL Server
- Entity Framework Core

No task may replace SQL Server with PostgreSQL or another database without an explicit architecture decision.

---

## 3.2 Arabic and English UI

Status: **Required in MVP**

The source explicitly requires Arabic and English.

Implementation rule:

- TASK-00 establishes frontend internationalization and LTR/RTL support.
- Every later task that adds user-visible UI must add both English and Arabic translations for its new strings.
- Arabic layout must be verified in RTL mode.
- Business data itself is not automatically translated unless the business field requires localized values.

---

## 3.3 Configurable Loan Types and Conditions

Status: **Required in MVP**

TASK-05/TASK-07 must support configuration rather than hard-coded controller/UI rules.

At minimum the MVP rule model must be capable of representing:

- maximum/approved amount rules;
- deduction percentage;
- effective period;
- financing type;
- due-date/term configuration where used;
- nationality requirement;
- maximum/allowed application-count rule;
- rank/grade-based amount calculation.

Source examples such as 30%/40% deductions are not permanent constants.

---

## 3.4 Omani Nationality and Single-Application Conditions

Status: **Required as configurable eligibility capabilities in MVP**

Resolution:

- Borrower data must include a nationality value required by the eligibility decision.
- Eligibility must be capable of enforcing a configured required nationality, including Omani where configured.
- Eligibility must be capable of enforcing the configured maximum number of applications/loans, including a one-application rule where configured.

Do not hard-code the rule directly into React or controllers.

---

## 3.5 Rank/Grade-Based Loan Amount

Status: **Required in MVP**

Eligibility/amount calculation must support deriving the permitted/proposed amount from rank/grade configuration rather than using an unexplained fixed amount.

---

## 3.6 Housing Financing Types

Status: **Required in MVP**

The source explicitly identifies:

- purchase existing house;
- build new house.

The product model must allow new financing types to be added.

### Change before disbursement

The source requires switching financing type before disbursement.

MVP safe interpretation:

- an authorized user may change the active financing type **before any disbursement capacity has been reserved or paid**;
- the new type must be allowed by the approved product version;
- the change must be audited;
- approved financial amount is not silently increased by this change;
- if a change would require different eligibility/amount rules, the operation must fail until the relevant eligibility/re-approval behavior is explicitly satisfied.

This behavior is added to the implementation plan rather than allowing an unsafe silent mutation after payment has begun.

---

## 3.7 Approval Queues

Status: **Required in MVP**

Role-specific queues are implemented for Unit, Committee, Technical/Disbursement, and Treasury stages.

Advanced waiting-list prioritization/exception nomination rules are not fully defined and are not invented by the MVP.

---

## 3.8 Return to Previous Approval Stage

Status: **Required for pre-payment approval workflows in MVP**

The source explicitly requires the administrative disbursement transaction to be returnable to the prior stage.

MVP transition rule:

- Technical Affairs may return the request to the request/correction state.
- Accounting may return it to Technical Affairs.
- Higher Officer may return it to Accounting.
- Treasury Auditor may return it to Treasury Input.
- Treasury Approver may return it to Treasury Audit.
- Treasury Input may return a not-yet-paid transaction to the administrative side when correction is required.

Every return requires:

- actor;
- timestamp;
- reason;
- from-state;
- target stage;
- audit event/history.

**Post-payment financial correction/reversal remains deferred**, because the approved MVP explicitly defers payment corrections and the source does not provide enough accounting semantics to invent them safely.

---

## 3.9 Loan Cancellation Before Payment

Status: **Required in MVP**

Source rule: cancellation is allowed while no amount has been paid.

MVP invariant:

```text
TotalDisbursed == 0
```

Additional implementation safety:

- an application may be cancelled before LoanAccount creation according to its workflow rules;
- an opened LoanAccount may be cancelled only while no amount has been disbursed;
- active disbursement reservations/requests must be cancelled/released before final LoanAccount cancellation;
- cancellation is audited;
- after any successful payment, the simple cancellation path is rejected.

---

## 3.10 Civil Number Matching for External Salary Deductions

Status: **Required in MVP**

TASK-16 must support matching deduction rows by Civil Number where Employee Number is absent/not used by the source organization.

Matching rules must be deterministic and ambiguous matches must be rejected for review rather than guessed.

---

## 3.11 Contractor / Property Owner Bank Details

Status: **Required in MVP payment/disbursement flow**

The MVP stores the relevant beneficiary/bank details as the approved payment beneficiary snapshot used by Disbursement/Treasury.

Sensitive data must be handled according to security/logging rules and must not be unnecessarily written to logs.

---

# 4. Full-Source Requirements Explicitly Deferred from MVP

The following are source requirements but were deliberately deferred by the approved MVP scope:

- Borrower Portal / borrower self-service
- Real Active Directory integration
- Real HR API integration
- Real B2B bank integration
- Production File Server integration where unavailable
- Power BI integration / smart self-service reporting
- Geographic inspector scheduling / route grouping optimization
- Insurance claims
- Annual life-insurance processing
- Monthly bank reconciliation
- Monthly account closing
- Loan fund balance management
- Loan exemption
- Loan transfer
- Deduction postponement
- special resigned-borrower debt handling
- post-payment correction/recovery flows
- full legacy data/attachment migration

Architecture must continue to preserve boundaries/adapters so these can be introduced later without rewriting the core domain.

---

# 5. Source Requirements Simplified in MVP

## 5.1 Borrower Deletion

The full source mentions deletion for invalid/duplicate records.

MVP behavior is **deactivate rather than hard-delete** for established borrower master records, preserving audit/history.

Duplicate/import errors should be rejected before creation where possible.

A future explicit deletion/merge policy may be added when legal/data-retention rules are known.

## 5.2 Committee Waiting-List Sequence / Exception Cases

The MVP provides status-based review queues and normal committee approval.

Advanced priority sequence and exception-case nomination are deferred until exact rules are supplied.

## 5.3 Mortgage Letters

The MVP tracks mortgage prerequisites/status and document references.

Automated generation/integration of Ministry of Housing mortgage/release letters is deferred unless a later task explicitly adds the required templates/rules.

## 5.4 Payroll Outbound Exchange

The full scope includes sending information to payroll/other entities for deductions.

The MVP implements inbound salary-deduction batch processing and keeps HR integration behind an adapter. Real outbound payroll exchange is deferred with HR integration.

## 5.5 Reporting

The MVP provides operational reports/read models.

Power BI and arbitrary end-user smart-report authoring are deferred.

---

# 6. Task Implementation Consistency Gate

For every task Codex must explicitly confirm in its final report:

```text
Scope consistency check: PASS / FAIL
Documents reviewed: ...
Contradictions found: none / list
Deferred source items touched by this task: none / list
```

A task is not complete if it implements a shortcut that makes a known deferred source requirement materially harder to add later without documenting an architecture decision.

---

# 7. Architecture Amendments Required by Scope Alignment

These amendments resolve source requirements that were missing or underspecified in earlier design documents. For these specific points, this section supersedes the older diagrams/API/persistence examples.

## 7.1 Borrower Model Amendment

Add to the Borrower aggregate/API/persistence model:

```text
Nationality
```

This value is available to configurable eligibility policies. It is not a UI-only field.

## 7.2 Loan Product / Eligibility Amendment

The published product/rule configuration must be capable of representing:

```text
RequiredNationality
MaximumApplicationCount
RankGradeAmountRules
DeductionPercentage
LoanTerm / DueDateRule
AllowedFinancingTypes
```

The exact physical representation may use the previously approved versioned eligibility/configuration structure; do not create hard-coded `if Officer ...` logic in API/UI.

## 7.3 Loan Account Amendment

LoanAccount must retain the active/current financing type (or equivalent approved financing-state reference) needed to support the source-required pre-disbursement financing-type change.

Add/allow:

```text
FinancingType
Cancelled status
CancelledAtUtc / cancellation metadata as appropriate
```

Cancellation invariant remains:

```text
TotalDisbursed == 0
```

and no unresolved active reservation/payment may remain.

## 7.4 Disbursement State-Machine Amendment

Earlier diagrams showed Approve/Reject only. Add non-terminal **ReturnForCorrection** transitions:

```text
Requested / Technical review
  └─ Return → Request/Correction state

TechnicalApproved / Accounting review
  └─ Return → Technical review

AccountingApproved / Higher review
  └─ Return → Accounting review
```

A return is not a rejection and does not silently discard the request. Reason/history are mandatory.

Exact status names may differ, but the behavior must be explicit and tested.

## 7.5 Treasury State-Machine Amendment

Before successful payment execution, add:

```text
Auditor review
  └─ Return → Input

Approver review
  └─ Return → Auditor review

Treasury Input
  └─ Return → Administrative side for correction
```

Once payment is successfully completed, the MVP does not invent reversal/correction accounting. That is an explicitly deferred full-scope process.

## 7.6 REST API Amendment

Use explicit business-command endpoints rather than arbitrary status updates. Suggested boundary additions:

```text
POST /api/v1/loans/{loanId}/cancel
POST /api/v1/loans/{loanId}/financing-type

POST /api/v1/disbursements/{disbursementId}/return
POST /api/v1/treasury-payments/{paymentId}/return
```

Request payloads must include the reason where a return/correction action requires it.

The implementation may use a decision endpoint with an explicit `return` decision if that is consistent with the existing API style, but it must not expose a generic free-form status mutation.

## 7.7 Persistence Amendment

Earlier persistence examples are amended as follows:

- `borrowers.borrowers`: add nationality.
- Loan product version/configuration: persist the scope-aligned configurable rule capabilities.
- `loan_accounts.loan_accounts`: persist current financing type and cancellation state/metadata as required.
- Disbursement/Treasury approval history must preserve return actions/reasons.
- Beneficiary/bank details required for payment must be persisted as an appropriate protected snapshot/reference without unnecessary logging.

All original module-ownership, DbContext, schema, rowversion, Outbox/Inbox, and no-cross-module-FK rules remain unchanged.
