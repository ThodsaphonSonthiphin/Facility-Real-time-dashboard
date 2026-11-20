import { expectNoSeriousViolations } from '../support/audit.ts';
import { loginAsAdmin } from '../support/login.ts';
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
