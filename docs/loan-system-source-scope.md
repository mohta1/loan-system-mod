# Housing Loan System — Source Scope Baseline

> **Source:** `Scope.pdf` supplied for the Ministry of Defence Housing Loan System project.  
> **Purpose:** Clean Markdown normalization of the source scope. This document preserves the source requirements and does **not** apply MVP reductions or architectural interpretation. MVP decisions belong in separate project documents.

---

## 1. Introduction

### 1.1 Loan System

The Housing Loan System is an electronic system for managing loan operations provided by the Ministry of Defence (MOD). Its functional coverage includes loan registration, payment tracking, repayment/collection, insurance, and administrative/financial reporting and statistics.

The MOD provides housing loans to employees as a financing mechanism intended to support housing and reduce financial burdens.

### 1.2 Project Idea

The project is to redevelop or acquire a new Housing Loan System using modern technologies while preserving the core administrative and financial functions of the existing MOD loan system and adding new user-experience requirements.

The full project scope includes migration of existing data and attachments.

### 1.3 System Scope of Work

The complete system covers:

1. User Management
2. Borrower Data Management
3. Loan Registration Process Management
4. Loan Follow-up, Borrower Role/Sequence, and Approval
5. Payment Management
6. Collection Management / Loan Repayment
7. Loan Insurance
8. Loan Account Balance Management
9. Integration with Other Systems
10. Administrative and Financial Reports

---

# 2. Functional Requirements

The system contains both administrative and financial procedures.

- The administrative side is managed by the MOD Loans Department / Military Welfare Services according to functional responsibilities and authorities.
- The financial side is managed by the Treasury and Accounts Department.

The system must enforce specialization, authority, and role separation according to the relevant business procedures.

---

# 3. Administrative Requirements

## 3.1 User Management

The system must support:

- registering users;
- suspending/deactivating users;
- modifying users;
- granting the permissions required to use the Housing Loan System.

---

## 3.2 Borrower Data Management

Borrower master data is the basis for later loan procedures.

### 3.2.1 Borrower Data Sources

For MOD employees:

- borrower data comes through direct integration with the Human Resources Management System.

For employees of other organizations, including entities such as the Royal Guard of Oman (RGO) and Pension Fund:

- data is provided using Excel files;
- these files are uploaded to the loan system because the organizations are on networks separate from the MOD network.

### 3.2.2 Borrower Basic Data

The system must maintain necessary borrower information including examples such as:

- Name
- Civil Number
- Employee ID / Employee Number
- Phone Number
- Entity / Organization
- Grade / Rank
- Other required employment information

### 3.2.3 Borrower Maintenance

The system must support maintenance of borrower information, including:

- modification after initial registration;
- handling duplicate or invalid borrower records;
- deletion where applicable and allowed by business conditions;
- updates related to employment changes such as promotion, resignation, death, and other changes.

### 3.2.4 Borrower Details

Authorized users must be able to view comprehensive borrower details.

---

## 3.3 Loan Management

### 3.3.1 Loan Types

The system must support multiple loan types, such as:

- Officer loans;
- Non-Officer loans;
- additional loan types introduced later.

Loan types must be configurable and may be activated or disabled independently.

Different loan types may have different conditions.

### 3.3.2 Housing Financing Types

The source scope identifies at least two housing-financing options:

1. Purchase an existing house
2. Build a new house

The system must allow additional financing types to be added.

The financing type must be changeable before the loan amount is disbursed, subject to the applicable business process.

### 3.3.3 Loan Conditions

Each loan type may have specific conditions such as:

- eligibility requirements;
- loan due date / term conditions;
- monthly deduction percentage;
- other loan-specific rules.

Examples explicitly stated in the source include:

- the applicant may apply for the loan only once;
- the applicant must be of Omani nationality.

Rules are business configuration and must not be assumed to be permanently hard-coded values.

---

## 3.4 Loan Application Registration

The management department of each MOD entity submits loan applications for its members.

The process includes:

