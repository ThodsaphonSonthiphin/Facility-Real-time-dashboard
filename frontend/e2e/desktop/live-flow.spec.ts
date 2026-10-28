import { expect, test } from '@playwright/test';
import { shiftInIfOffered } from '../support/attendance.ts';
import { phone } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { beforeFirstNightRound, cleanerOnShift, restroomName, restroomToken } from '../support/seed.ts';

test('a submission from the phone shows on D1 without a reload', async ({ page, browser }, testInfo) => {
  test.skip(beforeFirstNightRound(), '19:00–20:00: the Night shift has no round yet, so D1 shows no submission');
  const cleaner = cleanerOnShift();

  await loginAsAdmin(page);
  const tile = page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: restroomName }) });
  await expect(tile).toBeVisible();
  await expect(tile).not.toContainText(cleaner.name);

  const phoneContext = await browser.newContext({ ...phone, baseURL: testInfo.project.use.baseURL });
  try {
    const cleanerPage = await phoneContext.newPage();
    await loginAsEmployee(cleanerPage, cleaner);
    await expect(cleanerPage).toHaveURL(/\/my-work$/);
    await shiftInIfOffered(cleanerPage);

    await cleanerPage.goto(`/scan/${restroomToken}`);
    await cleanerPage.getByText('เรียบร้อย', { exact: true }).click();
    await cleanerPage.getByRole('button', { name: 'ส่งงาน' }).click();
    await expect(cleanerPage.getByRole('heading', { name: /^ส่ง/ })).toBeVisible();
  } finally {
    await phoneContext.close();
  }

  // D1 polls every 30 s (facility-0061); 10 s means the push brought it
  await expect(tile).toContainText(cleaner.name, { timeout: 10_000 });
});
