import { expect, test } from '@playwright/test';
import { loginAsAdmin } from '../support/login.ts';
import { expectAreaShown } from '../support/dashboard.ts';
import { holdBack, layoutShiftTotal, recordLayoutShifts, textEverShown, watchForText } from '../support/pageWatch.ts';

const badge = 'กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)';

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
  test('D1 does not move when the building names arrive', async ({ page }) => {
    await loginAsAdmin(page);

    await recordLayoutShifts(page);
    await holdBack(page, '**/api/admin/buildings', 800);
    await page.goto('/dashboard');
    await expectAreaShown(page);
    // The shift happens when the names land, so wait for a building beyond "ทุกตึก" before measuring
    await expect(page.getByLabel('ตึก').getByRole('option')).not.toHaveCount(1);

    expect(await layoutShiftTotal(page)).toBeLessThan(0.01);
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
