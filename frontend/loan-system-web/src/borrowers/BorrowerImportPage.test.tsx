import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';
import { applyLanguage, resources } from '../i18n';
import { BorrowerImportPage } from './BorrowerImportPage';

const preview = { batchId: 'batch-1', sourceDocumentId: 'doc-1', status: 'Validated', totalRows: 2, validRows: 1, invalidRows: 1, importedRows: 0, failedRows: 0, createdAtUtc: '2026-01-01', rows: [{ rowNumber: 2, status: 'Valid', civilNumber: '001', employeeNumber: 'E1', errorCodes: [] }, { rowNumber: 3, status: 'Invalid', civilNumber: '002', errorCodes: ['borrowerImports.duplicateCivilNumberInFile', 'borrowerImports.numericIdentifierNotSupported'] }] };
const completed = { ...preview, status: 'Completed', importedRows: 1, failedRows: 1, completedAtUtc: '2026-01-02', rows: [{ ...preview.rows[0], status: 'Imported', borrowerId: 'borrower-1' }, { ...preview.rows[1], status: 'Invalid' }] };
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
function show() { render(<QueryClientProvider client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}><BorrowerImportPage /></QueryClientProvider>); }
async function choose() { await userEvent.upload(screen.getByLabelText('Choose Excel workbook'), new File(['xlsx'], 'borrowers.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })); }

beforeEach(() => { applyLanguage('en'); vi.restoreAllMocks(); });

test('chooses, validates, previews row errors, and explicitly executes final counts', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(json(preview, 201)).mockResolvedValueOnce(json(completed)); show();
  expect(screen.getByRole('button', { name: 'Validate' })).toBeDisabled(); await choose(); expect(screen.getByText(/borrowers.xlsx/)).toBeInTheDocument(); await userEvent.click(screen.getByRole('button', { name: 'Validate' }));
  expect(await screen.findByText(/Duplicate Civil Number in workbook/)).toBeInTheDocument(); expect(screen.getByText(/Identifier cells must be stored as text/)).toBeInTheDocument(); expect(screen.getByText(/Total rows:/)).toHaveTextContent('2'); expect(fetch).toHaveBeenCalledTimes(1);
  await userEvent.click(screen.getByRole('button', { name: 'Execute Import' })); await waitFor(() => expect(screen.getByText(/Imported:/)).toHaveTextContent('1')); expect(screen.getByText(/Failed \/ not imported:/)).toHaveTextContent('1'); expect(fetch).toHaveBeenCalledWith('/api/v1/borrower-imports/batch-1/execute', expect.objectContaining({ method: 'POST' })); expect(screen.getByRole('button', { name: 'Execute Import' })).toBeDisabled();
});

test('shows pending validation and execute states without automatic execution', async () => {
  let finishValidate!: (value: Response) => void; const validating = new Promise<Response>(resolve => { finishValidate = resolve; }); const fetch = vi.spyOn(globalThis, 'fetch').mockReturnValueOnce(validating).mockImplementationOnce(() => new Promise(() => undefined)); show(); await choose(); await userEvent.click(screen.getByRole('button', { name: 'Validate' })); expect(screen.getByRole('button', { name: 'Validating…' })).toBeDisabled(); finishValidate(json(preview, 201)); await screen.findByText(/Duplicate Civil Number in workbook/); expect(fetch).toHaveBeenCalledTimes(1); await userEvent.click(screen.getByRole('button', { name: 'Execute Import' })); expect(await screen.findByRole('button', { name: 'Executing…' })).toBeDisabled();
});

test.each([{ status: 400, code: 'borrowerImports.invalidTemplate', text: 'does not match' }, { status: 403, code: '', text: 'do not have permission' }, { status: 404, code: 'borrowerImports.batchNotFound', text: 'not found' }])('shows safe validation API error for $status', async ({ status, code, text }) => {
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ errorCode: code }, status)); show(); await choose(); await userEvent.click(screen.getByRole('button', { name: 'Validate' })); expect(await screen.findByRole('alert')).toHaveTextContent(text);
});

test('shows localized execute failure and Arabic RTL resources', async () => {
  vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(json(preview, 201)).mockResolvedValueOnce(json({ errorCode: 'borrowerImports.batchNotExecutable' }, 409)); show(); await choose(); await userEvent.click(screen.getByRole('button', { name: 'Validate' })); await userEvent.click(await screen.findByRole('button', { name: 'Execute Import' })); expect(await screen.findByRole('alert')).toHaveTextContent('cannot be executed'); applyLanguage('ar'); await waitFor(() => expect(document.documentElement.dir).toBe('rtl')); expect(screen.getByRole('heading', { name: 'استيراد المقترضين' })).toBeInTheDocument(); expect(resources.en.translation.borrowerImport).toBeTruthy(); expect(resources.ar.translation.importDuplicateCivil).toBeTruthy();
});
