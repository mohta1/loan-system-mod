# Deferred Follow-ups

Only issues explicitly deferred by the user are recorded here.

## DFU-001

- **Origin:** TASK-01 / PR #2
- **Area:** IdentityAccess / Create User UX
- **Priority:** Low
- **Status:** Deferred until after all implementation tasks

**Observed:** Create User opens with pre-filled/default field values.

**Expected:** A fresh Create User form starts with blank Username, Display Name, and Password fields, with no unintended role selections. It must not reuse previous, demo, or seed form values.

This is not implemented in TASK-02.

## DFU-002

- **Origin:** TASK-01 / PR #2
- **Area:** IdentityAccess / User lifecycle
- **Priority:** Low
- **Status:** Deferred until after all implementation tasks

**Missing:** An administrator currently cannot remove/delete a user.

A later design must consider audit history, historical approval and financial actor references, referential integrity, retention requirements, and last-administrator protection. The final behavior may use hard deletion for unused accounts, archive/soft-delete for historically referenced accounts, or another documented lifecycle policy.

This is not implemented in TASK-02.
