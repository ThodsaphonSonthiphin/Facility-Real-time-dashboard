import { expect, type Page } from '@playwright/test';
import { checkInToken } from './seed.ts';

/**
 * Opens AR01's check-in page and presses "เข้างาน" when it is offered. An earlier test in the same run may already
 * have shifted in; then the page offers "ออกพัก" or "เลิกงาน" instead and nothing is pressed.
 */
export async function shiftInIfOffered(page: Page): Promise<void> {
  await page.goto(`/check-in/${checkInToken}`);
  await expect(page.getByRole('button', { name: /^(เข้างาน|ออกพัก|กลับจากพัก|เลิกงาน)$/ }).first()).toBeVisible();

  const shiftIn = page.getByRole('button', { name: 'เข้างาน', exact: true });
  if ((await shiftIn.count()) === 0) {
    return;
  }
  const answer = page.waitForResponse(
    (response) => response.url().endsWith('/api/attendance') && response.request().method() === 'POST',
  );
  await shiftIn.click();
  expect((await answer).ok()).toBe(true);
}
