import { expect, test } from '@playwright/test';
import { loginAsEmployee } from '../support/login.ts';
import { cleanerOnShift, supervisor } from '../support/seed.ts';

test('a Cleaner lands on My Work', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);
  await expect(page.getByRole('heading', { name: 'งานของฉัน' })).toBeVisible();
});

test('a Supervisor lands on My Building', async ({ page }) => {
  await loginAsEmployee(page, supervisor);
  await expect(page).toHaveURL(/\/my-building$/);
});

test('a wrong phone number is refused', async ({ page }) => {
  await loginAsEmployee(page, { employeeId: cleanerOnShift().employeeId, phone: '0899999999' });
  await expect(page.getByText('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});
