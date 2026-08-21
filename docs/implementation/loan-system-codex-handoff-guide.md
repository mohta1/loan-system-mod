# Loan Management System — Codex Task Handoff Guide

## How to Use

Give Codex exactly one `TASK-XX` file at a time. Keep all project documents under `docs/` so the repository-relative references in each task resolve correctly.

Recommended instruction:

```text
Implement this task exactly as specified. Before coding, read the mandatory Source Scope, MVP Scope, Scope Alignment and every Required Reference Document. Do not widen scope or contradict those documents. Complete every local test/coverage/quality/runtime gate, leave the application running for my manual UI test, push a task branch, create a PR to main, wait for required CI checks to pass, then STOP. Never merge the PR.
```

## Source Consistency Is Mandatory

The source hierarchy is:

```text
docs/loan-system-source-scope.md
  ↓
docs/loan-system-mvp-scope.md
  ↓
docs/loan-system-scope-alignment.md
  ↓
requirements/domain/architecture docs
  ↓
TASK file
```

A full-source item may be deferred only when the approved MVP/alignment explicitly says so. If a conflict is unresolved, Codex must stop before implementation and report it.

## What to Review Before You Merge a PR

Check the Codex final report and confirm:

- the task started from latest merged `main`;
- work happened on a fresh task branch;
- Source/MVP/architecture consistency check is PASS;
- backend + frontend requirements are complete;
- new UI supports English and Arabic/RTL;
- automated tests actually ran;
- coverage numbers were measured and gates passed;
- architecture tests, formatter/analyzers, lint/typecheck and production builds passed;
- the full application was restarted and is still running locally;
- Codex supplied detailed **Manual UI Test Scenarios** with steps and expected results;
- you manually tested the relevant UI scenarios;
- a Pull Request exists;
- required GitHub CI checks are green;
- Codex did not merge the PR.

Only then manually merge the PR to `main`. The next task must start from that newly merged `main`.

## If Codex Finds a Blocker

Codex must not widen scope or hide the issue behind mocks/disabled tests. It should identify the conflicting documents/requirement, propose the smallest compatible resolution, and leave the repository non-destructive.
