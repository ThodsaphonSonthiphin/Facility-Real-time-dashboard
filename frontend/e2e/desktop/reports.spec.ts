import { readFileSync } from 'node:fs';
import { test, expect } from '../support/test.ts';
import { loginAsAdmin } from '../support/login.ts';
import type { Page } from '@playwright/test';

function dataset() {
  return JSON.parse(readFileSync(`${process.env.E2E_RUN_DIR}/dataset.json`, 'utf8')) as {
    anchorDate: string; summaryFromDate: string; summaryToDate: string;
    expectedSummary: { employeeId: string; inspections: number; reworks: number; gpsFlagged: number }[];
  };
}

async function enterDatasetDates(page: Page, summary = false) {
  const { anchorDate, summaryFromDate, summaryToDate } = dataset();
  const start = new Date(`${anchorDate}T00:00:00Z`);
  start.setUTCDate(start.getUTCDate() - 14);
  const end = new Date(`${anchorDate}T00:00:00Z`);
  end.setUTCDate(end.getUTCDate() - 1);
  const text = (iso: string) => iso.split('-').reverse().join('/');
  await page.getByLabel('ตั้งแต่', { exact: true }).fill(text(summary ? summaryFromDate : start.toISOString().slice(0, 10)));
  await page.getByLabel('ถึง', { exact: true }).fill(text(summary ? summaryToDate : end.toISOString().slice(0, 10)));
}

async function historyRange(page: Page) {
  await page.getByRole('combobox', { name: 'ช่วงวัน', exact: true }).selectOption('custom');
  await enterDatasetDates(page);
  await page.getByRole('button', { name: 'แสดง', exact: true }).click();
}

test.beforeEach(async ({ page }) => { await loginAsAdmin(page); });

test('history cursor paging: distinct descending records, previous page, search and empty results @dataset', async ({ page }) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated history');
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'ประวัติการแก้ไข', exact: true }).click();
  await historyRange(page);
  await page.getByText('ค้นหาประวัติ', { exact: true }).click();
  await page.getByLabel('ค้นหา', { exact: true }).fill('E2E-HISTORY');
  const answer = page.waitForResponse((r) => r.url().includes('/api/admin/audit-log?') && r.url().includes('q=E2E-HISTORY'));
  await page.getByRole('button', { name: 'ค้นหา', exact: true }).click();
  const first = await (await answer).json() as { entries: { id: number; summary: string }[] };
  const table = page.getByRole('table', { name: 'ประวัติการแก้ไขของแอดมิน' });
  await expect(table.getByRole('row')).toHaveCount(first.entries.length + 1);
  expect(first.entries.length).toBe(100);
  const firstTexts = await table.getByRole('row').allTextContents();
    const next = page.waitForResponse((r) => r.url().includes('/api/admin/audit-log?') && r.url().includes('beforeId='));
    await page.getByRole('button', { name: 'ถัดไป', exact: true }).click();
    const second = await (await next).json() as typeof first;
    await expect(table.getByRole('row')).toHaveCount(second.entries.length + 1);
    expect(second.entries.length).toBeGreaterThan(0);
    const ids = [...first.entries, ...second.entries].map((entry) => entry.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids).toEqual([...ids].sort((a, b) => b - a));
    await expect(page.getByText(new RegExp(`^แสดง ${first.entries.length + 1}–${ids.length} รายการ`))).toBeVisible();
    await page.getByRole('button', { name: 'ก่อนหน้า', exact: true }).click();
    await expect(table.getByRole('row')).toHaveText(firstTexts);
  await page.getByLabel('ค้นหา', { exact: true }).fill('E2E-HISTORY-NONEXISTENT');
  await page.getByRole('button', { name: 'ค้นหา', exact: true }).click();
  await expect(page.getByText('ไม่พบรายการที่ตรงกับที่ค้นหาในช่วงนี้')).toBeVisible();
  await expect(page.getByRole('button', { name: 'ถัดไป', exact: true })).toBeDisabled();
});

