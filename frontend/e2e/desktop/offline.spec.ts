import { test, expect, observeAdditionalContext } from '../support/test.ts';
import { shiftInIfOffered } from '../support/attendance.ts';
import { phone } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { factoryArea } from '../support/seed.ts';
import { engine } from '../support/browser.ts';

// facility-0064: without the server the page keeps what it shows and says it is reconnecting; back online, it catches up
test('the attendance page says when it loses the network and catches up when it is back @dataset', async ({ page, context, browser }, testInfo) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated Areas and their Cleaners');
  test.setTimeout(180_000);
  test.fixme(engine === 'webkit', 'WebKit offline emulation: HTTP fails and pushes stop, but the hub never reports the loss ' +
    'within 58 s (probe 2026-11-02); not known whether real Safari on a dead network does the same');
  test.fixme(engine === 'firefox', 'Firefox offline emulation: the reconnecting badge did not show within 45 s (run ' +
    '2026-11-03); why not investigated, nor whether real Firefox on a dead network does the same');
  const area = factoryArea(3);
  await loginAsAdmin(page);
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'การเข้างาน', exact: true }).click();
  await page.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill(area.code);
  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: new RegExp(`^${area.code} `) }) });
  await expect(row).toBeVisible();
  await expect(row).not.toContainText('ทำงานอยู่');

  await context.setOffline(true);
  const lost = Date.now();
  const reconnecting = page.getByText('กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)', { exact: true });
  // A network that goes quiet closes nothing: SignalR notices when its server timeout (30 s by default) runs out
  await expect(reconnecting).toBeVisible({ timeout: 45_000 });
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
