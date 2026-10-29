import { expect, type Page } from '@playwright/test';
import { areaCode } from './seed.ts';

/**
 * Waits until D1 shows AR01: as a card section, or, with nothing wrong there (the seed), as one folded line (the design
 * file's "พื้นที่ที่ไม่มีปัญหา"), which is how the page opens.
 */
export async function expectAreaShown(page: Page): Promise<void> {
  await expect(page.getByText(new RegExp(`^${areaCode} `)).first()).toBeVisible();
}

/** Turns "เรียงพื้นที่ที่มีปัญหาขึ้นก่อน" off, so every Area shows its cards instead of a folded line. */
export async function showEveryArea(page: Page): Promise<void> {
  await page.getByRole('checkbox', { name: 'เรียงพื้นที่ที่มีปัญหาขึ้นก่อน' }).uncheck();
}
