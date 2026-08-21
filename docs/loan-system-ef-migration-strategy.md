# Loan Management System — EF Core Migration Strategy

## 1. Ownership

Each module owns migrations for its own schema.

Example:

```text
Modules/Borrowers/.../Infrastructure/Persistence/Migrations
Modules/LoanOrigination/.../Infrastructure/Persistence/Migrations
Modules/Treasury/.../Infrastructure/Persistence/Migrations
```

A module migration may change only:
- its own schema;
- its own indexes and constraints;
- its own Outbox/Inbox tables.

Host technical migrations may change only `platform.*`.

## 2. Naming

Use explicit names:

```text
Platform_Initial
IdentityAccess_Initial
Borrowers_Initial
LoanProducts_Initial
LoanOrigination_Initial
LoanAccounts_Initial
Disbursements_Initial
Treasury_Initial
Repayments_Initial
Documents_Initial
Audit_Initial
Reporting_Initial
```

Later:

```text
LoanAccounts_AddDisbursementReservations
Treasury_AddPaymentAttempts
```

## 3. Bootstrap Order

Recommended initial order:

```text
1. Platform
2. IdentityAccess
3. Documents
4. Borrowers
5. LoanProducts
6. LoanOrigination
7. LoanAccounts
8. Disbursements
9. Treasury
10. Repayments
11. Audit
12. Reporting
```

There are no cross-module SQL FKs, so correctness does not depend on a hidden relational dependency graph.

## 4. Migration Review Checklist

Review every migration for:

- correct schema;
- accidental table drop;
- accidental cross-module FK;
- money precision;
- nullability;
- unique indexes;
- regular indexes;
- rowversion;
- default values;
- destructive data changes;
- unexpected cascade behavior.

## 5. Production Execution

Preferred:

```text
Build
  ↓
Run migration command/job
  ↓
Verify
  ↓
Deploy API
```

Do not run destructive production migrations automatically on every application startup.

## 6. Backward Compatibility

After real production usage starts, prefer expand/contract:

```text
Add new structure
Deploy compatible code
Backfill
Switch reads/writes
Remove old structure later
```

Do not assume schema and application versions change atomically.

## 7. Seed Data

Good seed candidates:
- permission catalog;
- development roles;
- development admin user;
- optional demo products in development.

Do not seed:
- production secrets;
- real production users;
- changing production loan amounts/rules;
- organization-specific operational data hidden in migrations.

## 8. Development Reset

Provide an explicit dev-only reset workflow:

```text
drop development DB
create DB
apply all migrations
load development seed
```

Never execute this automatically in production.

## 9. Integration-Test Database

Integration tests should:

```text
start isolated SQL Server
apply all migrations
run tests
dispose container
```

Preferred tooling:
`Testcontainers for .NET`.

This makes migration bootstrap part of CI verification.

## 10. Baseline

```text
Module-owned migrations
Explicit production migration step
No cross-module schema mutation
Migration review required
Testcontainers verifies clean bootstrap
```
