import { expect, test } from '@playwright/test';

test('TASK-03 borrower CRUD lifecycle remains persisted and readable', async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME;
  const password = process.env.E2E_ADMIN_PASSWORD;
  test.skip(!username || !password, 'Disposable credentials must be injected');
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const civil = `e2e-civil-${unique}`;
  const employee = `e2e-employee-${unique}`;

  await page.goto('/');
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password').fill(password!);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.getByRole('button', { name: 'Borrowers' }).click();
  await page.getByRole('button', { name: /Create Borrower/ }).click();
  await page.getByLabel(/Civil Number/).fill(civil);
  await page.getByLabel(/Employee Number/).fill(employee);
  await page.getByLabel(/Full Name/).fill('TASK-03 E2E Borrower');
  await page.getByLabel(/Phone Number/).fill('90000000');
  await page.getByLabel(/Nationality/).fill('Omani');
  await page.getByLabel(/Organization/).fill('E2E Organization');
  await page.getByLabel(/Rank \/ Grade/).fill('G7');
  const createResponse = page.waitForResponse(response => response.url().endsWith('/api/v1/borrowers') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Create' }).click();
  expect((await createResponse).status()).toBe(201);

  await page.getByLabel('Civil Number').fill(civil);
  const searchResponse = page.waitForResponse(response => response.url().includes('/api/v1/borrowers?') && response.request().method() === 'GET');
  await page.getByRole('button', { name: 'Search' }).click();
  expect((await searchResponse).ok()).toBeTruthy();
  const rowName = page.getByText('TASK-03 E2E Borrower');
  await expect(rowName).toBeVisible();
  const detailResponse = page.waitForResponse(response => /\/api\/v1\/borrowers\/[0-9a-f-]+$/i.test(new URL(response.url()).pathname) && response.request().method() === 'GET');
  await rowName.click();
  expect((await detailResponse).ok()).toBeTruthy();
  await expect(page.getByText('E2E Organization')).toBeVisible();

  await page.getByRole('button', { name: 'Edit' }).click();
  await expect(page.getByLabel(/Full Name/)).toHaveValue('TASK-03 E2E Borrower');
  await page.getByLabel(/Organization/).fill('Persisted E2E Organization');
  const updateResponse = page.waitForResponse(response => /\/api\/v1\/borrowers\/[0-9a-f-]+$/i.test(new URL(response.url()).pathname) && response.request().method() === 'PUT');
  await page.getByRole('button', { name: 'Save' }).click();
  expect((await updateResponse).ok()).toBeTruthy();

  await page.getByLabel('Civil Number').fill(civil);
  await page.getByRole('button', { name: 'Search' }).click();
  await page.getByText('TASK-03 E2E Borrower').click();
  await expect(page.getByText('Persisted E2E Organization')).toBeVisible();

  page.once('dialog', dialog => dialog.accept());
  const deactivateResponse = page.waitForResponse(response => response.url().endsWith('/deactivate') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Deactivate' }).click();
  expect((await deactivateResponse).ok()).toBeTruthy();
  await expect(page.getByText('Inactive')).toBeVisible();

  await page.getByRole('button', { name: 'Back' }).click();
  await page.getByLabel('Status').selectOption('Inactive');
  await page.getByRole('button', { name: 'Search' }).click();
  await page.getByText('TASK-03 E2E Borrower').click();
  await expect(page.getByText('Inactive')).toBeVisible();

  page.once('dialog', dialog => dialog.accept());
  const activateResponse = page.waitForResponse(response => response.url().endsWith('/activate') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Activate' }).click();
  expect((await activateResponse).ok()).toBeTruthy();
  await expect(page.getByText('Active')).toBeVisible();
  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});
