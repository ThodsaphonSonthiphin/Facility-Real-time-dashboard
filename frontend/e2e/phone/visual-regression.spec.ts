import { loginAsEmployee } from '../support/login.ts';
import { cleanerOnShift, restroomName, supervisor } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';
import { visualBuilding, visualTime, visualWeek, visualWork } from '../support/visualFixtures.ts';

test.skip(process.env.E2E_DATASET !== 'medium', 'Visual baselines use the deterministic medium dataset');

test('phone key screens match visual baselines', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });

  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'เข้าสู่ระบบ' })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-login.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('footer .text-xs')],
  });

  await page.clock.setFixedTime(visualTime);
  await page.route('**/api/my-work', (route) => route.fulfill({ json: visualWork }));
  await page.route('**/api/schedules/me?*', (route) => route.fulfill({ json: visualWeek }));
  await page.route('**/api/my-building', (route) => route.fulfill({ json: visualBuilding }));
  await loginAsEmployee(page, cleanerOnShift(visualTime));
  await expect(page.getByRole('heading', { name: restroomName })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-work.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });

  await page.goto('/my-schedule');
  await expect(page.getByRole('heading', { name: 'ตารางของฉัน' })).toBeVisible();
  await expect(page.getByRole('article').first()).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-schedule.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
  });

  await page.goto('/my-account');
  await expect(page.getByRole('heading', { name: 'บัญชีของฉัน' })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-account.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });

  await page.getByRole('button', { name: 'ออกจากระบบ' }).click();
  const loggedOut = page.waitForResponse((response) =>
    new URL(response.url()).pathname === '/api/auth/logout' && response.ok());
  await page.getByRole('alertdialog').getByRole('button', { name: 'ออกจากระบบ' }).click();
  await loggedOut;
  await expect(page).toHaveURL(/\/login$/);

  await loginAsEmployee(page, supervisor);
  await expect(page.getByRole('heading', { name: 'ตึกของฉัน' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'สรุปและกรองการตรวจ' })).toBeVisible();
  await expect(page).toHaveScreenshot('phone-my-building.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
  });
});
