import { test, expect, observeAdditionalContext } from '../support/test.ts';
import { shiftInIfOffered } from '../support/attendance.ts';
import { phone } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { factoryArea } from '../support/seed.ts';

// facility-0064: without the server the page keeps what it shows and says it is reconnecting; back online, it catches up
test('the attendance page says when it loses the network and catches up when it is back @dataset', async ({ page, context, browser }, testInfo) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated Areas and their Cleaners');
  test.setTimeout(180_000);
  const area = factoryArea(3);
  await loginAsAdmin(page);
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'การเข้างาน', exact: true }).click();
  await page.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill(area.code);
  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: new RegExp(`^${area.code} `) }) });
  await expect(row).toBeVisible();
  await expect(row).not.toContainText('ทำงานอยู่');

  await context.setOffline(true);
  const lost = Date.now();
  const reconnecting = page.getByText('ขาดการเชื่อมต่อ', { exact: true });
  // facility-0107: the browser says at once that its network went
  await expect(reconnecting).toBeVisible({ timeout: 5_000 });
  const noticed = Date.now();
  await expect(row).toBeVisible(); // the table stays while the network is gone

  // The Cleaner checks in from a phone that has signal
  const phoneContext = await browser.newContext({ ...phone, baseURL: testInfo.project.use.baseURL });
  await observeAdditionalContext(phoneContext);
  try {
    const cleaner = await phoneContext.newPage();
    await loginAsEmployee(cleaner, area.cleaner);
    await expect(cleaner).toHaveURL(/\/my-work$/);
    await shiftInIfOffered(cleaner, area.checkIn);
  } finally {
    await phoneContext.close();
  }
  await expect(row).not.toContainText('ทำงานอยู่'); // nothing can reach an offline page

  await context.setOffline(false);
  const back = Date.now();
  // Under the 30 s poll (facility-0061): the page asks again because the network came back
  await expect(row).toContainText('ทำงานอยู่', { timeout: 20_000 });
  const caughtUp = Date.now();
  await expect(reconnecting).toBeHidden({ timeout: 45_000 });
  const seconds = (from: number, to: number) => ((to - from) / 1000).toFixed(1);
  testInfo.annotations.push({ type: 'measured', description:
    `badge after ${seconds(lost, noticed)} s offline; row caught up ${seconds(back, caughtUp)} s and badge gone ` +
    `${seconds(back, Date.now())} s after the network came back` });
});

// facility-0107: on a quiet healthy link no extra connection is opened; when the hub goes silent while online, no false
// disconnect badge appears.
test('an Admin page stays live on a quiet link and stays quiet when only the hub goes silent while online', async ({ page }, testInfo) => {
  test.setTimeout(120_000);
  let silent = false;
  let opened = 0;
  await page.routeWebSocket('**/hubs/scan**', (socket) => {
    opened++;
    const server = socket.connectToServer();
    server.onMessage((message) => {
      if (!silent) socket.send(message);
    });
  });
  await loginAsAdmin(page);
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'การเข้างาน', exact: true }).click();
  const disconnected = page.getByText('ขาดการเชื่อมต่อ', { exact: true });
  await expect.poll(() => opened).toBeGreaterThan(0);
  await expect(page.getByRole('status')).toBeEmpty(); // the hub is live
  // Opening the page may open more than one connection (two seen, 2026-11-03); count only what opens after that
  await page.waitForTimeout(2_000);
  const settled = opened;

  // Two and a half of the page's 10 s waits with nothing to push: only the pings speak
  await page.waitForTimeout(25_000);
  expect(opened).toBe(settled); // no link dropped and opened again
  await expect(disconnected).toBeHidden();

  silent = true;
  await page.waitForTimeout(15_000);
  await expect(disconnected).toBeHidden();
  await expect(page.getByRole('status')).toBeEmpty();
  testInfo.annotations.push({ type: 'measured',
    description: `${settled} connection(s) on opening; stayed quiet while online even after hub pings stopped` });
});
