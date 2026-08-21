# Loan Management System — Source Scope to MVP Task Traceability

## 1. Purpose

This matrix records the implementation-plan review against the supplied source Scope and identifies where each source area is implemented, simplified, or explicitly deferred.

Status values:

- **MVP** — implemented in the current task plan.
- **MVP-Simplified** — source capability is represented in a deliberately smaller MVP form.
- **Deferred** — explicitly postponed by the approved MVP scope/alignment.

---

| Source Scope Area | MVP Status | Primary Task(s) | Alignment Notes |
|---|---|---|---|
| User management / permissions | MVP | TASK-01 | Local identity for MVP; Active Directory adapter later |
| Active Directory login | Deferred | TASK-01 boundary only | Replaceable identity provider retained |
| MOD HR borrower integration | Deferred | TASK-03/04 boundary | Manual/Excel MVP; HR adapter later |
| External-org borrower Excel | MVP | TASK-04 | Validate → preview → execute |
| Borrower master data | MVP | TASK-03 | Nationality added for eligibility |
| Borrower physical deletion | MVP-Simplified | TASK-03 | Deactivation/history preservation; future explicit delete/merge rules |
| Configurable loan types | MVP | TASK-05 | Versioned products |
| Purchase existing / build new financing | MVP | TASK-05/06 | Configurable financing types |
| Add future financing types | MVP | TASK-05 | Data/configuration driven |
| Change financing type before disbursement | MVP | TASK-12 | Allowed before reservation/payment, audited and policy-safe |
| Loan due date / deduction rules | MVP | TASK-05 | Configurable capability |
| Omani nationality rule | MVP | TASK-03/05/07 | Configurable required-nationality rule |
| Single-application rule | MVP | TASK-05/07 | Configurable application-count policy |
| Rank/grade-based amount calculation | MVP | TASK-05/07 | Configurable rule, not hard-coded |
| Application registration / initial docs | MVP | TASK-06/07/10 | Internal staff flow |
| Unit approval | MVP | TASK-08 | Queue + approve/reject |
| Committee approval | MVP | TASK-09 | Queue + approve/reject |
| Waiting-list sequence / exception nominations | Deferred | — | Exact prioritization/exception rules not provided |
| Property inspection | MVP | TASK-10 | Property/location/details + decision |
| Geographic inspection scheduling | Deferred | — | Explicit MVP deferral |
| Ownership/survey/engineering docs | MVP | TASK-02/10 | File adapter + document references |
| Contractor/property-owner bank details | MVP | TASK-12/14 | Beneficiary/payment snapshot |
| Mortgage prerequisite/status | MVP-Simplified | TASK-10 | Full letters/integration deferred |
| Administrative disbursement Technical→Accounting→Higher | MVP | TASK-13 | Ordered workflow |
| Return transaction to prior admin stage | MVP | TASK-13 | Non-terminal ReturnForCorrection |
| Insurance claims | Deferred | — | Explicit MVP deferral |
| Treasury Input→Auditor→Approver | MVP | TASK-14 | Maker/checker flow |
| Return pre-payment Treasury transaction | MVP | TASK-14 | Prior-stage/admin return with reason |
| Real B2B payment | Deferred | TASK-14 adapter | Fake gateway MVP, real adapter later |
| Paid amount ≤ approved loan amount | MVP | TASK-12/14 | LoanAccount reservation + concurrency invariant |
| MOD salary deduction HR integration | Deferred | TASK-16 boundary | Real HR integration later |
| External/pension deduction Excel | MVP | TASK-16 | Batch import |
| Civil Number deduction matching | MVP | TASK-16 | Explicit fallback, ambiguity rejected |
| Manual/full/partial repayment with receipt | MVP | TASK-15 | Receipt via Documents |
| Loan account balance | MVP | TASK-11/12/14/15/16 | Disbursed/repaid/outstanding |
| Monthly bank reconciliation | Deferred | — | Explicit MVP deferral |
| Monthly account closing | Deferred | — | Explicit MVP deferral |
| Loan fund balance management | Deferred | — | Explicit MVP deferral |
| Annual life insurance | Deferred | — | Explicit MVP deferral |
| Loan exemption | Deferred | — | Explicit MVP deferral |
| Loan transfer | Deferred | — | Explicit MVP deferral |
| Loan closure due to special reasons/death | Deferred | — | Normal fully-repaid close remains; exception flow later |
| Deduction postponement | Deferred | — | Explicit MVP deferral |
| Resigned-borrower special debts | Deferred | — | Explicit MVP deferral |
| Loan cancellation before any payment | MVP | TASK-07/11/13 | Application/loan cancellation with `TotalDisbursed == 0` |
| Paid/received amount correction | Deferred | — | Post-payment correction explicitly deferred |
| Administrative/financial reports | MVP | TASK-18 | Operational reports |
| Power BI / smart self-service reports | Deferred | — | Explicit MVP deferral |
| ASP.Net | MVP | TASK-00 | ASP.NET Core |
| MSSQL | MVP | TASK-00 | SQL Server + EF Core |
| Arabic + English interface | MVP | TASK-00 + every UI task | i18n + LTR/RTL mandatory |
| File Server attachments | Deferred integration | TASK-02 boundary | Local adapter MVP; FileServer adapter later |
| Full legacy data/attachment migration | Deferred | — | Explicit MVP deferral |
| Training | Deferred delivery activity | — | Not an MVP software slice |
| 2+ years technical support | Deferred delivery/service obligation | — | Not an MVP feature slice |
| Architecture/UI/DB/integration documentation | MVP baseline | Existing docs + TASK-19 review | Documentation maintained in repo |
| Borrower-facing tracking/self-service | Deferred | — | Borrower Portal explicitly excluded from MVP |

---

## 2. Review Result

After the task-plan review, the previously hidden source-scope gaps are now explicitly resolved by `loan-system-scope-alignment.md` and the revised task files.

The important changes made to the task plan are:

1. GitHub Actions CI and main-branch PR protection are established in TASK-00 before business feature work.
2. Every task starts from latest merged `main`, uses a fresh branch, creates a PR, waits for required CI, and **never merges**.
3. Every task restarts the application and leaves it running for user manual verification.
4. Every task returns detailed Manual UI Test Scenarios with steps and expected results.
5. Every task must read Source Scope + MVP Scope + Scope Alignment before implementation.
6. English/Arabic + RTL is mandatory from TASK-00 onward.
7. Nationality, configurable one-application rule, rank/grade amount calculation, financing-type requirements, Civil Number deduction matching, pre-payment return transitions, and zero-disbursement cancellation are explicitly represented.
8. Full-scope items intentionally excluded from MVP remain explicitly deferred rather than being accidentally omitted.

No unresolved task-plan blocker remains for starting TASK-00. If implementation discovers a new contradiction in the actual repository/docs, the task protocol requires Codex to stop before coding that conflicting behavior.
