import '../i18n';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { LoanApplication } from '../api/loanApplications';
import { LoanApplicationsPage } from './LoanApplicationsPage';

const application = {
  loanApplicationId: '87654321-1234-1234-1234-abcdef123456',
  borrowerId: '11111111-2222-3333-4444-555555555555',
  loanProductId: 'p1',
  loanProductVersionId: 'v1',
  requestedAmount: 50000,
  currency: 'OMR',
  financingType: 'Build',
  status: 'Approved',
  borrowerSnapshot: { civilNumber: 'CIV-42', employeeNumber: 'EMP-7', fullName: 'Readable Borrower', nationality: 'OM', organization: 'MOD', rankGrade: 'A', status: 'Active' },
  productSnapshot: { loanProductId: 'p1', loanProductVersionId: 'v1', productName: 'Housing Build', versionNumber: 1, maximumAmount: 50000, currency: 'OMR', deductionPercentage: 10, financingTypes: ['Build'], eligibilityConfiguration: { requiredNationality: 'OM', maximumApplicationCount: 1, rankGradeAmountRules: [], maximumTermMonths: 240, dueDateRule: 'Monthly' }, effectiveFrom: '2026-01-01', effectiveTo: null, productStatus: 'Active', versionStatus: 'Published' },
  eligibilityDecision: null,
  unitApproval: null,
  committeeApproval: null,
  rejectedAtUtc: null,
  inspectionPrerequisiteStatus: 'Approved',
  mortgageStatus: 'Completed',
  mortgageDecision: null,
  documentPrerequisiteStatus: 'Satisfied',
  applicationDocuments: [],
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: '2026-01-01T00:00:00Z',
  submittedAtUtc: null,
  eTag: 'etag',
} as LoanApplication;

it('opens a handed-off source application directly in detail view and consumes the handoff', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
  const consumed = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <LoanApplicationsPage permissions={['loanApplications.read']} initialApplication={application} onInitialApplicationConsumed={consumed} />
    </QueryClientProvider>,
  );

  expect(screen.getByRole('heading', { name: 'Application Detail' })).toBeInTheDocument();
  expect(screen.getByText(application.loanApplicationId, { exact: false })).toBeInTheDocument();
  expect(screen.getByText(/Readable Borrower/)).toBeInTheDocument();
  expect(screen.getByText(/Housing Build/)).toBeInTheDocument();
  await waitFor(() => expect(consumed).toHaveBeenCalledTimes(1));
});
