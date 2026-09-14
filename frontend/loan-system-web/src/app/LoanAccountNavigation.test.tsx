import '../i18n';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { App } from './App';

const loan={loanId:'12345678-1234-1234-1234-123456789abc',sourceApplicationId:'87654321-1234-1234-1234-abcdef123456',borrowerId:'11111111-2222-3333-4444-555555555555',loanProductId:'p1',loanProductVersionId:'v1',approvedAmount:50000,currency:'OMR',financingType:'Build',reservedDisbursementAmount:0,totalDisbursed:0,availableToDisburse:50000,totalRepaid:0,outstandingBalance:0,status:'Active',openedAtUtc:'2026-01-01T00:00:00Z',eTag:'v'};
const borrower={borrowerId:loan.borrowerId,civilNumber:'CIV-42',employeeNumber:'EMP-7',fullName:'Readable Borrower',nationality:'OM',organization:'MOD',status:'Active',createdAt:'2026-01-01',updatedAt:'2026-01-01',eTag:'b'};
const application={loanApplicationId:loan.sourceApplicationId,borrowerId:loan.borrowerId,loanProductId:'p1',loanProductVersionId:'v1',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Approved',borrowerSnapshot:{civilNumber:'CIV-42',employeeNumber:'EMP-7',fullName:'Readable Borrower',nationality:'OM',organization:'MOD',rankGrade:'A',status:'Active'},productSnapshot:{loanProductId:'p1',loanProductVersionId:'v1',productName:'Housing Build',versionNumber:1,maximumAmount:50000,currency:'OMR',deductionPercentage:10,financingTypes:['Build'],eligibilityConfiguration:{requiredNationality:'OM',maximumApplicationCount:1,rankGradeAmountRules:[],maximumTermMonths:240,dueDateRule:'Monthly'},effectiveFrom:'2026-01-01',effectiveTo:null,productStatus:'Active',versionStatus:'Published'},eligibilityDecision:null,unitApproval:null,committeeApproval:null,rejectedAtUtc:null,inspectionPrerequisiteStatus:'Approved',mortgageStatus:'Completed',mortgageDecision:null,documentPrerequisiteStatus:'Satisfied',applicationDocuments:[],createdAtUtc:'2026-01-01T00:00:00Z',updatedAtUtc:'2026-01-01T00:00:00Z',submittedAtUtc:null,eTag:'a'};
const json=(body:unknown,status=200)=>Promise.resolve(new Response(JSON.stringify(body),{status,headers:{'Content-Type':'application/json'}}));

it('navigates from a loan account to its exact source application detail',async()=>{
  vi.stubGlobal('fetch',vi.fn().mockImplementation((input:string|URL|Request)=>{
    const url=typeof input==='string'?input:input instanceof URL?input.toString():input.url;
    if(url==='/api/v1/auth/me')return json({userId:'1',username:'admin',displayName:'Administrator',roles:['System Administrator'],permissions:['loans.read','borrowers.read','loanApplications.read']});
    if(url==='/api/v1/loans?')return json({items:[loan],pageNumber:1,pageSize:25,totalCount:1});
    if(url===`/api/v1/loans/${loan.loanId}`)return json(loan);
    if(url===`/api/v1/borrowers/${loan.borrowerId}`)return json(borrower);
    if(url===`/api/v1/loan-applications/${loan.sourceApplicationId}`)return json(application);
    if(url==='/api/v1/loan-applications?')return json({items:[],pageNumber:1,pageSize:25,totalCount:0});
    return json({},404);
  }));

  render(<QueryClientProvider client={new QueryClient({defaultOptions:{queries:{retry:false},mutations:{retry:false}}})}><App/></QueryClientProvider>);
  await userEvent.click(await screen.findByRole('button',{name:'Loan Accounts'}));
  await userEvent.click(await screen.findByTitle(loan.loanId));
  expect(await screen.findByText('Readable Borrower')).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button',{name:'Application Detail'}));
  expect(await screen.findByRole('heading',{name:'Application Detail'})).toBeInTheDocument();
  expect(screen.getByText(loan.sourceApplicationId,{exact:false})).toBeInTheDocument();
  expect(screen.getByText(/Housing Build/)).toBeInTheDocument();
});