1. Selecting/registering the borrower.
2. Checking borrower conditions and eligibility.
3. Calculating the loan amount based on employee rank or grade.
4. Attaching initial documents, including the loan application form and other required documents.
5. Unit Official approval of the registration request.
6. Placing applications in a queue for the next stage.

For borrowers from other organizations:

- borrower data is uploaded through Excel / indirect electronic linkage because no direct network connection exists.

The source scope also requires borrower/application information to be exchanged with payroll processes so deductions can be handled. For external organizations, approved application information may be exchanged through Excel/indirect linkage.

---

## 3.5 Loan Application Approval

A committee approves loan-payment requests according to the borrower role/sequence/waiting list.

The committee may also approve nominated exception cases according to the applicable MOD process.

---

## 3.6 Engineer / Property Inspector

After committee approval, Technical Affairs reviews the application.

The inspection process includes:

- property location information such as governorate, state, and area;
- setting a field-inspection date;
- inspector site visit;
- grouping/scheduling visits based on geographical location where appropriate;
- capturing detailed property data after the visit, including examples such as:
  - number of floors;
  - number of rooms;
  - area;
  - property condition;
- an approval workflow for the inspection result.

---

## 3.7 Adding Documents to Loan Applications

After inspection approval, the Loan Reception Department completes required loan documentation.

Examples include:

- ownership documentation;
- survey documentation;
- engineering drawings;
- other required property/loan documents.

The bank details of the contractor or property owner must also be maintained for the payment process.

---

## 3.8 Property Mortgage Contracts

Before loan disbursement, the required property/land mortgage process must be completed.

The source process includes:

- preparation of mortgage contracts;
- issuing a letter to the Ministry of Housing for mortgaging the property;
- issuing another letter to release the mortgage after the applicable loan-completion condition is reached.

---

## 3.9 Administrative Approval for Disbursing Financial Installments

Administrative approval of a financial installment has three ordered stages:

1. Technical Affairs Department
2. Accounting Department
3. Higher Officer

After completion, the transaction is sent to Treasury and Accounts for the financial-payment process.

The source explicitly requires the ability to return the transaction to the previous stage during this workflow.

---

## 3.10 Insurance Claims

Housing loans are insured. Where a mortgaged property is damaged, an insurance compensation claim may be submitted to the Loans Department.

---

# 4. Financial Requirements

The Treasury and Accounts Department is responsible for the financial procedures.

## 4.1 Payment Management

Requests that complete the administrative workflow become available for financial processing.

Payment is made to the contractor or property owner through a bank account using an electronic B2B payment system.

The financial payment workflow contains three ordered stages:

1. Input
2. Auditor
3. Approver

Requirements include:

- payment amount must not exceed the total loan amount;
- transactions must be returnable to the administrative side in case of errors according to the applicable process;
- errors before or after disbursement must be handled by the relevant correction/return process.

---

## 4.2 Collection Management / Loan Repayment

### MOD Employees

Monthly salary deductions are received through integration with the Human Resources system, and loan balances are updated accordingly.

### Other Organizations / Pension Fund

Monthly deductions may be provided through Excel files.

The system must support matching deductions using the **Civil Number** because some organizations use the Civil Number instead of the Employee Number.

### Direct / Manual Repayment

A borrower may repay all or part of the loan through other channels, such as depositing money directly at the bank and providing the receipt so that the loan can be updated in the system.

---

## 4.3 Loan Account Management

Treasury and Accounts manages loan accounts through reconciliation and monthly closing.

### 4.3.1 Account Reconciliation

On a monthly basis, actual bank balances must be reconciled with the amounts recorded by the system.

### 4.3.2 Monthly Account Closing

The monthly closing process includes:

- opening balance;
- receipts;
- payments;
- closing balance;
- comparison/matching with bank statements.

This process also requires:

1. Input
2. Auditor
3. Approver

---

## 4.4 Loan Fund Balance Management

The loan fund balance may increase or decrease when financial reinforcement is added or adjusted.

Changes to the fund balance must pass the financial approval stages:

1. Input
2. Auditor
3. Approver

---

## 4.5 Loan Insurance Management

All loans are subject to life insurance to help ensure repayment in the event of borrower death before the loan is repaid.

