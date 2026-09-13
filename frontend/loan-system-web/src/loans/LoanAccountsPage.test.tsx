import '../i18n';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, expect, it, vi } from 'vitest';
import { applyLanguage } from '../i18n';
import { LoanAccountsPage } from './LoanAccountsPage';

const loan={loanId:'12345678-1234-1234-1234-123456789abc',sourceApplicationId:'87654321-1234-1234-1234-abcdef123456',borrowerId:'11111111-2222-3333-4444-555555555555',loanProductId:'p1',loanProductVersionId:'v1',approvedAmount:50000,currency:'OMR',financingType:'Build',reservedDisbursementAmount:0,totalDisbursed:0,availableToDisburse:50000,totalRepaid:0,outstandingBalance:0,status:'Active',openedAtUtc:'2026-01-01T00:00:00Z',eTag:'v'};
const borrower={borrowerId:loan.borrowerId,civilNumber:'CIV-42',employeeNumber:'EMP-7',fullName:'Readable Borrower',nationality:'OM',organization:'MOD',status:'Active',createdAt:'2026-01-01',updatedAt:'2026-01-01',eTag:'b'};
const application={loanApplicationId:loan.sourceApplicationId,borrowerId:loan.borrowerId,loanProductId:'p1',loanProductVersionId:'v1',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Approved',borrowerSnapshot:{civilNumber:'CIV-42',employeeNumber:'EMP-7',fullName:'Readable Borrower',nationality:'OM',organization:'MOD',status:'Active'},productSnapshot:{loanProductId:'p1',loanProductVersionId:'v1',productName:'Housing Build',versionNumber:1,maximumAmount:50000,currency:'OMR',deductionPercentage:10,financingTypes:['Build'],eligibilityConfiguration:{requiredNationality:'OM',maximumApplicationCount:1,rankGradeAmountRules:[],maximumTermMonths:240,dueDateRule:'Monthly'},effectiveFrom:'2026-01-01',effectiveTo:null,productStatus:'Active',versionStatus:'Published'},eligibilityDecision:null,unitApproval:null,committeeApproval:null,rejectedAtUtc:null,inspectionPrerequisiteStatus:'Approved',mortgageStatus:'Completed',mortgageDecision:null,documentPrerequisiteStatus:'Satisfied',applicationDocuments:[],createdAtUtc:'2026-01-01T00:00:00Z',updatedAtUtc:'2026-01-01T00:00:00Z',submittedAtUtc:null,eTag:'a'};
const fetchMock=vi.fn();const json=(x:unknown,status=200)=>Promise.resolve(new Response(JSON.stringify(x),{status,headers:{'Content-Type':'application/json'}}));
function show(permissions:string[]=[]){return render(<QueryClientProvider client={new QueryClient({defaultOptions:{queries:{retry:false}}})}><LoanAccountsPage permissions={permissions}/></QueryClientProvider>)}
beforeEach(()=>{applyLanguage('en');fetchMock.mockReset();vi.stubGlobal('fetch',fetchMock)});

it('renders readable borrower and application details while keeping technical ids secondary',async()=>{
  fetchMock.mockImplementation((url:string)=>{
    if(url==='/api/v1/loans?')return json({items:[loan],pageNumber:1,pageSize:25,totalCount:1});
    if(url===`/api/v1/loans/${loan.loanId}`)return json(loan);
    if(url===`/api/v1/borrowers/${loan.borrowerId}`)return json(borrower);
    if(url===`/api/v1/loan-applications/${loan.sourceApplicationId}`)return json(application);
    return json({items:[],pageNumber:1,pageSize:25,totalCount:0});
  });
  show(['borrowers.read','loanApplications.read']);
  expect(await screen.findByTitle(loan.loanId)).toHaveTextContent('12345678…89abc');
  await userEvent.click(screen.getByTitle(loan.loanId));
  expect(await screen.findByRole('heading',{name:'Loan Account Details'})).toBeInTheDocument();
  expect(await screen.findByText('Readable Borrower')).toBeInTheDocument();
  expect(screen.getByText('Civil Number: CIV-42')).toBeInTheDocument();
  expect(screen.getByText('Employee Number: EMP-7')).toBeInTheDocument();
  expect(await screen.findByText('Housing Build')).toBeInTheDocument();
  expect(screen.getByText(/Financing Type: Build/)).toBeInTheDocument();
  expect(screen.getByTitle(loan.sourceApplicationId)).toHaveTextContent('87654321…23456');
  expect(screen.getAllByText('0 OMR')).toHaveLength(4);
  expect(screen.getAllByText('50000 OMR')).toHaveLength(2);
});

it('does not request borrower or application details without their read permissions',async()=>{
  fetchMock.mockImplementation((url:string)=>url==='/api/v1/loans?'?json({items:[loan],pageNumber:1,pageSize:25,totalCount:1}):url===`/api/v1/loans/${loan.loanId}`?json(loan):json({},403));
  show(['loans.read']);
  await userEvent.click(await screen.findByTitle(loan.loanId));
  expect(await screen.findByRole('heading',{name:'Loan Account Details'})).toBeInTheDocument();
  expect(screen.queryByText('Readable Borrower')).not.toBeInTheDocument();
  expect(fetchMock).not.toHaveBeenCalledWith(`/api/v1/borrowers/${loan.borrowerId}`);
  expect(fetchMock).not.toHaveBeenCalledWith(`/api/v1/loan-applications/${loan.sourceApplicationId}`);
});

it('localizes Arabic and shows an API error',async()=>{applyLanguage('ar');fetchMock.mockResolvedValue(new Response(JSON.stringify({errorCode:'loans.failure'}),{status:500,headers:{'Content-Type':'application/json'}}));show();expect(await screen.findByRole('alert')).toHaveTextContent('فشلت عملية حساب القرض');expect(document.documentElement.dir).toBe('rtl')});
