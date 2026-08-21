# TASK-16 — Salary Deduction Batch Import

    ## Objective

    Implement payroll/salary deduction import as a controlled batch that validates rows, posts valid repayments independently, and preserves row-level errors and idempotency.

    ## Dependencies

    - `TASK-15`

    ## Required Reference Documents
- `docs/loan-system-source-scope.md`
- `docs/loan-system-mvp-scope.md`
- `docs/loan-system-requirements.md`
- `docs/loan-system-scope-alignment.md`
- `docs/loan-system-application-use-cases.md`
- `docs/loan-system-persistence-design.md`
- `docs/loan-system-event-storming.md`

    ## Backend Scope

    - Implement SalaryDeductionBatch persistence/process.
- Implement upload, validate, row matching, and explicit post commands.
- Reuse Repayment posting logic for valid rows.
- Track duplicate, unmatched, invalid, posted row states.
- Make batch create/post idempotent.

    ## Frontend Scope

    - Create Salary Deduction import page with upload/validation summary.
- Create paginated row-result view.
- Create explicit Post Valid Rows action and final batch summary.

    ## Persistence / Infrastructure Scope

    - Create salary deduction batch/row migrations.

    ## Required Tests

    - Parser/matching tests.
- Integration tests for mixed valid/invalid rows, duplicate batch, duplicate row/reference, partial completion.
- Test proves successful rows are not rolled back by unrelated invalid rows under MVP policy.
- Frontend tests for validation/result/post flow.
- E2E smoke posts a small deduction batch and verifies loan balances.

    ## Acceptance Criteria

    - [ ] Invalid rows do not corrupt valid repayments.
- [ ] Batch can be safely retried.
- [ ] Row-level errors are visible.
- [ ] All quality and coverage gates pass.

## Scope / Documentation Alignment Requirements

- Implement deterministic fallback matching by Civil Number when Employee Number is absent/not used, as explicitly required by the source. Ambiguous matches must fail for review rather than be guessed.
- Real HR/payroll integration/outbound exchange remains deferred; this task covers the approved Excel deduction path.

## Required Manual UI Scenarios to Report

Codex must include detailed step-by-step manual UI verification for at least these task-specific scenarios in its final report:

- Upload a salary-deduction batch and inspect validation before posting.
- Post a batch containing valid and invalid rows and verify valid rows post independently while errors remain visible.
- Provide a row with no Employee Number but a valid Civil Number and verify deterministic Civil Number matching succeeds.
- Provide an ambiguous/unmatched Civil Number and verify it is rejected for review rather than guessed.
- Retry the same batch and verify it does not duplicate repayments.

## Mandatory Execution Protocol

Follow this sequence exactly. Do not skip a gate.

### 0. Verify Dependencies and Sync Git

Before any code change:

- verify every task listed in **Dependencies** has already been reviewed by the user and merged to `main`;
- do not continue from a previous task branch;
- start from the latest merged `main`.

```bash
git status
git switch main
git pull --ff-only origin main
```

If the working tree is dirty or the pull cannot fast-forward, **stop and report the issue**. Do not reset, discard, stash, overwrite, or merge user work automatically.

Create a fresh branch for this task:

```bash
git switch -c task/16-salary-deduction
```

Direct feature implementation on `main` is forbidden.

### 1. Mandatory Documentation / Scope Consistency Gate

Before editing code, read at minimum:

```text
docs/loan-system-source-scope.md
docs/loan-system-mvp-scope.md
docs/loan-system-requirements.md
docs/loan-system-scope-alignment.md
```

Then read **all Required Reference Documents listed in this task** and inspect the current implementation/tests.

Apply this implementation precedence:

```text
Source Scope
  ↓
Explicit approved MVP Scope decisions
  ↓
Scope Alignment decisions
  ↓
Requirements / Domain / Architecture documents
  ↓
Task file
```

The source Scope is the full requirement baseline; an item may be omitted from the current implementation only when an approved MVP decision explicitly defers/simplifies it.

Before coding, perform a short consistency check:

- confirm the task does not contradict the source Scope;
- confirm it does not re-introduce an explicitly deferred feature;
- confirm it respects Context Map, aggregates, state machines, module contracts, API and persistence boundaries;
- confirm the technical stack remains ASP.NET Core + React/TypeScript + Microsoft SQL Server + REST API + Modular Monolith;
- confirm external full-scope integrations remain behind their approved abstractions.

If a genuine contradiction is found that is not resolved by `loan-system-scope-alignment.md`, **stop before implementation and report it**. Do not silently invent a competing business rule.

### 2. Implement the Smallest Complete Vertical Slice

Complete the task end-to-end:

```text
Database / Migration (when required)
        ↓
Domain
        ↓
Application
        ↓
Infrastructure
        ↓
REST API
        ↓
React UI
        ↓
Automated Tests
```

Do not leave placeholder TODO implementations inside task scope.

For every new user-visible UI string:

- provide English and Arabic resources;
- verify English LTR and Arabic RTL behavior;
- do not put authoritative business rules only in React.

### 3. Automated Test Gate

Run all existing tests plus tests added by the task.

Backend baseline:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Frontend baseline:

```bash
npm ci
npm run typecheck
npm run lint
npm run test -- --run
```

Use the package-manager commands standardized by TASK-00 if different.

Integration tests must use the repository-standard real SQL Server container/Testcontainers setup rather than replacing relational behavior with an in-memory fake.

### 4. Coverage Gate

Coverage must be measured.

Minimum task gate:

