# Loan Management System — MVP Scope

## 1. MVP Objective

The MVP shall provide an internal Loan Lifecycle Management System that demonstrates the complete core housing-loan flow from borrower registration to repayment and outstanding balance tracking.

The MVP is not intended to implement every capability of the final production system.

Its purpose is to validate:

- the domain model;
- business workflow;
- approval boundaries;
- financial state transitions;
- modular architecture;
- data integrity;
- auditability;
- end-to-end usability.

---

## 2. MVP Product Definition

The MVP covers the following primary lifecycle:

```text
Borrower
  ↓
Loan Application
  ↓
Eligibility Check
  ↓
Administrative Approval
  ↓
Property Inspection
  ↓
Document / Mortgage Completion
  ↓
Disbursement Approval
  ↓
Treasury Processing
  ↓
Payment
  ↓
Repayment
  ↓
Outstanding Balance
```

---

## 3. Target Users

The MVP is for internal organizational users only.

### Included User Roles

- System Administrator
- Loan Reception / Loan Management Officer
- Unit Officer
- Loan Committee Member
- Property Inspector / Technical Affairs Officer
- Accounting Officer
- Higher Administrative Approver
- Treasury Input User
- Treasury Auditor
- Treasury Approver

### Excluded User

- Borrower / Customer self-service user

A borrower-facing portal is explicitly out of scope for the MVP.

---

## 4. In Scope

### 4.1 Authentication and Authorization

Included:

- Login
- Users
- Roles
- Permissions
- User activation/deactivation
- Server-side authorization
- Role-based access control

MVP implementation may use a local/internal authentication provider.

Active Directory shall remain replaceable through an abstraction.

---

### 4.2 Borrower Management

Included:

- Create borrower
- Edit borrower
- View borrower
- Search borrower
- Activate/deactivate borrower

Minimum borrower information:

- Civil Number
- Employee Number
- Full Name
- Phone
- Organization
- Rank / Grade
- Employment Information
- Status

---

### 4.3 Borrower Excel Import

Included:

```text
Upload Excel
  ↓
Validate
  ↓
Preview
  ↓
Import
  ↓
Result / Error Report
```

A real HR integration is not included.

---

### 4.4 Loan Product Configuration

Included:

- Create/configure loan products
- Activate/deactivate loan products
- Maximum loan amount
- Deduction percentage
- Financing type
- Eligibility-related configuration
- Effective dates

Loan rules shall be configurable where practical and shall not be hard-coded in controllers or frontend code.

---

### 4.5 Loan Application

Included:

- Create application
- Save draft
- Select borrower
- Select loan product
- Select financing type
- Enter application data
- Evaluate eligibility
- Determine proposed amount
- Attach required documents
- Submit application
- View application status
- Reject application
- Cancel eligible application

---

### 4.6 Administrative Approval Workflow

Included:

```text
Submitted
   ↓
Unit Officer Approval
   ↓
Loan Committee Approval
   ↓
Technical / Inspection Stage
```

Each action shall capture:

- actor;
- timestamp;
- decision;
- status transition;
- comment/reason.

---

### 4.7 Property Inspection

Included:

- Create inspection
- Assign/record inspector
- Record property data
- Record inspection date
- Record inspection result
- Add notes
- Approve inspection
- Reject inspection

Geographic optimization of inspections is excluded.

---

### 4.8 Documents

Included:

- Upload/attach document
- Store document metadata
- Associate documents with business records
- Retrieve/view document reference
- Remove or replace document where permitted

The MVP may use local/dev file storage.

---

### 4.9 Mortgage Tracking

Included:

Basic mortgage status tracking, such as:

- Not Required
- Pending
- Completed
- Released

The exact workflow may be refined during domain design.

---

### 4.10 Disbursement

Included:

- Create a disbursement request
- Support multiple disbursements where allowed
- Track disbursement status
- Administrative approvals
- Treasury processing
- Final payment state

Administrative approval sequence:

```text
Technical Affairs
  ↓
Accounting
  ↓
Higher Officer
```

Financial processing sequence:

```text
Treasury Input
  ↓
Treasury Auditor
  ↓
Treasury Approver
```

Critical invariant:

```text
Total Disbursed Amount <= Approved Loan Amount
```

---

### 4.11 Payment Simulation

Included:

- Payment request
- Pending status
- Processing status
- Paid status
- Failed status

The MVP shall use a fake/local payment provider.

A real B2B bank integration is excluded.

---

### 4.12 Repayment

Included:

#### Salary Deduction Import
- Upload Excel
- Validate
- Match borrower/employee
- Record deductions
- Report invalid/unmatched rows

#### Manual Repayment
- Amount
- Date
- Receipt
- Reference
- Notes

---

### 4.13 Outstanding Balance

Included:

The system shall calculate and expose the current outstanding balance.

Initial MVP rule:

```text
Outstanding Balance =
Total Disbursed Amount
-
Total Valid Repayments
```

More advanced accounting adjustments are deferred.

---

### 4.14 Cancellation

Included:

An application/loan may be cancelled when allowed.

Initial MVP invariant:

```text
Cancellation is allowed only if
Total Disbursed Amount == 0
```

---

### 4.15 Audit Trail

Included for critical operations:

- Approve
- Reject
- Cancel
- Disburse
- Repay
- Critical state changes
- Critical configuration changes

Audit data shall include:

- Actor
- Action
- Entity
- Entity ID
- Timestamp
- Previous State
- New State
- Reason/Comment

---

### 4.16 Operational Reporting

Included:

- Loan Applications
- Approved Applications
- Rejected Applications
- Active Loans
- Disbursements
- Repayments
- Outstanding Balances

Reports may initially be implemented as application screens/API queries.

---

