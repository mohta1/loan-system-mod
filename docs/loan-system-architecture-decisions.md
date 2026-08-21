# Loan Management System — Architecture Decision Record Baseline

## ADR-001 — Use a Modular Monolith

**Status:** Accepted

### Decision

Build the MVP as one ASP.NET Core deployable organized into explicit business modules.

### Why

- lower operational complexity than microservices;
- simple deployment/debugging;
- strong transactional behavior within modules;
- preserves future extraction options through module boundaries.

### Consequence

Direct cross-module database access is prohibited.

---

## ADR-002 — Use Pragmatic DDD

**Status:** Accepted

### Decision

Use aggregates, entities, value objects, domain events, and policies where they protect meaningful business behavior.

Do not force rich domain patterns onto simple CRUD/reference data.

### Consequence

Core modules receive stronger domain modeling than generic supporting concerns.

---

## ADR-003 — Clean Architecture Inside One Module Assembly

**Status:** Accepted

### Decision

Each business module uses one implementation project containing Domain, Application, Infrastructure, and Presentation namespaces/folders.

Dependency rules are enforced with architecture tests rather than four projects per module.

### Why

Four projects × eleven modules would create unnecessary MVP ceremony.

### Consequence

Architecture tests are mandatory, not optional.

---

## ADR-004 — Central Cross-Module Contracts Assembly

**Status:** Accepted

### Decision

Use `LoanSystem.Contracts` for public module interfaces, contract DTOs, and integration-event schemas.

Module implementation projects do not reference one another.

### Consequence

The Contracts assembly must remain behavior-free and infrastructure-free.

---

## ADR-005 — One SQL Server Database, Schema per Module

**Status:** Accepted

### Decision

Use one SQL Server database for MVP deployment, with separate module-owned schemas and separate DbContexts.

### Why

- simple operations;
- preserves ownership boundaries;
- avoids distributed transactions;
- allows future extraction.

### Consequence

Cross-module joins/updates are not part of transactional module code.

---

## ADR-006 — Lightweight CQRS Without Mandatory MediatR

**Status:** Accepted

### Decision

Separate commands and queries and use explicit handlers.

A mediator library is not required.

### Why

CQRS semantics are useful; a mediator dependency is not necessary to obtain them.

### Consequence

Endpoints may inject use-case handlers directly.

---

## ADR-007 — Integration Events with Outbox/Inbox

**Status:** Accepted

### Decision

Cross-module state changes use persisted integration events.

Use Transactional Outbox on producers and idempotent Inbox/consumer handling.

Dispatch in process for MVP.

### Why

Business-critical events must survive process failure and duplicate delivery.

### Consequence

Event handlers must be idempotent.

---

## ADR-008 — No External Message Broker for MVP

**Status:** Accepted

### Decision

Do not introduce Kafka/RabbitMQ solely for internal modular-monolith communication.

### Consequence

A background in-process dispatcher handles persisted Outbox events. Event contracts remain transport-neutral for future migration.

---

## ADR-009 — Minimal API Route Groups

**Status:** Accepted

### Decision

Use ASP.NET Core Minimal API route groups for module endpoints.

### Why

- thin HTTP layer;
- concise module grouping;
- less controller ceremony.

### Consequence

Business logic remains in handlers/domain; endpoints only translate HTTP to/from application contracts.

---

## ADR-010 — React SPA with Vite

**Status:** Accepted

### Decision

Build the internal frontend as a React + TypeScript SPA using Vite.

Use business-feature folders and a typed API boundary.

### Consequence

SSR/React Server Components are not an MVP requirement. The ASP.NET API remains the backend boundary.

---

## ADR-011 — TanStack Query for Server State; No Redux by Default

**Status:** Accepted

### Decision

Use a server-state library for API data and keep UI state local where possible.

Do not introduce Redux/global client state without a concrete need.

### Consequence

Backend data remains authoritative and cache invalidation is explicit around mutations.

---

## ADR-012 — Permission-Based Authorization

**Status:** Accepted

### Decision

Authorize backend use cases with permissions such as:

```text
loanApplications.committeeApprove
treasury.audit
treasury.execute
```

Roles group permissions but are not hard-coded into domain behavior.

### Consequence

Organizational role mapping can change without rewriting domain rules.

---

## ADR-013 — External Systems Behind Owning-Module Adapters

**Status:** Accepted

### Decision

Use replaceable ports/adapters for:

- Active Directory;
- HR integration;
- B2B payment;
- file server.

### Consequence

MVP fake/local adapters can later be replaced without redesigning core domain modules.

---

## ADR-014 — Optimistic Concurrency for Contested Financial State

**Status:** Accepted

### Decision

Use optimistic concurrency for financial/workflow aggregates where parallel changes can violate invariants.

Priority:

- LoanAccount;
- Disbursement;
- TreasuryPayment;
- important LoanApplication transitions.

### Consequence

Concurrency conflicts are surfaced explicitly rather than silently overwriting state.

---

## ADR-015 — Idempotency for Financial Commands

**Status:** Accepted

### Decision

Use business-level idempotency for:

- disbursement creation;
- payment execution/retry;
- repayment posting;
- salary-deduction imports;
- integration-event consumers.

### Consequence

Retries cannot create duplicate financial effects.