test('history category filters and literal search do not treat percent as a wildcard @dataset', async ({ page }) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated history');
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'ประวัติการแก้ไข', exact: true }).click();
  await historyRange(page);
  await page.getByRole('combobox', { name: 'ประเภท', exact: true }).selectOption('Setup');
  await expect(page.getByRole('table')).toBeVisible();
  await page.getByText('ค้นหาประวัติ', { exact: true }).click();
  await page.getByLabel('ค้นหา', { exact: true }).fill('%');
  await page.getByRole('button', { name: 'ค้นหา', exact: true }).click();
  await expect(page.getByText('ไม่พบรายการที่ตรงกับที่ค้นหาในช่วงนี้')).toBeVisible();
});

test('inspection summary displays API counts, filters by shift, and expands real details @dataset', async ({ page }) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated inspections');
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'สรุปการตรวจ', exact: true }).click();
  await page.getByRole('combobox', { name: 'ช่วงวัน', exact: true }).selectOption('custom');
  await enterDatasetDates(page, true);
  const response = page.waitForResponse((r) => r.url().includes('/api/admin/inspection-summary?') && r.url().includes('from='));
  await page.getByRole('button', { name: 'แสดง', exact: true }).click();
  const summary = await (await response).json() as { rows: { employeeId: string; displayName: string; inspections: number; notInspected: number; reworks: number; gpsFlagged: number; shift: string }[] };
  for (const truth of dataset().expectedSummary) {
    const actual = summary.rows.find((row) => row.employeeId === truth.employeeId);
    expect(actual).toMatchObject(truth);
  }
  const expected = summary.rows.find((row) => row.displayName === 'หัวหน้าทดสอบ 0000')!;
  expect(expected.inspections).toBeGreaterThan(0);
  const row = page.getByRole('row').filter({ has: page.getByRole('rowheader', { name: expected.displayName, exact: true }) });
  await expect(row.getByRole('cell').nth(1)).toHaveText(String(expected.inspections));
  await expect(row.getByRole('cell').nth(2)).toHaveText(String(expected.notInspected));
  await expect(row.getByRole('cell').nth(3)).toHaveText(String(expected.reworks));
  await expect(row.getByRole('cell').nth(4)).toHaveText(String(expected.gpsFlagged));
  const details = row.getByRole('button');
  await details.click();
  await expect(details).toHaveAttribute('aria-expanded', 'true');
  const detailId = await details.getAttribute('aria-controls');
  await expect(page.locator(`[id="${detailId}"]`)).toBeVisible();
  await details.click();
  await expect(details).toHaveAttribute('aria-expanded', 'false');
  await page.getByRole('combobox', { name: 'กะ', exact: true }).selectOption('Night');
  await expect(row).toHaveCount(0);
  await expect(page.getByRole('table').getByRole('rowheader')).toHaveCount(summary.rows.filter((r) => r.shift === 'Night').length);
});

test('Excel download requires acknowledgement and produces a real XLSX file @dataset', async ({ page }, testInfo) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires historical export data');
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'ส่งออก Excel', exact: true }).click();
  await enterDatasetDates(page);
  const downloadButton = page.getByRole('button', { name: /ดาวน์โหลด Excel/ });
  await expect(downloadButton).toBeDisabled();
  await page.getByRole('checkbox', { name: /เข้าใจแล้ว/ }).check();
  await expect(downloadButton).toBeEnabled();
  const download = page.waitForEvent('download');
  await downloadButton.click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/\.xlsx$/);
  const path = testInfo.outputPath(file.suggestedFilename());
  await file.saveAs(path);
  const bytes = readFileSync(path);
  expect(bytes.subarray(0, 2).toString()).toBe('PK');
  expect(bytes.length).toBeGreaterThan(1000);
  await expect(page.getByRole('status').filter({ hasText: 'ดาวน์โหลดแล้ว' })).toBeVisible();
  await testInfo.attach('export-workbook', { path, contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
});
