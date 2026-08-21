# Loan Management System — Requirements Baseline

## 1. Purpose

This document defines the baseline requirements for the Loan Management System MVP and provides a stable reference for subsequent domain modeling, architecture, implementation, and testing.

The system will manage the internal lifecycle of housing loans from borrower registration through application, approval, property inspection, disbursement, repayment, and balance tracking.

The first implementation will focus on internal organizational users. A borrower-facing portal is intentionally excluded from the initial MVP.

---

## 2. Technical Baseline

### Backend
- ASP.NET Core
- .NET 10 LTS
- REST API
- Entity Framework Core
- Microsoft SQL Server

### Frontend
- React
- TypeScript

### Architecture
- Modular Monolith
- Pragmatic Domain-Driven Design (DDD)
- Clean Architecture principles
- Clean Code
- Explicit module boundaries
- Dependency inversion for external integrations

### Deployment
- Single backend deployable
- Single frontend deployable
- No microservices for the MVP

---

## 3. Architectural Principles

### 3.1 Modular Monolith

The system shall be implemented as a modular monolith.

Each business module shall:
- own its business rules;
- expose explicit contracts;
- avoid direct coupling to internal implementation details of other modules;
- be independently testable;
- be designed so that future extraction to a separate service is possible if justified.

### 3.2 Pragmatic DDD

DDD shall be applied where business complexity justifies it.

Use:
- Aggregates
- Entities
- Value Objects
- Domain Services
- Domain Events
- Domain Invariants

where they clarify and protect business behavior.

Simple lookup tables and straightforward CRUD concerns shall not be over-engineered.

### 3.3 Clean Architecture

Business logic shall not depend on:
- ASP.NET Core;
- EF Core;
- SQL Server;
- React;
- external APIs;
- file storage;
- authentication providers.

External dependencies shall be accessed through abstractions where doing so materially reduces coupling.

### 3.4 Clean Code

The implementation shall prioritize:
- meaningful names;
- small focused methods;
- explicit business intent;
- low coupling;
- high cohesion;
- predictable error handling;
- maintainability;
- testability.

---

## 4. User and Access Requirements

The system shall support internal organizational users.

Initial roles may include:

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

### Required capabilities

The system shall support:
- user authentication;
- role-based authorization;
- permission checks;
- user activation and deactivation;
- auditability of privileged actions.

### Authentication Integration

Active Directory integration is not required for the MVP.

Authentication shall be implemented behind an abstraction so that a future Active Directory provider can be added without changing business logic.

---

## 5. Borrower Management Requirements

The system shall allow authorized users to:

- create a borrower;
- update borrower information;
- view borrower details;
- search borrowers;
- activate or deactivate borrower records.

Borrower data shall support, at minimum:

- Civil Number
- Employee Number
- Full Name
- Phone Number
- Organization
- Rank / Grade
- Employment Information
- Status

### Borrower Import

The system shall support borrower import from Excel.

The import workflow shall include:

1. Upload
2. Validation
3. Preview
4. Import
5. Import result / error reporting

### Future HR Integration

A real HR integration is not required for the MVP.

Borrower sourcing shall be designed so that manual input, Excel import, and future HR API integration can coexist.

---

## 6. Loan Product and Loan Rule Requirements

The system shall support more than one loan product or loan type.

A loan product shall be configurable and may include:

- Name
- Status
- Maximum Loan Amount
- Deduction Percentage
- Eligibility Rules
- Financing Types
- Effective From
- Effective To

Loan rules shall not be hard-coded into controllers or UI logic.

Rules that are expected to change operationally should be represented as configuration or domain data where practical.

The system shall support activation and deactivation of loan products.

---

## 7. Loan Application Requirements

Authorized users shall be able to:

- create a loan application;
- select a borrower;
- select a loan product;
- select a financing type;
- enter required application information;
- evaluate eligibility;
- calculate or determine the proposed loan amount;
- attach required documents;
- save as draft;
- submit the application.

A loan application shall have an explicit lifecycle/status.

Initial status concepts may include:

- Draft
- Submitted
- Unit Approved
- Committee Approved
- Inspection Pending
- Inspection Approved
- Documents Pending
- Ready for Disbursement
- Rejected
- Cancelled

The final domain state model will be defined during domain design.

---

## 8. Eligibility Requirements

The system shall evaluate borrower eligibility according to the selected loan product.

Eligibility rules may consider:

- borrower type;
- rank or grade;
- employment information;
- nationality if applicable;
- previous applications or loans;
- configured loan constraints;
- loan product limits.

Eligibility logic shall be implemented in the domain/application layer and shall not depend on frontend validation.

---

## 9. Approval Workflow Requirements

The MVP shall support a defined approval workflow.

The administrative application flow shall include, at minimum:

1. Application Submission
2. Unit Approval
3. Loan Committee Approval
4. Property Inspection / Technical Review

Approval actions shall capture:

- Actor
- Date and Time
- Previous State
- New State
- Decision
- Comment or Reason

The system shall support both approval and rejection where applicable.

---

## 10. Property Inspection Requirements

The system shall allow authorized users to create and record property inspection information.

Property inspection data may include:

- Governorate
- State
- Area
- Number of Floors
- Number of Rooms
- Property Area
- Property Condition
- Inspection Date
- Inspector
- Inspection Result
- Notes

The inspection shall support an approve/reject outcome.

