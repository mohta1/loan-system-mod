import { beforeEach, expect, test, vi } from 'vitest';
import { borrowersApi, type Borrower, type Input } from './borrowers';

const borrower: Borrower = { borrowerId: 'b1', civilNumber: 'C1', employeeNumber: 'E1', fullName: 'Ali', nationality: 'Omani', organization: 'MOD', status: 'Active', createdAt: '2026-01-01', updatedAt: '2026-01-01', eTag: 'AQID' };
const input: Input = { civilNumber: 'C1', employeeNumber: 'E1', fullName: 'Ali', nationality: 'Omani', organization: 'MOD' };
beforeEach(() => vi.restoreAllMocks());

test('searches, creates, and gets borrowers', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch')
    .mockResolvedValueOnce(Response.json({ items: [borrower], pageNumber: 1, pageSize: 25, totalCount: 1 }))
    .mockResolvedValueOnce(Response.json(borrower, { status: 201 }))
    .mockResolvedValueOnce(Response.json(borrower));
  expect((await borrowersApi.search('name=Ali')).items).toHaveLength(1);
  expect((await borrowersApi.create(input)).borrowerId).toBe('b1');
  expect((await borrowersApi.get('b1')).fullName).toBe('Ali');
  expect(fetch.mock.calls[0][0]).toBe('/api/v1/borrowers?name=Ali');
  expect(fetch.mock.calls[1][1]).toMatchObject({ method: 'POST', body: JSON.stringify(input) });
});

test('updates and changes status using the ETag', async () => {
  const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => Response.json(borrower));
  await borrowersApi.update(borrower, { ...input, fullName: 'Changed' });
  await borrowersApi.status(borrower, false);
  await borrowersApi.status({ ...borrower, status: 'Inactive' }, true);
  expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'PUT', credentials: 'same-origin' });
  const updateHeaders = new Headers(fetch.mock.calls[0][1]?.headers);
  expect(updateHeaders.get('Content-Type')).toBe('application/json');
  expect(updateHeaders.get('If-Match')).toBe('"AQID"');
  expect(fetch.mock.calls[1][0]).toContain('/deactivate');
  expect(fetch.mock.calls[2][0]).toContain('/activate');
});

test.each([401, 403, 404, 412])('reports HTTP %s and dispatches session expiry only for 401', async status => {
  const expired = vi.fn(); addEventListener('identity:unauthorized', expired, { once: true });
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ errorCode: status === 412 ? 'borrowers.concurrencyConflict' : undefined }), { status, headers: { 'Content-Type': 'application/json' } }));
  await expect(borrowersApi.get('missing')).rejects.toMatchObject({ status });
  expect(expired).toHaveBeenCalledTimes(status === 401 ? 1 : 0);
});

test('handles a non-json error response', async () => {
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('no', { status: 500 }));
  await expect(borrowersApi.get('b1')).rejects.toMatchObject({ status: 500 });
});
