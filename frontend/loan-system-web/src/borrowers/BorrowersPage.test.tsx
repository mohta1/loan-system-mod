import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';
import i18n, { applyLanguage } from '../i18n';
import type { Borrower } from '../api/borrowers';
import { BorrowersPage } from './BorrowersPage';

let borrower: Borrower;
const all = ['borrowers.read', 'borrowers.create', 'borrowers.update', 'borrowers.manageStatus'];
beforeEach(() => { vi.restoreAllMocks(); applyLanguage('en'); borrower = { borrowerId: 'b1', civilNumber: 'C1', employeeNumber: 'E1', fullName: 'Ali Borrower', phoneNumber: '9', nationality: 'Omani', organization: 'MOD', rankGrade: 'G7', employmentInformation: 'Staff', status: 'Active', createdAt: '2026-01-01', updatedAt: '2026-01-01', eTag: 'AQID' }; vi.stubGlobal('confirm', vi.fn(() => true)); });
function show(permissions = all) { render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><BorrowersPage permissions={permissions} /></QueryClientProvider>); }
function api() { return vi.spyOn(globalThis, 'fetch').mockImplementation(async (value, init) => { const url = String(value); if (!init?.method && url.endsWith('/b1')) return Response.json(borrower); if (init?.method === 'POST' && !url.endsWith('activate') && !url.endsWith('deactivate')) return Response.json(borrower, { status: 201 }); if (init?.method === 'PUT') { borrower = { ...borrower, ...JSON.parse(String(init.body)), eTag: 'NEW' }; return Response.json(borrower); } if (url.endsWith('deactivate')) { borrower = { ...borrower, status: 'Inactive' }; return Response.json(borrower); } if (url.endsWith('activate')) { borrower = { ...borrower, status: 'Active' }; return Response.json(borrower); } return Response.json({ items: [{ ...borrower, isActive: borrower.status === 'Active' }], pageNumber: 1, pageSize: 25, totalCount: 30 }); }); }

test('renders, searches, paginates, and opens details with permission-aware controls', async () => {
  const fetch = api(); show(); expect(await screen.findByText('Ali Borrower')).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText('Name'), 'Ali'); await userEvent.selectOptions(screen.getByLabelText('Status'), 'Active'); await userEvent.click(screen.getByRole('button', { name: 'Search' }));
  await waitFor(() => expect(String(fetch.mock.calls.at(-1)?.[0])).toContain('name=Ali'));
  const searches = fetch.mock.calls.length; await userEvent.click(screen.getByRole('button', { name: 'Search' })); await waitFor(() => expect(fetch.mock.calls.length).toBeGreaterThan(searches));
  await userEvent.click(screen.getByRole('button', { name: 'Next' })); await waitFor(() => expect(String(fetch.mock.calls.at(-1)?.[0])).toContain('pageNumber=2'));
  await userEvent.click(screen.getByText('Ali Borrower')); expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Deactivate' })).toBeInTheDocument();
});

test('validates and successfully creates', async () => {
  const fetch = api(); show(); await screen.findByText('Ali Borrower'); await userEvent.click(screen.getByRole('button', { name: /Create Borrower/ }));
  const create = screen.getByRole('button', { name: 'Create' }); expect(create).toBeDisabled();
  await userEvent.type(screen.getByLabelText(/Civil Number/), 'C2'); await userEvent.type(screen.getByLabelText(/Full Name/), 'New'); await userEvent.type(screen.getByLabelText(/Nationality/), 'Omani'); await userEvent.type(screen.getByLabelText(/Organization/), 'MOD'); expect(create).toBeEnabled(); await userEvent.click(create);
  await waitFor(() => expect(fetch.mock.calls.some(x => x[1]?.method === 'POST')).toBe(true));
});

test('shows duplicate civil number and concurrency errors', async () => {
  vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(Response.json({ items: [{ ...borrower, isActive: borrower.status === 'Active' }], pageNumber: 1, pageSize: 25, totalCount: 1 })).mockResolvedValueOnce(Response.json({ errorCode: 'borrowers.civilNumberConflict' }, { status: 409 })); show(); await screen.findByText('Ali Borrower'); await userEvent.click(screen.getByRole('button', { name: /Create Borrower/ })); for (const [label, value] of [['Civil Number','C1'],['Full Name','New'],['Nationality','Omani'],['Organization','MOD']]) await userEvent.type(screen.getByLabelText(new RegExp(label)), value); await userEvent.click(screen.getByRole('button', { name: 'Create' })); expect(await screen.findByRole('alert')).toHaveTextContent('already exists');
});

