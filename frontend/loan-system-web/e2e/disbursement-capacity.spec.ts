import { expect, test } from '@playwright/test';

test('TASK-12 reserves disbursement capacity and rejects over-capacity', async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME, password = process.env.E2E_ADMIN_PASSWORD;
  expect(username).toBeTruthy(); expect(password).toBeTruthy();
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;

  await page.goto('/');
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await Promise.all([
    page.waitForResponse(response => response.url().includes('/api/v1/auth/login') && response.status() === 204),
    page.getByRole('button', { name: 'Login', exact: true }).click(),
  ]);
  await expect(page.getByRole('button', { name: 'Loan Applications', exact: true })).toBeVisible();

  const setup = await page.evaluate(async suffix => {
    const json = async (url: string, init?: RequestInit) => {
      const response = await fetch(url, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } });
      if (!response.ok) throw new Error(`${url}: ${response.status}`);
      return { body: await response.json(), etag: response.headers.get('etag')! };
    };
    const borrower = await json('/api/v1/borrowers', { method: 'POST', body: JSON.stringify({ civilNumber: `T12-C-${suffix}`, employeeNumber: `T12-E-${suffix}`, fullName: `TASK-12 Borrower ${suffix}`, phoneNumber: '90000000', nationality: 'OM', organization: 'MOD', rankGrade: 'A', employmentInformation: 'Active' }) });
    const product = await json('/api/v1/loan-products', { method: 'POST', body: JSON.stringify({ name: `TASK-12 Product ${suffix}` }) });
    const draft = await json(`/api/v1/loan-products/${product.body.loanProductId}/versions`, { method: 'POST', body: JSON.stringify({ maximumAmount: 100000, currency: 'OMR', deductionPercentage: 10, financingTypes: ['Build', 'Purchase'], eligibilityConfiguration: { requiredNationality: 'OM', maximumApplicationCount: 1, rankGradeAmountRules: [{ rankGrade: 'A', maximumAmount: 100000 }], term: { maximumTermMonths: 240, dueDateRule: 'Monthly' } }, effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: null }) });
    await json(`/api/v1/loan-products/${product.body.loanProductId}/versions/${draft.body.versionId}/publish`, { method: 'POST', headers: { 'If-Match': draft.etag }, body: '{}' });
    const application = await json('/api/v1/loan-applications', { method: 'POST', body: JSON.stringify({ borrowerId: borrower.body.borrowerId, loanProductVersionId: draft.body.versionId, requestedAmount: 50000, financingType: 'Build' }) });
    const evaluated = await json(`/api/v1/loan-applications/${application.body.loanApplicationId}/evaluate-eligibility`, { method: 'POST', headers: { 'If-Match': application.etag } });
    const submitted = await json(`/api/v1/loan-applications/${application.body.loanApplicationId}/submit`, { method: 'POST', headers: { 'If-Match': evaluated.etag } });
    const unit = await json(`/api/v1/loan-applications/${application.body.loanApplicationId}/unit-decision`, { method: 'POST', headers: { 'If-Match': submitted.etag }, body: JSON.stringify({ decision: 'approve' }) });
    await json(`/api/v1/loan-applications/${application.body.loanApplicationId}/committee-decision`, { method: 'POST', headers: { 'If-Match': unit.etag }, body: JSON.stringify({ decision: 'approve' }) });
    return { borrowerName: borrower.body.fullName as string, productName: product.body.name as string, applicationId: application.body.loanApplicationId as string };
  }, unique);

  await page.getByRole('button', { name: 'Inspections', exact: true }).click();
  const pendingApplication = page.getByRole('article').filter({ hasText: setup.borrowerName });
  await expect(pendingApplication).toBeVisible();
  await pendingApplication.getByRole('button', { name: 'Create Inspection', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Property Inspection' })).toBeVisible();

  await page.getByLabel('Governorate').fill('Muscat');
  await page.getByLabel('State').fill('Bawshar');
  await page.getByLabel('Area', { exact: true }).fill('Khuwair');
  await page.getByLabel('Inspection Date').fill(new Date().toISOString().slice(0, 10));
  await page.getByLabel('Number of Floors').fill('2');
  await page.getByLabel('Number of Rooms').fill('4');
  await page.getByLabel('Property Area').fill('250');
  await page.getByLabel('Property Condition').fill('Good');
  await page.getByLabel('Inspection Result').fill('Suitable');
  await page.getByLabel('Notes').fill('TASK-12 inspection');

  const saveResponse = page.waitForResponse(response => response.url().includes('/api/v1/inspections/') && response.request().method() === 'PUT' && response.ok());
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await saveResponse;
  await expect(page.getByRole('button', { name: 'Complete Inspection' })).toBeEnabled();

  const completeResponse = page.waitForResponse(response => response.url().includes('/complete') && response.request().method() === 'POST' && response.ok());
  await page.getByRole('button', { name: 'Complete Inspection' }).click();
  await completeResponse;
  await expect(page.getByRole('button', { name: 'Approve', exact: true })).toBeVisible();

  const approveResponse = page.waitForResponse(response => response.url().includes('/decision') && response.request().method() === 'POST' && response.ok());
  await page.getByRole('button', { name: 'Approve', exact: true }).click();
  await approveResponse;
  await expect(page.getByText(/Status:\s*Approved/)).toBeVisible();

  await page.getByRole('button', { name: 'Back', exact: true }).click();
  await page.getByRole('button', { name: 'Loan Applications', exact: true }).click();
  await page.getByText(setup.borrowerName, { exact: true }).click();
  const prerequisites = page.getByRole('region', { name: 'Final Approval Prerequisites' });
  await expect(prerequisites.getByText(/Inspection:\s*Approved/)).toBeVisible();

  for (const [value, name] of [['Ownership', 'ownership'], ['Survey', 'survey'], ['EngineeringDrawing', 'drawing']] as const) {
    await page.getByLabel('Document Type').selectOption(value);
    await page.getByLabel('Choose document').setInputFiles({ name: `${name}-${unique}.txt`, mimeType: 'text/plain', buffer: Buffer.from(`TASK-12 ${name}`) });
    const attachResponse = page.waitForResponse(response => response.url().includes('/documents') && response.request().method() === 'POST' && response.ok());
    await page.getByRole('button', { name: 'Upload', exact: true }).click();
    await attachResponse;
    const documentLabel = value === 'EngineeringDrawing' ? 'Engineering Drawing' : `${value} Document`;
    await expect(prerequisites.getByRole('listitem').filter({ hasText: documentLabel })).toBeVisible();
  }

  await expect(prerequisites.getByText(/Documents:\s*Satisfied/)).toBeVisible();
  const mortgageResponse = page.waitForResponse(response => response.url().includes('/mortgage/completed') && response.request().method() === 'POST' && response.ok());
  await page.getByRole('button', { name: 'Mark Mortgage Completed' }).click();
  await mortgageResponse;
  await expect(prerequisites.getByText(/Overall:\s*Ready for Final Approval/)).toBeVisible();

  const finalResponse = page.waitForResponse(response => response.url().includes('/final-decision') && response.request().method() === 'POST' && response.ok());
  await page.getByRole('button', { name: 'Final Approve', exact: true }).click(); await finalResponse;
  await expect(page.getByText(/Status:\s*Approved/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Final Approve', exact: true })).not.toBeVisible();

  await expect.poll(async () => page.evaluate(async applicationId => {
    const response = await fetch(`/api/v1/loans?sourceApplicationId=${encodeURIComponent(applicationId)}`);
    if (!response.ok) return -1;
    const body = await response.json();
    return body.items.length;
  }, setup.applicationId), { timeout: 10000 }).toBe(1);

  await page.getByRole('button', { name: 'Loan Accounts', exact: true }).click();
  await page.getByLabel('Source Application', { exact: true }).fill(setup.applicationId);
  const sourceApplicationReference = page.locator(`code[title="${setup.applicationId}"]`).first();
  await expect(sourceApplicationReference).toBeVisible();
  await sourceApplicationReference.locator('xpath=ancestor::tr').click();
  await expect(page.getByTitle(setup.applicationId)).toBeVisible();
  await expect(page.getByText(setup.borrowerName, { exact: true })).toBeVisible();
  await expect(page.getByText(setup.productName, { exact: true })).toBeVisible();
  await expect(page.getByText('50000 OMR', { exact: true })).toHaveCount(2);
  await expect(page.getByText('0 OMR', { exact: true })).toHaveCount(4);

  await page.getByLabel('Change Financing Type').selectOption('Purchase');
  const financingResponse = page.waitForResponse(response => response.url().includes('/financing-type') && response.ok());
  await page.getByRole('button', { name: 'Confirm Change' }).click();
  await financingResponse;
  await expect(page.getByLabel('Change Financing Type')).toHaveValue('Purchase');
  await expect(page.getByText('50000 OMR', { exact: true })).toHaveCount(2);

  const loanId = await page.getByText('Loan ID').locator('xpath=following-sibling::dd[1]').locator('code').getAttribute('title');
  expect(loanId).toBeTruthy();
  await page.getByLabel('Amount').fill('20000');
  await page.getByLabel('Beneficiary Name').fill(`TASK-12 Contractor ${unique}`);
  await page.getByLabel('Account Holder').fill('Contractor Account');
  await page.getByLabel('Bank Name').fill('Test Bank');
  await page.getByLabel('Bank Account / IBAN').fill(`SAFE-${unique}`);
  const createdResponse = page.waitForResponse(response => response.url().endsWith('/api/v1/disbursements') && response.request().method() === 'POST' && response.ok());
  await page.getByRole('button', { name: 'Submit Request' }).click();
  const created = await (await createdResponse).json();
  const disbursementId = created.disbursementId as string;
  await expect(page.getByRole('heading', { name: 'Disbursement Details' })).toBeVisible();
  await expect.poll(async () => page.evaluate(async id => (await (await fetch(`/api/v1/disbursements/${id}`)).json()).status, disbursementId), { timeout: 10000 }).toBe('Requested');
  await expect.poll(async () => page.evaluate(async id => (await (await fetch(`/api/v1/loans/${id}`)).json()).reservedDisbursementAmount, loanId), { timeout: 10000 }).toBe(20000);
  const rejected = await page.evaluate(async ({ id, suffix }) => { const response = await fetch('/api/v1/disbursements', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': `over-${suffix}` }, body: JSON.stringify({ loanId: id, amount: 40000, beneficiary: { type: 'PropertyOwner', displayName: 'Owner', accountHolderName: 'Owner', bankName: 'Bank', bankAccountIdentifier: 'REDACTED' }, supportingDocumentIds: [] }) }); return await response.json(); }, { id: loanId!, suffix: unique });
  await expect.poll(async () => page.evaluate(async id => (await (await fetch(`/api/v1/disbursements/${id}`)).json()).status, rejected.disbursementId), { timeout: 10000 }).toBe('Rejected');
  expect(await page.evaluate(async id => (await (await fetch(`/api/v1/loans/${id}`)).json()).reservedDisbursementAmount, loanId)).toBe(20000);

  await page.reload();
  await page.getByRole('button', { name: 'Loan Accounts', exact: true }).click();
  await page.getByLabel('Source Application', { exact: true }).fill(setup.applicationId);
  await expect(page.locator(`code[title="${setup.applicationId}"]`).first()).toBeVisible();
});
