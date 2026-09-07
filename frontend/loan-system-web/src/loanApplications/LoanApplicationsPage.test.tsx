import'../i18n';
import{render,screen,waitFor}from'@testing-library/react';
import userEvent from'@testing-library/user-event';
import{QueryClient,QueryClientProvider}from'@tanstack/react-query';
import{beforeEach,expect,it,vi}from'vitest';
import{applyLanguage}from'../i18n';
import{LoanApplicationsPage}from'./LoanApplicationsPage';

const fetchMock=vi.fn();
const json=(body:unknown,status=200)=>Promise.resolve(new Response(JSON.stringify(body),{status,headers:{'Content-Type':'application/json'}}));
const product={loanProductId:'p1',versionId:'v1',productName:'Housing',versionNumber:2,maximumAmount:100000,currency:'OMR',deductionPercentage:10,financingTypes:['Build','Purchase'],effectiveFrom:'2026-01-01',effectiveTo:null};
const borrower={borrowerId:'b1',civilNumber:'C1',employeeNumber:'E1',fullName:'Borrower One',nationality:'OM',organization:'MOD',isActive:true};
const application={
 loanApplicationId:'a1',borrowerId:'b1',loanProductId:'p1',loanProductVersionId:'v1',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Draft' as const,
 borrowerSnapshot:{civilNumber:'C1',employeeNumber:'E1',fullName:'Borrower One',phoneNumber:'90000000',nationality:'OM',organization:'MOD',rankGrade:'A',employmentInformation:'Active',status:'Active'},
 productSnapshot:{loanProductId:'p1',loanProductVersionId:'v1',productName:'Housing',versionNumber:2,maximumAmount:100000,currency:'OMR',deductionPercentage:10,financingTypes:['Build','Purchase'],eligibilityConfiguration:{requiredNationality:'OM',maximumApplicationCount:1,rankGradeAmountRules:[{rankGrade:'A',maximumAmount:100000}],maximumTermMonths:240,dueDateRule:'Monthly'},effectiveFrom:'2026-01-01',effectiveTo:null,productStatus:'Active',versionStatus:'Published',publishedAtUtc:'2026-01-01T00:00:00Z'},
 createdAtUtc:'2026-01-01T00:00:00Z',updatedAtUtc:'2026-01-01T00:00:00Z',eTag:'etag-1'
};

function show(permissions=['loanApplications.read','loanApplications.create','loanApplications.update']){
 const client=new QueryClient({defaultOptions:{queries:{retry:false},mutations:{retry:false}}});
 return render(<QueryClientProvider client={client}><LoanApplicationsPage permissions={permissions}/></QueryClientProvider>);
}

beforeEach(()=>{applyLanguage('en');fetchMock.mockReset();vi.stubGlobal('fetch',fetchMock)});

it('creates a Draft from active borrower, available version and allowed financing type',async()=>{
 fetchMock.mockImplementation((url:string,init?:RequestInit)=>{
  if(url==='/api/v1/loan-applications?'&&!init?.method)return json({items:[],pageNumber:1,pageSize:25,totalCount:0});
  if(url.includes('/api/v1/borrowers?'))return json({items:[borrower,{...borrower,borrowerId:'b2',fullName:'Inactive',isActive:false}],pageNumber:1,pageSize:100,totalCount:2});
  if(url==='/api/v1/loan-products/available')return json([product]);
  if(url==='/api/v1/loan-applications'&&init?.method==='POST')return json(application,201);
  throw new Error(`Unexpected request: ${url}`);
 });
 show();
 expect(await screen.findByRole('heading',{name:'Loan Applications'})).toBeInTheDocument();
 await userEvent.click(screen.getByRole('button',{name:'New Application'}));
 expect(screen.queryByRole('option',{name:/Inactive/})).not.toBeInTheDocument();
 await userEvent.selectOptions(await screen.findByRole('combobox',{name:'Borrower'}),'b1');
 await userEvent.selectOptions(await screen.findByRole('combobox',{name:'Product Version'}),'v1');
 expect(screen.getByRole('option',{name:'Build'})).toBeInTheDocument();
 expect(screen.getByRole('option',{name:'Purchase'})).toBeInTheDocument();
 await userEvent.selectOptions(screen.getByRole('combobox',{name:'Financing Type'}),'Build');
 await userEvent.type(screen.getByLabelText(/Requested Amount/),'50000');
 await userEvent.click(screen.getByRole('button',{name:'Save Draft'}));
 await waitFor(()=>expect(fetchMock).toHaveBeenCalledWith('/api/v1/loan-applications',expect.objectContaining({
  method:'POST',body:JSON.stringify({borrowerId:'b1',loanProductVersionId:'v1',requestedAmount:50000,financingType:'Build'})
 })));
});

