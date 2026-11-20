import { expectNoSeriousViolations } from '../support/audit.ts';
import { loginAsEmployee } from '../support/login.ts';
import { cleanerOnShift, supervisor } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test('phone login, cleaner my-work, and supervisor my-building have zero critical or serious WCAG violations', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'เข้าสู่ระบบ' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await loginAsEmployee(page, cleanerOnShift());
  await expect(page.getByRole('heading', { name: 'งานของฉัน' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await page.goto('/my-account');
  await expect(page.getByRole('heading', { name: 'บัญชีของฉัน' })).toBeVisible();
  await page.getByRole('button', { name: 'ออกจากระบบ' }).click();
  const loggedOut = page.waitForResponse((response) =>
    new URL(response.url()).pathname === '/api/auth/logout' && response.ok());
  await page.getByRole('alertdialog').getByRole('button', { name: 'ออกจากระบบ' }).click();
  await loggedOut;
  await expect(page).toHaveURL(/\/login$/);

  await loginAsEmployee(page, supervisor);
  await expect(page.getByRole('heading', { name: 'ตึกของฉัน' })).toBeVisible();
  await expectNoSeriousViolations(page);
});
