import { expect, test } from '@playwright/test';
import { loginAsEmployee } from '../support/login.ts';
import { sidewaysOverflow } from '../support/pageWatch.ts';
import { checkInToken, cleanerOnShift } from '../support/seed.ts';

// Chromium's phone emulation has no toolbar that hides while scrolling, so this cannot catch the 100dvh scroll bug
// of 2026-10-27; it guards the page's layout. A real phone is still the check for that bug (spec, 2026-10-28).
test('the check-in page fits the phone and its buttons can be reached', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);
  await page.goto(`/check-in/${checkInToken}`);

  const firstEvent = page.getByRole('button', { name: /^(เข้างาน|ออกพัก|กลับจากพัก|เลิกงาน)$/ }).first();
  await expect(firstEvent).toBeVisible();
  expect(await sidewaysOverflow(page)).toBe(0);

  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await expect(firstEvent).toBeInViewport();
});
