import { expect, test } from '@playwright/test';

test('TASK-03 borrower CRUD lifecycle remains persisted and readable', async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME;
  const password = process.env.E2E_ADMIN_PASSWORD;
  test.skip(!username || !password, 'Disposable credentials must be injected');
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const civil = `e2e-civil-${unique}`;
  const employee = `e2e-employee-${unique}`;
  const fullName = `TASK-03 E2E Borrower ${unique}`;

  const borrowerResponse = (method: string, suffix = '') => page.waitForResponse(response => {
    const path = new URL(response.url()).pathname;
    return response.request().method() === method
      && /\/api\/v1\/borrowers\/[0-9a-f-]+/i.test(path)
      && path.endsWith(suffix);
  });

  const search = async (status = '') => {
    await page.getByLabel('Civil Number').fill(civil);
    await page.getByLabel('Status').selectOption(status);
    const responsePromise = page.waitForResponse(response => {
      const url = new URL(response.url());
      return response.request().method() === 'GET'
        && url.pathname === '/api/v1/borrowers'
        && url.searchParams.get('civilNumber') === civil
        && url.searchParams.get('status') === (status || null);
    });
    await page.getByRole('button', { name: 'Search' }).click();
    expect((await responsePromise).ok()).toBeTruthy();
    const row = page.getByRole('row').filter({ hasText: civil });
    await expect(row).toHaveCount(1);
    return row;
  };

  const openBorrower = async (status = '') => {
    const row = await search(status);
    const responsePromise = borrowerResponse('GET');
    await row.getByText(fullName).click();
    expect((await responsePromise).ok()).toBeTruthy();
  };

  await page.goto('/');
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password').fill(password!);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.getByRole('button', { name: 'Borrowers' }).click();
  await page.getByRole('button', { name: /Create Borrower/ }).click();
  await page.getByLabel(/Civil Number/).fill(civil);
  await page.getByLabel(/Employee Number/).fill(employee);
  await page.getByLabel(/Full Name/).fill(fullName);
  await page.getByLabel(/Phone Number/).fill('90000000');
  await page.getByLabel(/Nationality/).fill('Omani');
  await page.getByLabel(/Organization/).fill('E2E Organization');
  await page.getByLabel(/Rank \/ Grade/).fill('G7');
  const createResponse = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/borrowers' && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Create' }).click();
  expect((await createResponse).status()).toBe(201);

  await openBorrower();
  await expect(page.getByText('E2E Organization')).toBeVisible();

  await page.getByRole('button', { name: 'Edit' }).click();
  await expect(page.getByLabel(/Full Name/)).toHaveValue(fullName);
  await page.getByLabel(/Organization/).fill('Persisted E2E Organization');
  const updateResponse = borrowerResponse('PUT');
  await page.getByRole('button', { name: 'Save' }).click();
  expect((await updateResponse).ok()).toBeTruthy();

  await openBorrower();
  await expect(page.getByText('Persisted E2E Organization')).toBeVisible();

  page.once('dialog', dialog => dialog.accept());
  const deactivateResponse = borrowerResponse('POST', '/deactivate');
  await page.getByRole('button', { name: 'Deactivate' }).click();
  expect((await deactivateResponse).ok()).toBeTruthy();
  await expect(page.getByText('Inactive')).toBeVisible();

  await page.getByRole('button', { name: 'Back' }).click();
  await openBorrower('Inactive');
  await expect(page.getByText('Inactive')).toBeVisible();

  page.once('dialog', dialog => dialog.accept());
  const activateResponse = borrowerResponse('POST', '/activate');
  await page.getByRole('button', { name: 'Activate' }).click();
  expect((await activateResponse).ok()).toBeTruthy();
  await expect(page.getByText('Active')).toBeVisible();

  await page.getByRole('button', { name: 'Back' }).click();
  await page.getByRole('button', { name: /Create Borrower/ }).click();
  await page.getByLabel(/Civil Number/).fill(civil);
  await page.getByLabel(/Full Name/).fill(`${fullName} duplicate`);
  await page.getByLabel(/Nationality/).fill('Omani');
  await page.getByLabel(/Organization/).fill('E2E Organization');
  const duplicateResponse = page.waitForResponse(response => new URL(response.url()).pathname === '/api/v1/borrowers' && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Create' }).click();
  expect((await duplicateResponse).status()).toBe(409);
  await expect(page.getByRole('alert')).toContainText('Civil Number already exists');

  await page.getByRole('button', { name: 'Cancel' }).click();
  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});