test('edits with initially disabled Save and deactivates then activates', async () => {
  const fetch = api(); show(); await userEvent.click(await screen.findByText('Ali Borrower')); await userEvent.click(screen.getByRole('button', { name: 'Edit' })); const save = screen.getByRole('button', { name: 'Save' }); expect(save).toBeDisabled(); await userEvent.clear(screen.getByLabelText(/Full Name/)); await userEvent.type(screen.getByLabelText(/Full Name/), 'Changed'); await userEvent.click(save); await waitFor(() => expect(fetch.mock.calls.some(x => x[1]?.method === 'PUT')).toBe(true)); await userEvent.click(await screen.findByText('Changed')); await userEvent.click(screen.getByRole('button', { name: 'Deactivate' })); expect(await screen.findByText('Inactive')).toBeInTheDocument(); await userEvent.click(screen.getByRole('button', { name: 'Activate' })); expect(await screen.findByText('Active')).toBeInTheDocument();
});

test('hides mutation controls for read-only users and translates Arabic', async () => {
  api(); show(['borrowers.read']); await userEvent.click(await screen.findByText('Ali Borrower')); expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument(); applyLanguage('ar'); await waitFor(() => expect(i18n.language).toBe('ar')); expect(document.documentElement.dir).toBe('rtl');
});

test.each([{ status: 403, text: 'do not have permission' }, { status: 500, text: 'operation failed' }])('shows list error UX for $status', async ({ status, text }) => {
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({}), { status, headers: { 'Content-Type': 'application/json' } }));
  show();
  expect(await screen.findByRole('alert')).toHaveTextContent(text);
});

test('shows stale update concurrency message', async () => {
  vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(Response.json({ items: [{ ...borrower, isActive: borrower.status === 'Active' }], pageNumber: 1, pageSize: 25, totalCount: 1 }))
    .mockResolvedValueOnce(Response.json(borrower))
    .mockResolvedValueOnce(Response.json({ errorCode: 'borrowers.concurrencyConflict' }, { status: 412 }));
  show();
  await userEvent.click(await screen.findByText('Ali Borrower'));
  await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
  await userEvent.clear(screen.getByLabelText(/Full Name/));
  await userEvent.type(screen.getByLabelText(/Full Name/), 'Concurrent Change');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('changed by another user');
});

test('failed deactivate is visible and preserves Active status', async () => {
  vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(Response.json({ items: [{ ...borrower, isActive: true }], pageNumber: 1, pageSize: 25, totalCount: 1 }))
    .mockResolvedValueOnce(Response.json(borrower))
    .mockResolvedValueOnce(Response.json({}, { status: 403 }));
  show();
  await userEvent.click(await screen.findByText('Ali Borrower'));
  await userEvent.click(await screen.findByRole('button', { name: 'Deactivate' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('do not have permission');
  expect(screen.getByText('Active')).toBeInTheDocument();
  expect(screen.queryByText('Inactive')).not.toBeInTheDocument();
});

test('failed activate shows localized concurrency error and preserves Inactive status', async () => {
  borrower = { ...borrower, status: 'Inactive' };
  vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(Response.json({ items: [{ ...borrower, isActive: false }], pageNumber: 1, pageSize: 25, totalCount: 1 }))
    .mockResolvedValueOnce(Response.json(borrower))
    .mockResolvedValueOnce(Response.json({ errorCode: 'borrowers.concurrencyConflict' }, { status: 412 }));
  applyLanguage('ar');
  show();
  await userEvent.click(await screen.findByText('Ali Borrower'));
  await userEvent.click(await screen.findByRole('button', { name: 'تفعيل' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('تم تعديل المقترض');
  expect(screen.getByText('غير نشط')).toBeInTheDocument();
  expect(document.documentElement.dir).toBe('rtl');
});
