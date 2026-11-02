import type { Request } from '@playwright/test';
import { test, expect } from '../support/test.ts';
import { shiftInIfOffered } from '../support/attendance.ts';
import { loginAsEmployee } from '../support/login.ts';
import { factoryArea, nearShiftChange } from '../support/seed.ts';

const isSubmission = (request: Request) => request.url().endsWith('/api/scan-records') && request.method() === 'POST';

// facility-0052, wireframe M12: nothing waits on the phone to be sent later; the page keeps the choice for ส่งใหม่
test('a submission without signal keeps its choice and is sent once when the signal is back @dataset', async ({ page, context }) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated Areas and their Cleaners');
  test.skip(nearShiftChange(), 'Near the shift change: the round could close during this workflow');
  const area = factoryArea(6);
  await loginAsEmployee(page, area.cleaner);
  await expect(page).toHaveURL(/\/my-work$/);
  await shiftInIfOffered(page, area.checkIn);

  await page.goto(area.point(1));
  await page.getByText('เรียบร้อย', { exact: true }).click();
  const outcomes: string[] = [];
  page.on('requestfailed', (request) => {
    if (isSubmission(request)) outcomes.push('no signal');
  });
  page.on('response', (response) => {
    if (isSubmission(response.request())) outcomes.push(String(response.status()));
  });

  await context.setOffline(true);
  await page.getByRole('button', { name: 'ส่งงาน', exact: true }).click();
  await expect(page.getByText('ส่งไม่สำเร็จ', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeVisible();
  const kept = page.locator('section').filter({ has: page.getByRole('heading', { name: 'สิ่งที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)' }) });
  await expect(kept).toContainText('เรียบร้อย');

  await context.setOffline(false);
  await page.getByRole('button', { name: 'ส่งใหม่' }).click();
  await expect(page.getByRole('heading', { name: /^ส่งงานแล้ว/ })).toBeVisible();
  expect(outcomes).toEqual(['no signal', '201']);
});
