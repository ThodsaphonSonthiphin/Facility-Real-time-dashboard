import { expectNoSeriousViolations, settlePage } from '../support/audit.ts';
import { loginAsAdmin } from '../support/login.ts';
import { holdUntilReleased } from '../support/pageWatch.ts';
import { overviewFixture } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test('admin login and core admin pages have zero critical or serious WCAG violations', async ({ page }) => {
  test.setTimeout(180_000); // The large dataset's populated schedule has thousands of cells to audit.
  await page.goto('/login/admin');
  await expect(page.getByRole('heading', { name: 'แอดมินเข้าสู่ระบบ' })).toBeVisible();
  await expectNoSeriousViolations(page);

  await loginAsAdmin(page);
  for (const path of ['/admin/overview', '/admin/schedules', '/admin/attendance', '/admin/areas', '/admin/buildings']) {
    await test.step(`Audit ${path}`, async () => {
      await page.goto(path);
      await expect(page.getByRole('main')).toBeVisible();
      await expectNoSeriousViolations(page);
    });
  }
});

test('admin navigation and drawers support Tab and Escape keyboard interaction', async ({ page }) => {
  await loginAsAdmin(page);
  await page.goto('/admin/buildings');
  await page.getByRole('button', { name: 'เพิ่มตึก' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(dialog.locator(':focus')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
});

test('Overview retains accessible results while a filter request is pending', async ({ page }) => {
  test.skip(process.env.E2E_DATASET !== 'medium', 'Requires the medium historical Overview fixture');
  await page.setViewportSize({ width: 375, height: 812 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await loginAsAdmin(page);
  const { date, shift, buildingId, submitted } = overviewFixture();
  await page.goto(`/admin/overview?date=${date}&shift=${shift}&building=${buildingId}&group=entered&page=1`);
  const results = page.getByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' });
  await expect(results.getByRole('link', { name: /^ดูพื้นที่ / }).first()).toBeVisible();
  await settlePage(page);
  const table = results.getByRole('table');
  const tableTop = () => table.evaluate((element) => {
    const section = element.closest('section')!;
    return element.getBoundingClientRect().top - section.getBoundingClientRect().top;
  });
  const initialTableTop = await tableTop();

  const release = await holdUntilReleased(page, /\/api\/admin\/overview\/areas\?.*group=submitted/);
  try {
    await page.getByRole('button', { name: `ส่งงานครบ ${submitted} พื้นที่`, exact: true }).click();
    await expect(results.getByText('กำลังอัปเดต...', { exact: true })).toBeVisible();
    await expect(page.getByText('กำลังอัปเดตข้อมูล...', { exact: true })).toBeHidden();
    expect(await tableTop()).toBeCloseTo(initialTableTop, 1);
    await expectNoSeriousViolations(page, { waitForNetwork: false });
  } finally {
    release();
  }
});
