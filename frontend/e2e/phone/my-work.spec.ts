import { expect, test } from '@playwright/test';
import { loginAsEmployee } from '../support/login.ts';
import { holdBack, layoutShiftTotal, recordLayoutShifts } from '../support/pageWatch.ts';
import { cleanerOnShift } from '../support/seed.ts';

// Measured 0.10 before bdfd581: the footer showed while loading and the list pushed it down
test('My Work does not move while its work loads', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);

  await recordLayoutShifts(page);
  await holdBack(page, '**/api/my-work', 800);
  await page.goto('/my-work');
  // exact: while it loads, the old footer's button read "กำลังอัปเดต...", which also contains "อัปเดต"; waiting for the
  // exact name means waiting for the answer, so the shift (when the list arrives) is over before it is measured
  await expect(page.getByRole('button', { name: 'อัปเดต', exact: true })).toBeVisible();

  expect(await layoutShiftTotal(page)).toBeLessThan(0.01);
});