## 5. Out of Scope

The following capabilities are intentionally excluded from the initial MVP:

### User Experience
- Borrower Portal
- Borrower self-service
- Public-facing application submission

### Integrations
- Real Active Directory Integration
- Real HR System Integration
- Real B2B Bank Payment Integration
- Power BI Integration
- Production File Server Integration if unavailable

### Advanced Operations
- Geographic Inspector Scheduling
- Inspection Route Optimization
- Insurance Claims
- Annual Loan Insurance
- Account Reconciliation
- Monthly Financial Closing
- Loan Fund Balance Management
- Loan Exemption
- Loan Transfer
- Postponement of Deductions
- Special Debt Handling
- Payment Corrections

### Migration
- Full Legacy Data Migration
- Historical migration tooling beyond what is necessary for MVP testing/demo

### Architecture
- Microservices
- Distributed transactions
- Event streaming platform
- Service mesh
- Separate deployment per module

---

## 6. External Integrations Strategy

External integrations shall not block MVP implementation.

Use abstractions/adapters such as:

```text
IIdentityProvider
  ├── LocalIdentityProvider      ← MVP
  └── ActiveDirectoryProvider    ← Later

IBorrowerSource
  ├── Manual
  ├── Excel
  └── HrApi                      ← Later

IPaymentGateway
  ├── FakePaymentGateway         ← MVP
  └── B2BPaymentGateway          ← Later

IFileStorage
  ├── LocalFileStorage           ← MVP
  └── FileServerStorage          ← Later
```

---

## 7. Technical Scope

### Backend
- ASP.NET Core
- .NET 10 LTS
- Entity Framework Core
- Microsoft SQL Server
- REST API

### Frontend
- React
- TypeScript

### Architecture
- Modular Monolith
- Pragmatic DDD
- Clean Architecture principles
- Clean Code

### Testing
- Unit Tests
- Integration Tests
- Architecture Tests
- Critical end-to-end flow coverage

---

## 8. MVP Quality Constraints

The MVP must not be treated as disposable prototype code.

It shall:

- enforce business invariants in backend/domain logic;
- have clear module boundaries;
- avoid direct cross-module persistence coupling where possible;
- be testable;
- be auditable;
- use migrations for database changes;
- use explicit contracts for external integrations;
- keep frontend validation separate from authoritative backend validation;
- avoid unnecessary framework or architectural complexity.

---

## 9. Primary MVP Happy Path

The following scenario must work end-to-end:

```text
1. Internal user logs in

2. User creates or imports a borrower

3. User creates a loan application

4. Eligibility is evaluated

5. Application is submitted

6. Unit Officer approves

7. Loan Committee approves

8. Property Inspector records inspection

9. Inspection is approved

10. Required documents are attached

11. Mortgage is marked completed where required

12. First disbursement is created

13. Technical Affairs approves

14. Accounting approves

15. Higher Officer approves

16. Treasury Input processes the request

17. Treasury Auditor approves

18. Treasury Approver approves

19. Payment is marked successful

20. Repayment is recorded/imported

21. Outstanding balance is recalculated

22. Audit trail shows the complete history
```

---

## 10. Required Negative Scenarios

The MVP must also demonstrate that invalid operations are prevented.

At minimum:

- unauthorized user cannot approve;
- rejected application cannot continue through the happy path;
- disbursement cannot exceed approved loan amount;
- disbursement cannot occur before required approvals;
- repayment amount must be valid;
- duplicate or invalid Excel rows are reported;
- cancellation is rejected after any disbursement;
- invalid status transitions are rejected;
- missing required business data prevents relevant workflow progression.

---

## 11. MVP Definition of Done

The MVP is considered complete when all of the following are true.

### Functional
- Primary happy path works end-to-end.
- Critical negative scenarios are enforced.
- Internal users can perform required workflows through the React UI.
- Loan state and balance remain consistent.

### Architecture
- Modular Monolith boundaries are defined and enforced.
- Core business logic is not implemented in controllers or React components.
- External integrations use replaceable adapters.
- No known circular module dependencies exist.

### Data
- EF Core migrations can create the database from scratch.
- Critical operations are transactional.
- Data integrity constraints are enforced.

### Security
- Authentication works.
- Authorization works server-side.
- Privileged actions are protected by permissions/roles.

### Audit
- Approval and financial transitions are traceable.
- Critical actions include actor and timestamp.

### Testing
- Domain rules have unit coverage.
- Persistence/API paths have integration coverage.
- Architecture rules have automated tests.
- Primary critical flow has automated or repeatable verification.

### Delivery
- Backend can run locally.
- Frontend can run locally.
- Database can be initialized consistently.
- Setup instructions are documented.

---

## 12. MVP Success Criteria

The MVP succeeds if stakeholders can observe a realistic internal loan lifecycle without requiring unavailable production integrations.

The system should demonstrate that:

1. the business workflow is correctly modeled;
2. critical rules are protected;
3. financial state is consistent;
4. role separation is enforced;
5. the architecture can evolve toward the full system without major redesign.

---

## 13. Post-MVP Direction

After MVP validation, the next increments may add:

- Active Directory
- HR Integration
- Real B2B Payment Integration
- Production File Server
- Power BI
- Insurance
- Reconciliation
- Closing
- Loan Transfer
- Exemptions
- Postponements
- Special Debt Handling
- Full Migration
- Borrower Portal

These additions shall be prioritized based on business value and availability of external contracts.

---

## 14. Scope Baseline

This document is the baseline MVP scope.

Any new requirement should be classified as one of:

- MVP-critical;
- post-MVP;
- architecture-only consideration;
- explicitly rejected.

Changes to this scope should be deliberate to prevent uncontrolled MVP expansion.