The outstanding loan balance is insured annually.

---

# 5. Exception Processes

The full system must account for special cases outside the standard request path.

## 5.1 Loan Exemption

Some borrowers may be exempted from all or part of the remaining amount, for example due to a royal pardon or other authorized decision.

The system must be able to represent the applicable category or percentage of exemption.

## 5.2 Loan Transfer

A loan may be transferred to another employee registered in the system according to the applicable rules.

## 5.3 Loan Closure

A loan may be closed for specific reasons, including death.

## 5.4 Postponement of Deductions

Loan Management may postpone deductions between specified dates where authorized.

## 5.5 Purchase of a House Already Mortgaged to MOD

The process may require repaying the existing owner's loan balance and paying the remaining amount to that owner.

## 5.6 Debts for Resigned Borrowers

Borrowers who have resigned and still have outstanding amounts may require a special monthly collection process.

## 5.7 Loan Cancellation

As long as no amount has been paid/disbursed, the loan may be cancelled at the applicable stage.

The source also refers to the Financial Directorate returning the amount after cancellation according to the applicable process.

## 5.8 Correction of Paid or Received Amounts

The system must support correction/recovery where excess amounts have been paid or received.

---

# 6. Integration with Other Systems

## 6.1 HR Integration

For MOD borrowers, the system directly integrates with the Human Resources system.

Integration includes:

- basic borrower data;
- subsequent corrections/modifications;
- promotion/employment updates;
- monthly deduction information.

## 6.2 Other Organizations

For external organizations, integration is indirect, including Excel-based exchange where required.

## 6.3 Monthly Deductions

The system exchanges borrowers/outstanding-loan information with HR or other organizations so monthly deductions can be calculated and returned to the loan system.

The source notes current deduction paths such as 30% and 40% of basic salary. These are business values and should be treated as configurable rules rather than permanent technical constants.

---

# 7. Technical Requirements

## 7.1 Existing System

The existing system has operated for more than ten years and was developed internally within the Oracle EBS environment.

Current technology stated in the source:

| Area | Existing Technology |
|---|---|
| Database | Oracle |
| Application | OAF |
| Reports | Oracle Reports |

The redevelopment initiative is driven by maintainability, technical support, and the need for improvements using modern technology.

## 7.2 New System Technical Specifications

| Area | Source Requirement |
|---|---|
| Programming / Framework | ASP.Net |
| Database | MSSQL |
| Reports | Prefer MS Power BI integrated with the system |
| HR Integration | API integration using .NET technology |
| User Interface Languages | Arabic and English |
| Attachment Management | Files and attachments on a File Server |
| System Login | Active Directory integration |

---

# 8. Administrative and Financial Reporting

The system must provide the administrative and financial reports required to follow the loan-management procedures.

The full scope also requests smart/self-service reporting so end users can create reports suited to business needs without depending on technical support.

---

# 9. Data and Attachment Migration

The full project includes migration of data and files from the existing system to the new system so current and historical records are preserved when the existing system is retired.

Migration requires analysis of the current system/data and compliance with MOD information-security policy.

---

# 10. Training

Training is required for:

1. Developers and Technicians
2. System Administrators
3. End Users

The purpose is to enable effective system use and support future maintenance.

---

# 11. Technical Support

Technical support must ensure continuity of effective system operation and assist users with technical issues.

The source requires support for a period of **not less than two years after actual system operation**.

---

# 12. Required Documentation

The full solution must include documentation covering at least:

1. System Architecture
2. User Interface Design
3. Database Design
4. Integration with Other Systems

---

# 13. Source-Scope Conclusion

The source describes a secure and transparent Housing Loan System covering the complete administrative and financial loan lifecycle, including borrower management, origination, approvals, property inspection, mortgages, disbursement, payment, repayment, balances, insurance, integrations, reporting, migration, training, support, and documentation.

This file is a **source baseline**. Use `loan-system-mvp-scope.md` and `loan-system-scope-alignment.md` to determine which source requirements are implemented in the current MVP and which are intentionally deferred.
