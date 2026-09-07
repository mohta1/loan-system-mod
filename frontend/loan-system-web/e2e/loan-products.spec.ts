import { expect, test } from '@playwright/test';

test('TASK-05 versioned loan product remains immutable and availability follows product status', async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME;
  const password = process.env.E2E_ADMIN_PASSWORD;
  expect(username, 'E2E_ADMIN_USERNAME must be provided').toBeTruthy();
  expect(password, 'E2E_ADMIN_PASSWORD must be provided').toBeTruthy();
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const name = `TASK-05 E2E Product ${unique}`;

  await page.goto('/');
  await page.getByLabel('Username', { exact: true }).fill(username!);
  await page.getByLabel('Password', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.getByRole('button', { name: 'Loan Products' }).click();
  await page.getByRole('button', { name: 'Create Product' }).click();
  await page.getByLabel('Name', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Create', exact: true }).click();
  await page.getByText(name).click();
  await page.getByRole('button', { name: 'Create Draft Version' }).click();
  await page.getByLabel('Maximum Amount', { exact: true }).fill('125000');
  await page.getByLabel('Currency', { exact: true }).fill('OMR');
  await page.getByLabel('Deduction Percentage (0–100)', { exact: true }).fill('25.5');
  await page.getByLabel('Required Nationality', { exact: true }).fill('Configured E2E nationality');
  await page.getByLabel('Maximum Application Count', { exact: true }).fill('2');
  await page.getByLabel('Rank / Grade', { exact: true }).fill('Configured E2E grade A');
  await page.getByLabel('Rank / Grade Maximum Amount', { exact: true }).fill('100000');
  await page.getByRole('button', { name: 'Add Rank / Grade Rule' }).click();
  await page.getByLabel('Rank / Grade', { exact: true }).nth(1).fill('Configured E2E grade B');
  await page.getByLabel('Rank / Grade Maximum Amount', { exact: true }).nth(1).fill('90000');
  await page.getByRole('button', { name: 'Add Rank / Grade Rule' }).click();
  await page.getByLabel('Rank / Grade', { exact: true }).nth(2).fill('Configured E2E grade C');
  await page.getByLabel('Rank / Grade Maximum Amount', { exact: true }).nth(2).fill('80000');
  await page.getByLabel('Maximum Term (months)', { exact: true }).fill('120');
  await page.getByLabel('Due-Date Rule', { exact: true }).fill('Configured E2E due-date rule');
  await expect(page.getByRole('button', { name: 'Purchase Existing House ×' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Build New House ×' })).toBeVisible();
  const createDraftResponse = page.waitForResponse(response => response.request().method() === 'POST' && /\/api\/v1\/loan-products\/[0-9a-f-]+\/versions$/i.test(new URL(response.url()).pathname));
  await page.getByRole('button', { name: 'Save' }).click();
  expect((await createDraftResponse).status()).toBe(201);
  await expect(page.getByText('Draft', { exact: true })).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'Loan Products' }).click();
  await page.getByText(name).click();
  await expect(page.getByText('125000 OMR')).toBeVisible();
  await expect(page.getByText('Configured E2E nationality')).toBeVisible();
  await expect(page.getByText(/Configured E2E grade A — 100000/)).toBeVisible();
  await expect(page.getByText(/Configured E2E grade B — 90000/)).toBeVisible();
  await expect(page.getByText(/Configured E2E grade C — 80000/)).toBeVisible();
  const publishResponse = page.waitForResponse(response => response.request().method() === 'POST' && /\/api\/v1\/loan-products\/[0-9a-f-]+\/versions\/[0-9a-f-]+\/publish$/i.test(new URL(response.url()).pathname));
  await page.getByRole('button', { name: 'Publish' }).click();
  expect((await publishResponse).status()).toBe(200);
  await expect(page.getByText('Published', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Edit Draft Version' })).toHaveCount(0);

  const availableWhileActive = await page.evaluate(async () => {
    const response = await fetch('/api/v1/loan-products/available');
    return { status: response.status, body: await response.json() as Array<{ productName: string }> };
  });
  expect(availableWhileActive.status).toBe(200);
  expect(availableWhileActive.body.some(product => product.productName === name)).toBeTruthy();

  const deactivateResponse = page.waitForResponse(response => response.request().method() === 'POST' && /\/api\/v1\/loan-products\/[0-9a-f-]+\/deactivate$/i.test(new URL(response.url()).pathname));
  await page.getByRole('button', { name: 'Deactivate' }).click();
  expect((await deactivateResponse).status()).toBe(200);
  await expect(page.getByText('Inactive', { exact: true })).toBeVisible();
  const availableWhileInactive = await page.evaluate(async productName => {
    const response = await fetch('/api/v1/loan-products/available');
    return (await response.json() as Array<{ productName: string }>).some(product => product.productName === productName);
  }, name);
  expect(availableWhileInactive).toBeFalsy();
  await expect(page.getByText('Published', { exact: true })).toBeVisible();
  await expect(page.getByText('125000 OMR')).toBeVisible();
  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});
