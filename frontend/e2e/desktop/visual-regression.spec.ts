import { loginAsAdmin } from '../support/login.ts';
import { overviewFixture } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';
import { visualPoints, visualTime, visualWeek } from '../support/visualFixtures.ts';

test.skip(process.env.E2E_DATASET !== 'medium', 'Visual baselines use the deterministic medium dataset');

test('desktop key admin screens match visual baselines', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });

  await page.goto('/login/admin');
  await expect(page.getByRole('heading', { name: 'แอดมินเข้าสู่ระบบ' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-admin-login.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.getByText(/^เวอร์ชัน /)],
  });

  await loginAsAdmin(page);
  const { date, shift, buildingId } = overviewFixture();
  await page.goto(`/admin/overview?date=${date}&shift=${shift}&building=${buildingId}&group=entered&page=1`);
  await expect(page.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-overview.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time'), page.locator('header p'), page.getByLabel('วันที่ของกะ')],
  });

  await page.goto(`/admin/attendance?date=${date}&shift=Night`);
  await expect(page.getByRole('table', { name: 'การเข้างานรายพื้นที่' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-attendance.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time'), page.getByLabel('วันที่', { exact: true })],
  });

  await page.route('**/api/service-points', (route) => route.fulfill({ json: visualPoints }));
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: /^AR01 / })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-dashboard.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
  });

  await page.clock.setFixedTime(visualTime);
  await page.route('**/api/schedules?*', (route) => route.fulfill({ json: visualWeek }));
  await page.goto('/admin/schedules');
  await expect(page.getByRole('heading', { name: 'ตารางงาน' })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: 'แม่บ้านทดสอบ' })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-schedules.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
    mask: [page.locator('time')],
  });

  await page.goto('/admin/buildings');
  await expect(page.getByRole('row', { name: /^A / })).toBeVisible();
  await expect(page).toHaveScreenshot('desktop-buildings.png', {
    animations: 'disabled',
    maxDiffPixelRatio: 0.01,
  });
});
