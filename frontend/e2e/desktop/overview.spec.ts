import type { Page } from '@playwright/test';
import { loginAsAdmin } from '../support/login.ts';
import { holdUntilReleased, sidewaysOverflow } from '../support/pageWatch.ts';
import { overviewFixture } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test.skip(process.env.E2E_DATASET !== 'medium', 'Requires the medium synthetic Night-shift fixture');

function overviewUrl(group = 'entered', date = overviewFixture().date) {
  const { shift, buildingId } = overviewFixture();
  return `/admin/overview?date=${date}&shift=${shift}&building=${buildingId}&group=${group}&page=1`;
}

function resultLinks(page: Page) {
  return page.getByRole('link', { name: /^ดูพื้นที่ / });
}

test('the default Overview read does not pull the Admin back from an open navigation', async ({ page }) => {
  const overviewApi = '**/api/admin/overview/areas?*';
  const accountsChunk = /\/assets\/accounts-[^/]+\.js$/;
  const releaseOverview = await holdUntilReleased(page, overviewApi);
  const releaseAccountsPage = await holdUntilReleased(page, accountsChunk);
  try {
    await loginAsAdmin(page);
    const accountsRequested = page.waitForRequest(accountsChunk);
    await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'บัญชีผู้ใช้' }).click();
    await accountsRequested;
    const defaultRead = page.waitForResponse(overviewApi);
    releaseOverview();
    expect((await defaultRead).ok()).toBe(true);
    await expect(page.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
    await expect(page).toHaveURL(/\/admin\/overview$/);
    releaseAccountsPage();
    await expect(page).toHaveURL(/\/admin\/accounts$/);
  } finally {
    releaseOverview();
    releaseAccountsPage();
  }
});

test('overview fits a narrow screen and respects the system motion preference', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 812 });
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await loginAsAdmin(page);
  await page.goto(overviewUrl());

  const overview = page.getByRole('main');
  await expect(overview.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
  await expect(overview).toHaveCSS('animation-name', 'motion-view-in');
  await expect(overview).toHaveCSS('opacity', '1');
  expect(await sidewaysOverflow(page)).toBe(0);

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(overview).toHaveCSS('animation-name', 'none');
  await expect(overview).toHaveCSS('transform', 'none');
  await expect(overview.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
});