- **new/changed backend business code:** >= 90% line coverage;
- **Domain/Application code introduced by the task:** target >= 95%;
- **new/changed frontend logic/components:** >= 85% line coverage;
- overall coverage must not materially regress;
- critical business invariants, authorization, negative states, concurrency and idempotency paths must have explicit tests even if numeric coverage is already high.

Generated migrations/OpenAPI clients/generated files may be excluded where appropriate.

Do not add meaningless tests solely to inflate coverage.

### 5. Local Quality Gate

Backend:

```bash
dotnet format --verify-no-changes
dotnet build
dotnet test
```

Also run configured analyzers and architecture tests.

Frontend:

```bash
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Review explicitly for:

- DDD aggregate/invariant correctness;
- Clean Architecture dependency direction;
- no cross-module DbContext/table access;
- cross-module reads through contracts and state changes through approved events;
- no business logic in endpoints or React components;
- no duplicated rules or magic values;
- no secrets/sensitive data in source or logs;
- appropriate concurrency/idempotency;
- English/Arabic localization and RTL behavior;
- no shortcut that makes a known deferred source requirement materially harder to add later.

### 6. Restart the Application and Leave It Ready for User Testing

After implementation and local automated gates pass, restart the complete local application so the user can test the exact task branch.

Preferred full-stack sequence:

```bash
docker compose down
docker compose up --build -d
docker compose ps
```

Do **not** use `down -v` unless the task explicitly requires/describes a destructive clean-database test. Preserve normal development volumes/data.

If the repository-standard workflow runs API/React locally for hot reload, restart those processes as well; the final state must still have the relevant API/UI/database running and healthy.

Verify and report:

- SQL Server health;
- API readiness;
- frontend availability;
- OpenAPI URL;
- relevant login/test role information without exposing secrets.

Run the task happy-path smoke scenario and at least one negative scenario against the real running API/UI.

**Leave the application running and ready for the user's manual UI verification at the end of the task.**

### 7. Prepare Manual UI Test Scenarios for the User

The final report must contain a section named exactly:

```text
Manual UI Test Scenarios
```

For every scenario provide:

1. scenario name;
2. required login role/user type;
3. preconditions/test data;
4. exact page/menu/URL to open;
5. step-by-step actions/clicks/data to enter;
6. expected visible result;
7. what must **not** happen;
8. cleanup/reset step if needed.

Include the task-specific manual scenarios listed earlier in this file. Cover at minimum the main happy path and a meaningful negative/permission/validation/state scenario where applicable.

### 8. Commit, Push, Open Pull Request — Never Merge

Only after local tests, coverage, quality and runtime smoke checks pass:

```bash
git status
git add .
git commit -m "feat: complete TASK-16 salary-deduction"
git push -u origin task/16-salary-deduction
```

Create a Pull Request from the task branch to `main` using GitHub tooling available in the environment.

The PR description must summarize:

- task scope;
- source/architecture documents reviewed;
- migrations;
- tests and coverage;
- manual UI scenarios;
- known limitations/deferred source items touched.

**Codex must never merge the Pull Request.**

If CI is available, monitor the PR checks synchronously when tooling permits (for example `gh pr checks --watch`). If a check fails, fix the task on the same branch, push again, and rerun checks until green.

If CI status cannot be accessed because of permissions/tooling, report that explicitly. The task is not merge-ready until required GitHub checks are green.

Stop after creating/updating the green PR. The **user performs manual UI testing, PR review, and the merge to `main`**.

### 9. Final Task Report

Return a concise but complete report containing:

1. Implementation Summary
2. Scope Consistency Check: PASS/FAIL
3. Source/Architecture Documents Reviewed
4. Files Changed
5. Database Migrations
6. Automated Tests Added/Updated
7. Exact Test Commands and Results
8. Backend Coverage
9. Frontend Coverage
10. Architecture Test Results
11. Formatter / Analyzer / Lint / Typecheck Results
12. Production Build Result
13. Local Runtime Status
14. Frontend / API / OpenAPI / Health URLs
15. **Manual UI Test Scenarios**
16. Known Limitations / Explicitly Deferred Scope Items
17. Branch Name
18. Commit SHA
19. Pull Request URL
20. GitHub CI Status
21. Explicit statement: `PR NOT MERGED — waiting for user review and manual merge.`

### Definition of Done

This task is **not done** unless:

- [ ] Dependency tasks are already merged to `main`.
- [ ] Git was synced from latest `main` before changes.
- [ ] Work was performed on a fresh task branch, not `main`.
- [ ] Source Scope/MVP/Scope Alignment consistency check passed.
- [ ] Backend requirement is complete.
- [ ] Frontend requirement is complete where applicable.
- [ ] New UI is English/Arabic localized and RTL-aware.
- [ ] Required persistence/migrations are complete.
- [ ] Happy-path and required negative tests pass.
- [ ] All pre-existing tests still pass.
- [ ] Coverage gates pass.
- [ ] Architecture tests pass.
- [ ] Formatting/analyzers/lint/typecheck pass.
- [ ] Production builds succeed.
- [ ] Application was restarted after implementation.
- [ ] API/frontend/database are healthy and the application is left running for user testing.
- [ ] Task smoke verification passes.
- [ ] Manual UI Test Scenarios are provided with steps and expected results.
- [ ] Branch is committed and pushed.
- [ ] Pull Request to `main` is created.
- [ ] Required GitHub CI checks are green (or clearly reported as blocked by repository permissions before merge).
- [ ] Codex did **not** merge the PR.
- [ ] No unresolved TODO remains inside task scope.
