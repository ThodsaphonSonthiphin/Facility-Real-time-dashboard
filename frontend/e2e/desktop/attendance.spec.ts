import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import { test, expect } from '../support/test.ts';
import { loginAsAdmin } from '../support/login.ts';
import { holdBack } from '../support/pageWatch.ts';
import { engine } from '../support/browser.ts';

test.beforeEach(async ({ page }) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated assignments and attendance');
  await loginAsAdmin(page);
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'การเข้างาน', exact: true }).click();
});

const dayButton = (page: Page) => page.getByRole('group', { name: 'กะ', exact: true }).getByRole('button', { name: /^กะเช้า/ });

/**
 * Presses กะเช้า and waits until the page shows it, as a person would. Before the BoardControls fix (2026-11-02) a date
 * picked before the Day board arrived asked for the old shift; this test failed at night because of it.
 */
async function showDayShift(page: Page): Promise<void> {
  await dayButton(page).click();
  await expect(dayButton(page)).toHaveAttribute('aria-pressed', 'true');
}

test('attendance client pagination has distinct rows, returns to page one and resets after search @dataset', async ({ page }) => {
  test.skip(process.env.E2E_DATASET === 'small', 'Small profile has fewer than25 Areas; medium and large exercise pagination');
  await showDayShift(page);
  const table = page.getByRole('table', { name: 'การเข้างานรายพื้นที่' });
  await expect(table.getByRole('rowheader')).toHaveCount(25);
  const first = await table.getByRole('rowheader').allTextContents();
  await page.getByRole('button', { name: 'ถัดไป', exact: true }).click();
  await expect(page.getByText(/^แสดง 26–50 จาก/)).toBeVisible();
  const second = await table.getByRole('rowheader').allTextContents();
  expect(new Set([...first, ...second]).size).toBe(50);
  await page.getByRole('button', { name: 'ก่อนหน้า', exact: true }).click();
  await expect(table.getByRole('rowheader')).toHaveText(first);
  await page.getByRole('button', { name: 'ถัดไป', exact: true }).click();
  await page.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill('E2EA0001');
  await expect(table.getByRole('rowheader')).toHaveCount(1);
  await expect(page.getByRole('button', { name: 'ก่อนหน้า', exact: true })).toBeDisabled();
  await expect(table).toContainText('E2EA0001');
});

test('attendance correction requires a reason and persists its time and audit trail @dataset', async ({ page }) => {
  const dataset = JSON.parse(readFileSync(`${process.env.E2E_RUN_DIR}/dataset.json`, 'utf8')) as { anchorDate: string };
  const yesterday = new Date(`${dataset.anchorDate}T00:00:00Z`);
  yesterday.setUTCDate(yesterday.getUTCDate() - 1);
  const date = yesterday.toISOString().slice(0, 10);
  await showDayShift(page);
  await page.getByLabel('วันที่', { exact: true }).fill(date);
  await expect(page.getByLabel('วันที่', { exact: true })).toHaveValue(date);
  await expect(dayButton(page)).toHaveAttribute('aria-pressed', 'true');
  await page.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill('E2EA0001');
  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: /^E2EA0001 / }) });
  await expect(row).toContainText('07:00');
  await row.getByRole('button', { name: /^แก้เวลา/ }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('เวลาใหม่ เข้างาน', { exact: true }).fill('07:05');
  await dialog.getByRole('button', { name: /^บันทึกการแก้ไข/ }).click();
  await expect(dialog.getByRole('alert')).toBeVisible();
  await dialog.getByText('ลืมลงเวลา', { exact: true }).click();
  await dialog.getByLabel(/รายละเอียดเพิ่มเติม/).fill('ข้อมูลทดสอบ E2E-CORRECTION');
  await dialog.getByRole('button', { name: /^บันทึกการแก้ไข/ }).click();
  await expect(dialog).toBeHidden();
  await expect(row).toContainText('07:05');
  await page.reload();
  await page.getByLabel('ค้นหาพื้นที่หรือแม่บ้าน', { exact: true }).fill('E2EA0001');
  await expect(row).toContainText('07:05');
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'ประวัติการแก้ไข', exact: true }).click();
  await page.getByText('ค้นหาประวัติ', { exact: true }).click();
  await page.getByLabel('ค้นหา', { exact: true }).fill('E2E-CORRECTION');
  await page.getByRole('button', { name: 'ค้นหา', exact: true }).click();
  await expect(page.getByRole('table')).toContainText('E2E-CORRECTION');
  await expect(page.getByRole('table')).toContainText('07:05');
});

test('a date picked while another shift is still loading keeps that shift', async ({ page }) => {
  await holdBack(page, '**/api/admin/attendance*', 1_500); // a slow network: the old board stays on screen meanwhile
  const pressed = await dayButton(page).getAttribute('aria-pressed');
  const [pick, other] = pressed === 'true' ? (['Night', /^กะดึก/] as const) : (['Day', /^กะเช้า/] as const);
  const otherButton = page.getByRole('group', { name: 'กะ', exact: true }).getByRole('button', { name: other });
  await otherButton.click();
  await expect(page).toHaveURL(new RegExp(`shift=${pick}`));
  await expect(otherButton).toHaveAttribute('aria-pressed', 'false'); // still loading
  await page.getByLabel('วันที่', { exact: true }).fill('2026-10-01');
  await expect(page).toHaveURL(new RegExp(`date=2026-10-01&shift=${pick}$`));
  await expect(otherButton).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByLabel('วันที่', { exact: true })).toHaveValue('2026-10-01');
});

test('cover assignment and nested cancellation preserve the parent drawer @dataset', async ({ page }) => {
  await page.getByRole('button', { name: 'มอบหมายทำแทน', exact: true }).click();
  const drawer = page.getByRole('dialog');
  await drawer.getByLabel('ค้นหาพื้นที่หรือแม่บ้านประจำ', { exact: true }).fill('E2EA0002');
  await drawer.getByRole('radiogroup', { name: '2. พื้นที่ที่ต้องมีคนทำแทน' }).getByText('E2EA0002', { exact: true }).click();
  await expect(drawer.getByRole('button', { name: 'มอบหมาย', exact: true })).toBeEnabled();
  await drawer.getByRole('button', { name: 'มอบหมาย', exact: true }).click();
  await expect(drawer).toBeHidden();
  await page.getByRole('button', { name: 'มอบหมายทำแทน', exact: true }).click();
  const cancel = drawer.getByRole('button', { name: /ยกเลิกการทำแทนของ .* ที่ E2EA0002/ });
  await expect(cancel).toBeVisible();
  // A mouse click does not focus a button on Apple platforms (WebKit), so there the keyboard opens it: focus going back
  // to the button is for keyboard users
  if (engine === 'webkit') {
    await cancel.focus();
    await page.keyboard.press('Enter');
  } else {
    await cancel.click();
  }
  const confirmation = page.getByRole('alertdialog');
  await page.keyboard.press('Escape');
  await expect(confirmation).toBeHidden();
  await expect(drawer).toBeVisible();
  await expect(cancel).toBeFocused();
  await cancel.click();
  await confirmation.getByRole('button', { name: 'ยกเลิกการทำแทน', exact: true }).click();
  await expect(confirmation).toBeHidden();
  await expect(cancel).toHaveCount(0);
  await drawer.getByRole('button', { name: 'ยกเลิก', exact: true }).click();
  await expect(drawer).toBeHidden();
});
