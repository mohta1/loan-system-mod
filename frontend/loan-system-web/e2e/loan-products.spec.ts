import { expect, test } from '@playwright/test';

test('TASK-05 versioned loan product remains immutable and availability follows product status', async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME;
  const password = process.env.E2E_ADMIN_PASSWORD;
  expect(username, 'E2E_ADMIN_USERNAME must be provided').toBeTruthy();
  expect(password, 'E2E_ADMIN_PASSWORD must be provided').toBeTruthy();
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const name = `TASK-05 E2E Product ${unique}`;

  await page.goto('/');
  await page.getByLabel('Username').fill(username!);
  await page.getByLabel('Password').fill(password!);
  await page.getByRole('button', { name: 'Login' }).click();
  await page.getByRole('button', { name: 'Loan Products' }).click();
  await page.getByRole('button', { name: 'Create Product' }).click();
  await page.getByLabel('Name').fill(name);
  await page.getByRole('button', { name: 'Create', exact: true }).click();
  await page.getByText(name).click();
  await page.getByRole('button', { name: 'Create Draft Version' }).click();
  await page.getByLabel('Maximum Amount').fill('125000');
  await page.getByLabel('Currency').fill('OMR');
  await page.getByLabel('Deduction Percentage (0–100)').fill('25.5');
  await page.getByLabel('Required Nationality').fill('Configured E2E nationality');
  await page.getByLabel('Maximum Application Count').fill('2');
  await page.getByLabel('Rank / Grade').fill('Configured E2E grade');
  await page.getByLabel('Rank / Grade Maximum Amount').fill('100000');
  await page.getByLabel('Maximum Term (months)').fill('120');
  await page.getByLabel('Due-Date Rule').fill('Configured E2E due-date rule');
  await expect(page.getByRole('button', { name: 'Purchase Existing House ×' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Build New House ×' })).toBeVisible();
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByText('Draft')).toBeVisible();

  await page.reload();
  await page.getByRole('button', { name: 'Loan Products' }).click();
  await page.getByText(name).click();
  await expect(page.getByText('125000 OMR')).toBeVisible();
  await expect(page.getByText('Configured E2E nationality')).toBeVisible();
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText('Published')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Edit Draft Version' })).toHaveCount(0);

  const availableWhileActive = await page.evaluate(async () => {
    const response = await fetch('/api/v1/loan-products/available');
    return { status: response.status, body: await response.json() as Array<{ productName: string }> };
  });
  expect(availableWhileActive.status).toBe(200);
  expect(availableWhileActive.body.some(product => product.productName === name)).toBeTruthy();

  await page.getByRole('button', { name: 'Deactivate' }).click();
  await expect(page.getByText('Inactive')).toBeVisible();
  const availableWhileInactive = await page.evaluate(async productName => {
    const response = await fetch('/api/v1/loan-products/available');
    return (await response.json() as Array<{ productName: string }>).some(product => product.productName === productName);
  }, name);
  expect(availableWhileInactive).toBeFalsy();
  await expect(page.getByText('Published')).toBeVisible();
  await expect(page.getByText('125000 OMR')).toBeVisible();
  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
});
