import { test, expect } from '../support/test.ts';
import { desktop } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { factoryArea, signLocation } from '../support/seed.ts';
import type { BrowserContext, Page } from '@playwright/test';
import { engine } from '../support/browser.ts';

test.beforeEach(() => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated Areas and their Cleaners');
});

// 0.01° of latitude north of every sign: about 1,112 m, far outside the 50 m radius (facility-0038)
const farAway = { latitude: signLocation.latitude + 0.01, longitude: signLocation.longitude, accuracy: 10 };

const recorded = (page: Page) =>
  page.waitForResponse((response) => response.url().endsWith('/api/attendance') && response.request().method() === 'POST');

/** Opens the check-in sign once its buttons come from a fresh answer (see shiftInIfOffered). */
async function openCheckIn(page: Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('button', { name: 'เข้างาน', exact: true })).toBeVisible();
}

// facility-0035: far from the sign is recorded and flagged for the Admin, never refused
test('a check-in far from the sign is recorded with a flag the Admin sees @dataset', async ({ page, context, browser }, testInfo) => {
  const area = factoryArea(5);
  await loginAsEmployee(page, area.cleaner);
  await expect(page).toHaveURL(/\/my-work$/);
  await context.setGeolocation(farAway);
  await openCheckIn(page, area.checkIn);

  const answer = recorded(page);
  await page.getByRole('button', { name: 'เข้างาน', exact: true }).click();
  const response = await answer;
  expect(response.ok()).toBe(true);
  const state = (await response.json()) as { outcome: string; events: { eventType: string; withinRadius: boolean | null }[] };
  expect(state.outcome).toBe('Recorded');
  expect(state.events.find((event) => event.eventType === 'ShiftIn')?.withinRadius).toBe(false);
  await expect(page.getByText(/^บันทึกเวลาเข้างานแล้ว \d\d:\d\d แต่อยู่ไกลป้าย$/)).toBeVisible();
  await expect(page.getByText('ความแม่นยำของตำแหน่ง ±10 ม.', { exact: true })).toBeVisible();

  const adminContext = await browser.newContext({ ...desktop, baseURL: testInfo.project.use.baseURL });
  try {
    const admin = await adminContext.newPage();
    await loginAsAdmin(admin);
    await admin.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'การเข้างาน', exact: true }).click();
    await admin.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill(area.code);
    const row = admin.getByRole('row').filter({ has: admin.getByRole('rowheader', { name: new RegExp(`^${area.code} `) }) });
    const flag = row.getByRole('img', { name: /^ไกลป้าย \d+ ม\.$/ });
    await expect(flag).toBeVisible();
    const metres = Number(/(\d+)/.exec((await flag.getAttribute('aria-label')) ?? '')?.[1]);
    expect(metres).toBeGreaterThan(1_100);
    expect(metres).toBeLessThan(1_125);
  } finally {
    await adminContext.close();
  }
});

/**
 * The person said no to location. A visible Chromium would otherwise ask with its own prompt and wait for an answer
 * (seen 2026-11-02), so it gets a real browser-level refusal; WebKit refuses by itself once the permission is cleared.
 */
async function refuseLocation(context: BrowserContext, page: Page) {
  await context.clearPermissions();
  await setChromiumLocation(context, page, 'denied');
}

/** The person turned location back on; on Chromium, the same way it was turned off. */
async function allowLocation(context: BrowserContext, page: Page) {
  await context.grantPermissions(['geolocation']);
  await setChromiumLocation(context, page, 'granted');
}

async function setChromiumLocation(context: BrowserContext, page: Page, setting: 'denied' | 'granted') {
  if (engine !== 'chromium') return;
  const { targetInfo } = await (await context.newCDPSession(page)).send('Target.getTargetInfo');
  await (await context.browser()!.newBrowserCDPSession()).send('Browser.setPermission', {
    permission: { name: 'geolocation' }, setting, origin: new URL(page.url()).origin,
    browserContextId: targetInfo.browserContextId,
  });
}

// facility-0036: no location permission is the one case that blocks; turned on, the same press goes through
test('with location turned off a check-in waits until it is turned on @dataset', async ({ page, context }) => {
  const area = factoryArea(7);
  await loginAsEmployee(page, area.cleaner);
  await expect(page).toHaveURL(/\/my-work$/);
  await refuseLocation(context, page);
  await openCheckIn(page, area.checkIn);
  let sent = 0;
  page.on('request', (request) => {
    if (request.url().endsWith('/api/attendance') && request.method() === 'POST') sent++;
  });

  await page.getByRole('button', { name: 'เข้างาน', exact: true }).click();
  await expect(page.getByText('เปิดตำแหน่งก่อนลงเวลา', { exact: true })).toBeVisible();
  expect(sent).toBe(0);

  await allowLocation(context, page);
  const answer = recorded(page);
  await page.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง', exact: true }).click();
  expect((await answer).ok()).toBe(true);
  await expect(page.getByText(/^บันทึกเวลาเข้างานแล้ว \d\d:\d\d$/)).toBeVisible();
  expect(sent).toBe(1);
});
