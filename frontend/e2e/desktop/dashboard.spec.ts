import { expect, test } from '../support/test.ts';
import { loginAsAdmin } from '../support/login.ts';
import { expectAreaShown } from '../support/dashboard.ts';
import { holdBack, layoutShiftTotal, recordLayoutShifts, sidewaysOverflow, textEverShown, watchForText } from '../support/pageWatch.ts';

const badge = 'กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)';

test('Admin navigation keeps its geometry while alert counts load at phone, narrow and desktop widths', async ({ page }, testInfo) => {
  await loginAsAdmin(page);
  for (const width of [375, 700, 1280]) {
    await page.setViewportSize({ width, height: 844 });
    let release!: () => void;
    const gate = new Promise<void>((resolve) => { release = resolve; });
    await page.route('**/api/admin/alerts', async (route) => { await gate; await route.fallback(); });
    try {
      await page.goto('/dashboard');
      await expectAreaShown(page);
      const nav = page.getByRole('navigation', { name: 'เมนูแอดมิน' });
      const alertLink = nav.getByRole('link', { name: 'รายการที่ขึ้นเตือน', exact: true });
      await expect(alertLink).toBeVisible();
      const before = { nav: await nav.boundingBox(), link: await alertLink.boundingBox() };
      const response = page.waitForResponse('**/api/admin/alerts');
      release();
      const alerts = await (await response).json() as { reviewed: boolean }[];
      const count = alerts.filter((alert) => !alert.reviewed).length;
      const loaded = nav.getByRole('link', { name: count ? `รายการที่ขึ้นเตือน (${count})` : 'รายการที่ขึ้นเตือน', exact: true });
      await expect(loaded).toBeVisible();
      if (count) await expect(loaded.getByTitle(String(count))).toHaveText(count > 999 ? '999+' : String(count));
      expect({ nav: await nav.boundingBox(), link: await loaded.boundingBox() }).toEqual(before);
      expect(await sidewaysOverflow(page)).toBe(0);
      await testInfo.attach(`navigation-${width}px`, { body: await page.screenshot(), contentType: 'image/png' });
    } finally {
      release();
      await page.unroute('**/api/admin/alerts');
    }
  }
});

test('an Admin lands on D1 and sees AR01', async ({ page }) => {
  await loginAsAdmin(page);
  await expect(page.getByRole('heading', { name: 'สถานะจุดทำความสะอาด' })).toBeVisible();
  await expectAreaShown(page);
});

test.describe('on a narrow window (half a laptop screen)', () => {
  // On a wide window the picker has room and its widening moves little (0.002 on the old code)
  test.use({ viewport: { width: 700, height: 844 } });

  // Measured 0.04 in Lighthouse mobile before bdfd581, about 0.017 at 700px here (measured on this Mac; depends on the
  // system font, so re-run the proof on any new machine)
  test('D1 does not move when the building names arrive', async ({ page }, testInfo) => {
    await loginAsAdmin(page);

    await recordLayoutShifts(page);
    await holdBack(page, '**/api/admin/buildings', 800);
    await page.goto('/dashboard');
    await expectAreaShown(page);
    // The shift happens when the names land, so wait for a building beyond "ทุกตึก" before measuring
    await expect(page.getByLabel('ตึก').getByRole('option')).not.toHaveCount(1);

    const total = await layoutShiftTotal(page);
    await testInfo.attach('layout-shifts', { body: JSON.stringify({ total, entries: await page.evaluate(() => window.e2eLayoutEntries) }, null, 2), contentType: 'application/json' });
    expect(total).toBeLessThan(0.01);
  });
});

// facility-0064 as amended 2026-10-28: the badge flashed on every open. The dropped-hub case is LiveBadge.test.tsx
test('D1 shows no badge while the first live connection is made', async ({ page }) => {
  await loginAsAdmin(page);

  await watchForText(page, badge);
  await holdBack(page, '**/hubs/scan/negotiate*', 1500);
  const negotiated = page.waitForResponse('**/hubs/scan/negotiate*');
  await page.goto('/dashboard');
  await expectAreaShown(page);
  await negotiated;

  expect(await textEverShown(page)).toBe(false);
});