it('opens snapshot-backed detail and edits with If-Match while preserving permission behavior',async()=>{
 fetchMock.mockImplementation((url:string,init?:RequestInit)=>{
  if(url==='/api/v1/loan-applications?'&&!init?.method)return json({items:[{loanApplicationId:'a1',borrowerId:'b1',borrowerName:'Borrower One',loanProductId:'p1',productName:'Housing',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Draft',createdAtUtc:application.createdAtUtc}],pageNumber:1,pageSize:25,totalCount:1});
  if(url==='/api/v1/loan-applications/a1'&&!init?.method)return json(application);
  if(url==='/api/v1/loan-applications/a1'&&init?.method==='PUT')return json({...application,requestedAmount:45000,financingType:'Purchase',eTag:'etag-2'});
  if(url.includes('/api/v1/borrowers?'))return json({items:[],pageNumber:1,pageSize:100,totalCount:0});
  if(url==='/api/v1/loan-products/available')return json([]);
  throw new Error(`Unexpected request: ${url}`);
 });
 show();
 await userEvent.click(await screen.findByText('Borrower One'));
 expect(await screen.findByRole('heading',{name:'Application Detail'})).toBeInTheDocument();
 expect(screen.getByText(/Borrower One — C1 — OM — A/)).toBeInTheDocument();
 expect(screen.getByText(/Housing — Version 2 — Maximum Amount 100000 OMR/)).toBeInTheDocument();
 await userEvent.selectOptions(screen.getByRole('combobox',{name:'Financing Type'}),'Purchase');
 const amount=screen.getByLabelText(/Requested Amount/);await userEvent.clear(amount);await userEvent.type(amount,'45000');
 await userEvent.click(screen.getByRole('button',{name:'Save Draft'}));
 await waitFor(()=>{
  const call=fetchMock.mock.calls.find(([url,init])=>url==='/api/v1/loan-applications/a1'&&(init as RequestInit|undefined)?.method==='PUT');
  expect(call).toBeTruthy();
  const headers=new Headers((call![1] as RequestInit).headers);
  expect(headers.get('If-Match')).toBe('"etag-1"');
  expect(JSON.parse(String((call![1] as RequestInit).body))).toEqual({requestedAmount:45000,financingType:'Purchase'});
 });
});

it('shows concurrency feedback and hides create/update actions without permissions',async()=>{
 let failUpdate=false;
 fetchMock.mockImplementation((url:string,init?:RequestInit)=>{
  if(url==='/api/v1/loan-applications?'&&!init?.method)return json({items:[{loanApplicationId:'a1',borrowerId:'b1',borrowerName:'Borrower One',loanProductId:'p1',productName:'Housing',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Draft',createdAtUtc:application.createdAtUtc}],pageNumber:1,pageSize:25,totalCount:1});
  if(url==='/api/v1/loan-applications/a1'&&!init?.method)return json(application);
  if(url==='/api/v1/loan-applications/a1'&&init?.method==='PUT'&&failUpdate)return json({errorCode:'loanApplications.concurrencyConflict'},412);
  if(url.includes('/api/v1/borrowers?'))return json({items:[],pageNumber:1,pageSize:100,totalCount:0});
  if(url==='/api/v1/loan-products/available')return json([]);
  throw new Error(`Unexpected request: ${url}`);
 });
 const view=show(['loanApplications.read','loanApplications.update']);
 expect(await screen.findByText('Borrower One')).toBeInTheDocument();
 expect(screen.queryByRole('button',{name:'New Application'})).not.toBeInTheDocument();
 await userEvent.click(screen.getByText('Borrower One'));failUpdate=true;
 const amount=await screen.findByLabelText(/Requested Amount/);await userEvent.clear(amount);await userEvent.type(amount,'40000');
 await userEvent.click(screen.getByRole('button',{name:'Save Draft'}));
 expect(await screen.findByRole('alert')).toHaveTextContent('This draft was changed by another user');
 view.unmount();

 fetchMock.mockReset();
 fetchMock.mockImplementation((url:string)=>{
  if(url==='/api/v1/loan-applications?')return json({items:[{loanApplicationId:'a1',borrowerId:'b1',borrowerName:'Borrower One',loanProductId:'p1',productName:'Housing',requestedAmount:50000,currency:'OMR',financingType:'Build',status:'Draft',createdAtUtc:application.createdAtUtc}],pageNumber:1,pageSize:25,totalCount:1});
  if(url==='/api/v1/loan-applications/a1')return json(application);
  if(url.includes('/api/v1/borrowers?'))return json({items:[]});
  if(url==='/api/v1/loan-products/available')return json([]);
  throw new Error(`Unexpected request: ${url}`);
 });
 show(['loanApplications.read']);
 await userEvent.click(await screen.findByText('Borrower One'));
 expect(await screen.findByRole('heading',{name:'Application Detail'})).toBeInTheDocument();
 expect(screen.queryByRole('button',{name:'Save Draft'})).not.toBeInTheDocument();
 await userEvent.click(screen.getByRole('button',{name:'Back'}));
 expect(await screen.findByRole('heading',{name:'Loan Applications'})).toBeInTheDocument();
});

it('renders TASK-06 UI in Arabic RTL',async()=>{
 applyLanguage('ar');
 fetchMock.mockResolvedValue(json({items:[],pageNumber:1,pageSize:25,totalCount:0}));
 show(['loanApplications.read','loanApplications.create']);
 expect(await screen.findByRole('heading',{name:'طلبات القروض'})).toBeInTheDocument();
 expect(screen.getByRole('button',{name:'طلب جديد'})).toBeInTheDocument();
 expect(document.documentElement).toHaveAttribute('dir','rtl');
});

it('evaluates, renders server rule results, and submits only an eligible decision',async()=>{
 const eligible={...application,eTag:'etag-2',eligibilityDecision:{isEligible:true,evaluatedAtUtc:'2026-01-02T00:00:00Z',appliedLoanProductVersionId:'v1',appliedProductVersionNumber:2,requestedAmountAtEvaluation:50000,financingTypeAtEvaluation:'Build',permittedAmount:70000,observedConflictingApplicationCount:0,maximumApplicationCount:1,ruleResults:[{rule:'nationality',passed:true,reasonCode:'eligibility.nationalitySatisfied'}]},submittedAtUtc:null};
 fetchMock.mockImplementation((url:string,init?:RequestInit)=>{if(url==='/api/v1/loan-applications?')return json({items:[{loanApplicationId:'a1',borrowerName:'Borrower One',status:'Draft'}],pageNumber:1,pageSize:25,totalCount:1});if(url==='/api/v1/loan-applications/a1'&&!init?.method)return json({...application,eligibilityDecision:null,submittedAtUtc:null});if(url.endsWith('/evaluate-eligibility'))return json(eligible);if(url.endsWith('/submit'))return json({...eligible,status:'Submitted',submittedAtUtc:'2026-01-03T00:00:00Z',eTag:'etag-3'});throw new Error(url)});
 show(['loanApplications.read','loanApplications.evaluateEligibility','loanApplications.submit']);await userEvent.click(await screen.findByText('Borrower One'));expect(await screen.findByRole('button',{name:'Submit'})).toBeDisabled();await userEvent.click(screen.getByRole('button',{name:'Evaluate Eligibility'}));expect(await screen.findByText('Eligible')).toBeInTheDocument();expect(screen.getByText(/70000 OMR/)).toBeInTheDocument();expect(screen.getByText(/Nationality requirement satisfied/)).toBeInTheDocument();expect(screen.getByRole('button',{name:'Submit'})).toBeEnabled();await userEvent.click(screen.getByRole('button',{name:'Submit'}));expect(await screen.findByText('Submitted')).toBeInTheDocument();expect(screen.queryByRole('button',{name:'Save Draft'})).not.toBeInTheDocument();
});

it('renders ineligible and Arabic reason text',async()=>{applyLanguage('ar');const ineligible={...application,eligibilityDecision:{isEligible:false,evaluatedAtUtc:'2026-01-02T00:00:00Z',permittedAmount:50000,ruleResults:[{rule:'nationality',passed:false,reasonCode:'eligibility.nationalityMismatch'}]},submittedAtUtc:null};fetchMock.mockImplementation((url:string)=>url.endsWith('/a1')?json(ineligible):json({items:[{loanApplicationId:'a1',borrowerName:'Borrower One',status:'Draft'}],pageNumber:1,pageSize:25,totalCount:1}));show(['loanApplications.read']);await userEvent.click(await screen.findByText('Borrower One'));expect(await screen.findByText('غير مؤهل')).toBeInTheDocument();expect(screen.getByText(/الجنسية لا تطابق الشرط المحدد/)).toBeInTheDocument();expect(document.documentElement).toHaveAttribute('dir','rtl')});
