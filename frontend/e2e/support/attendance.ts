import { expect, type Page } from '@playwright/test';
import type { AttendanceState } from '../../src/shared/api/types.ts';
import { checkInToken } from './seed.ts';

export const attendanceButton = /^(เข้างาน|ออกพัก(?: [12])?|กลับจากพัก(?: [12])?|เลิกงาน)$/;

/**
 * Opens a check-in page (AR01's unless another is given) and presses "เข้างาน" when it is offered. An earlier test in
 * the same run may already have shifted in; then the page offers "ออกพัก" or "เลิกงาน" instead and nothing is pressed.
 */
export async function shiftInIfOffered(page: Page, sign = `/check-in/${checkInToken}`): Promise<void> {
  await page.goto(sign);
  // The page draws its buttons only from a fresh answer, and the hub asks for one again on its first connect
  // (facility-0064): the buttons appear, vanish while that second answer is on its way, then come back. Waiting for
  // the network to settle keeps the decision below out of that gap (it made live-flow fail 3 runs of 4 on 2026-10-30).
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('button', { name: attendanceButton }).first()).toBeVisible();

  const shiftIn = page.getByRole('button', { name: 'เข้างาน', exact: true });
  if ((await shiftIn.count()) === 0) {
    return;
  }
  const answer = page.waitForResponse(
    (response) => response.url().endsWith('/api/attendance') && response.request().method() === 'POST',
  );
  await shiftIn.click();
  const response = await answer;
  expect(response.ok()).toBe(true);
  expect((await response.json() as AttendanceState).outcome).toBe('Recorded');
}


interface TransferResult { id: number; state: string; confirmedAt: string }

/** Scan the destination sign and verify the confirmation was actually accepted. */
export async function recordTransferArrival(page: Page, destinationSignUrl: string): Promise<TransferResult> {
  await page.goto(destinationSignUrl);
  await expect(page.getByText('มีคำสั่งย้ายพื้นที่ที่ยังไม่ได้ยืนยัน')).toBeVisible();
  const saved = page.waitForResponse((response) => new URL(response.url()).pathname.startsWith('/api/area-transfers/')
    && response.url().endsWith('/confirm') && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'ยืนยันเข้าพื้นที่ใหม่', exact: true }).click();
  const answer = await saved;
  expect(answer.ok()).toBe(true);
  await expect(page.getByText('ยืนยันถึงพื้นที่ใหม่แล้ว')).toBeVisible();
  return answer.json() as Promise<TransferResult>;
}