for (const [width, height] of [[1280, 900], [375, 812]]) {
  test(`overview Night attendance keeps all five page-two Areas through detail and refresh at ${width}px`, async ({ page }, info) => {
    const fixture = overviewFixture();
    await page.setViewportSize({ width, height });
    await loginAsAdmin(page);
    await page.goto(overviewUrl());
    const entered = page.getByRole('button', { name: `ลงเวลาแล้ว ${fixture.entered} พื้นที่`, exact: true });
    await expect(entered).toBeVisible();
    await expect(page.getByRole('button', { name: `ส่งงานครบ ${fixture.submitted} พื้นที่`, exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: `ตรวจครบแล้ว ${fixture.inspected} พื้นที่`, exact: true })).toBeVisible();
    await expect(page.getByLabel('วันที่ของกะ')).toHaveValue(fixture.date);
    await expect(page.getByRole('combobox', { name: 'ตึก' })).toHaveValue(String(fixture.buildingId));
    await info.attach('overview-layout', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
    expect(await sidewaysOverflow(page)).toBe(0);
    const next = page.getByRole('button', { name: 'ถัดไป', exact: true });
    await next.focus();
    await expect(next).toBeFocused();
    await next.press('Enter');
    const pageTwo = fixture.enteredAreas.slice(5, 10);
    expect(pageTwo).toHaveLength(5);
    const status = page.getByRole('status', { name: 'สถานะภาพรวม' });
    await expect(status).toContainText('แสดง 6–10');
    for (const { id, code } of pageTwo) {
      const link = resultLinks(page).filter({ visible: true }).and(page.getByRole('link', { name: new RegExp(code) }));
      await expect(link).toHaveAttribute('href', new RegExp(`areaId=${id}(?:&|$)`));
      const source = page.url();
      await link.click();
      await expect(page.getByRole('heading', { level: 1 })).toContainText(code);
      const destination = page.url();
      const params = new URL(destination).searchParams;
      expect(Object.fromEntries(params)).toMatchObject({
        date: fixture.date, shift: 'Night', building: String(fixture.buildingId), group: 'entered',
        page: '2', areaId: String(id), source: 'overview', view: 'attendance',
      });
      await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
      await expect(page.getByText('ดูย้อนหลังเท่านั้น')).toBeVisible();
      if (id === pageTwo[0].id) {
        await page.reload();
        await expect(page).toHaveURL(destination);
        await expect(page.getByRole('heading', { level: 1 })).toContainText(code);
        expect(await sidewaysOverflow(page)).toBe(0);
      }
      await page.getByRole('link', { name: 'กลับภาพรวม' }).click();
      await expect(page).toHaveURL(source);
      await expect(status).toContainText('แสดง 6–10');
    }
  });
}

test('overview opens cleaning, inspection, Issue and the exact historical round read-only', async ({ page }) => {
  await loginAsAdmin(page);
  await page.goto(overviewUrl());
  const cases = [
    ['ส่งงานครบ', 'submitted', 'งานทำความสะอาด'], ['ตรวจครบแล้ว', 'inspected', 'ผลการตรวจ'], ['แม่บ้านแจ้งปัญหา', 'issue', 'ปัญหาที่แจ้ง'],
    ['มีรอบไม่ได้ทำ', 'missedCleaning', 'รอบที่เลือก'],
  ] as const;
  for (const [group, groupKey, title] of cases) {
    await page.getByRole('button', { name: new RegExp(`^${group} [1-9]\\d* พื้นที่$`) }).click();
    const link = title === 'รอบที่เลือก'
      ? page.locator('#overview-results-heading').locator('..').locator('..').locator('a[href*="roundKey="]').first()
      : resultLinks(page).first();
    await expect(link).toHaveAttribute('href', new RegExp(`group=${groupKey}(?:&|$)`));
    const href = await link.getAttribute('href');
    expect(href).toBeTruthy();
    await link.click();
    await expect(page).toHaveURL(new URL(href!, page.url()).href);
    await expect(page.getByRole('heading', { name: new RegExp(`^${title}`) }).first()).toBeVisible();
    if (title === 'รอบที่เลือก') {
      const key = new URL(page.url()).searchParams.get('roundKey');
      expect(key).toBeTruthy();
      await expect(page.locator('a[aria-current="true"]')).toHaveAttribute('href', href!);
    }
    await expect(page.getByText('ดูย้อนหลังเท่านั้น')).toBeVisible();
    await expect(page.getByRole('button', { name: /บันทึก|แก้ไข/ })).toHaveCount(0);
    await page.getByRole('link', { name: 'กลับภาพรวม' }).click();
  }
});

test('overview retries an aborted read and distinguishes complete empty from unknown history', async ({ page }) => {
  await loginAsAdmin(page);
  const { emptyDate, preBaselineDate } = overviewFixture();
  await page.route('**/api/admin/overview/areas?*', route => route.abort(), { times: 1 });
  await page.goto(overviewUrl('entered', emptyDate));
  await page.getByRole('button', { name: 'ลองอีกครั้ง' }).click();
  await expect(page.getByText('ไม่มีพื้นที่ในกลุ่มนี้', { exact: true })).toBeVisible();
  await page.getByLabel('วันที่ของกะ').fill(preBaselineDate);
  await expect(page.getByText('ไม่พบรายการ (ยอดรวม: —)', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'ลงเวลาแล้ว — พื้นที่', exact: true })).toBeVisible();
});

test('overview keeps the second filter when the first real request finishes later', async ({ page }) => {
  await loginAsAdmin(page);
  const { date, emptyDate, firstDate } = overviewFixture();
  await page.goto(overviewUrl('entered', date));
  await expect(resultLinks(page).first()).toBeVisible();
  let entered!: () => void;
  let release!: () => void;
  const started = new Promise<void>(resolve => { entered = resolve; });
  const allowed = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/admin/overview/areas?*', async route => {
    if (route.request().url().includes(firstDate)) {
      entered();
      await allowed;
    }
    await route.fallback();
  });
  const dateInput = page.getByLabel('วันที่ของกะ');
  try {
    await dateInput.fill(firstDate);
    await started;
    await expect(page.getByRole('heading', { name: 'สรุปผลของกะ' })).toBeVisible();
    await expect(page.getByText('กำลังโหลดภาพรวม...')).toBeHidden();
    await expect(page.getByText('กำลังอัปเดต...', { exact: true })).toBeVisible();
    await expect(page.locator('[aria-busy="true"]')).toContainText(overviewFixture().enteredAreas[0].code);
    await dateInput.fill(emptyDate);
    await expect(page.getByText('ไม่มีพื้นที่ในกลุ่มนี้', { exact: true })).toBeVisible();
    const settled = page.waitForEvent('requestfinished', req => req.url().includes(firstDate) && req.url().includes('/api/admin/overview/areas'));
    release();
    await settled;
    await expect(dateInput).toHaveValue(emptyDate);
    await expect(page.getByText('ไม่มีพื้นที่ในกลุ่มนี้', { exact: true })).toBeVisible();
  } finally {
    release();
  }
});