Geographic optimization and grouping of inspection visits are not required for the MVP.

---

## 11. Document Management Requirements

The system shall support attachments associated with borrowers, applications, inspections, mortgages, disbursements, and payments where relevant.

Examples include:

- ownership documents;
- survey documents;
- engineering drawings;
- application documents;
- bank details;
- receipts.

File storage shall be accessed through an abstraction.

The MVP may use local or development storage while allowing later integration with the target file server.

---

## 12. Mortgage Requirements

The system shall track mortgage-related state where applicable.

Initial mortgage status concepts may include:

- Not Required
- Pending
- Completed
- Released

Mortgage completion may be required before disbursement, depending on the selected loan product or business rule.

---

## 13. Disbursement Requirements

The system shall support one or more disbursements against an approved loan.

The system shall enforce the invariant:

> Total Disbursed Amount must never exceed the Approved Loan Amount.

The administrative disbursement approval flow shall include, at minimum:

1. Technical Affairs Approval
2. Accounting Approval
3. Higher Officer Approval

The financial processing flow shall include:

1. Treasury Input
2. Treasury Audit
3. Treasury Approval

Each transition shall be auditable.

---

## 14. Payment Requirements

The system shall track payment execution status.

Initial payment statuses may include:

- Pending
- Processing
- Paid
- Failed

A real B2B banking integration is not required for the MVP.

Payment processing shall be accessed through an abstraction so a fake/local implementation can be used initially and replaced later with a real B2B payment integration.

---

## 15. Repayment Requirements

The MVP shall support at least two repayment sources.

### Salary Deduction

The system shall support importing salary deduction information from Excel.

The import shall:
- validate rows;
- match borrower/employee identifiers;
- record successful deductions;
- report invalid or unmatched rows.

### Manual Repayment

Authorized users shall be able to record a manual repayment including:

- Amount
- Date
- Receipt
- Reference
- Notes

The system shall update the loan balance when valid repayments are recorded.

---

## 16. Loan Balance Requirements

The system shall maintain an accurate outstanding loan balance.

At minimum, the balance model shall account for:

- approved amount;
- disbursed amount;
- repayments;
- adjustments introduced by future scope.

The exact financial model will be finalized during domain modeling.

For the MVP, the core relationship is:

Outstanding Balance = Total Disbursed Amount - Valid Repayments

---

## 17. Cancellation Requirements

The system shall support cancellation of an application or loan where permitted.

For the MVP:

> Cancellation shall be allowed only when no amount has been disbursed.

The final cancellation rules will be modeled as domain invariants.

---

## 18. Audit Requirements

Important business operations shall be auditable.

The audit trail shall capture, where relevant:

- Actor
- Action
- Entity Type
- Entity Identifier
- Timestamp
- Previous State
- New State
- Reason or Comment

Important audited operations include:

- Approve
- Reject
- Return
- Cancel
- Disburse
- Repay
- Change Critical Configuration

---

## 19. Reporting Requirements

The MVP shall provide operational reporting for:

- Loan Applications
- Approved Applications
- Rejected Applications
- Active Loans
- Disbursements
- Repayments
- Outstanding Balances

Power BI integration is not required for the MVP.

The data model should not prevent later reporting integration.

---

## 20. External Integration Requirements

The target system may eventually integrate with:

- Active Directory
- HR System
- B2B Payment System
- File Server
- Power BI
- Other organizational systems

These integrations shall not be embedded directly into core business logic.

The MVP may use adapters, fake implementations, local implementations, or manual workflows where external contracts are unavailable.

---

## 21. Non-Functional Requirements

### Maintainability
The system shall use explicit module boundaries and maintainable code structure.

### Testability
Business logic shall be independently testable.

### Security
Authorization shall be enforced server-side.

### Data Integrity
Critical business invariants shall be enforced in the backend/domain layer.

### Auditability
Important business decisions and financial actions shall be traceable.

### Extensibility
Future integrations and additional loan processes shall not require redesigning the core system.

### Reliability
Financial state changes shall use transactional consistency where required.

### Validation
Frontend validation shall improve usability but shall never replace backend validation.

---

## 22. Testing Requirements

The solution shall include:

### Unit Tests
For domain rules and deterministic business logic.

### Integration Tests
For:
- persistence;
- API behavior;
- module integration;
- transaction behavior.

### Architecture Tests
For enforcing:
- module boundaries;
- dependency rules;
- prohibited cross-module dependencies.

### End-to-End / Critical Flow Tests
At least the primary loan lifecycle shall be covered at an appropriate level.

---

## 23. Initial Functional Flow

The target MVP business flow is:

```text
Borrower
  ↓
Loan Application
  ↓
Eligibility Check
  ↓
Unit Approval
  ↓
Committee Approval
  ↓
Property Inspection
  ↓
Documents / Mortgage
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

## 24. Deferred Requirements

The following requirements belong to the broader system but are deferred from the initial MVP:

- Borrower Portal
- Real Active Directory Integration
- Real HR Integration
- Real B2B Payment Integration
- Power BI Integration
- Geographic Inspector Scheduling
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
- Full Legacy Data Migration

These requirements shall remain visible in architecture decisions so the MVP does not create unnecessary future rework.

---

## 25. Baseline Decision

This document establishes the requirements baseline for the first implementation.

Subsequent domain decomposition, bounded-context design, module structure, database design, API contracts, and implementation tasks shall be derived from this baseline.
