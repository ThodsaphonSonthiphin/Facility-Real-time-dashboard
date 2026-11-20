import type { Locator, Page, TestInfo } from '@playwright/test';
import { phone } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import {
  apiAnswer, captureSchedule, openSchedule, saveReviewedSchedule, scheduleCommandPath, weekDayLabel, type CommandResult, type ScheduleWeek,
} from '../support/scheduling.ts';
import { schedulingFixture } from '../support/seed.ts';
import { expect, observeAdditionalContext, test } from '../support/test.ts';

test.describe('scheduling', () => {
  test.skip(process.env.E2E_DATASET !== 'large', 'Requires the run-owned large scheduling roster');

  test('saves one weekly day off and the Cleaner reads it in their own week', async ({ page, browser, baseURL }, info) => {
    const fixture = schedulingFixture();
    await loginAsEmployee(page, fixture.supervisorAB);
    await expect(page).toHaveURL(/\/my-building$/);
    const before = await openSchedule(page, '/schedule-management', fixture.planningWeek);
    expect(before.rows.map(({ userId }) => userId)).toEqual(expect.arrayContaining([fixture.weekly.id, fixture.competing.id]));
    expect(before.rows.some(({ userId }) => userId === fixture.relief.id)).toBe(false);
    await expect(page.getByRole('rowheader', { name: new RegExp(fixture.weekly.name) })).toBeVisible();
    await captureManagement(page, info, 'schedule', page.getByRole('heading', { name: 'จัดตารางงาน' }));

    const dialog = await openWeeklyEditor(page, fixture.planningWeek, true);
    const off = dialog.getByRole('group', { name: fixture.weekly.name })
      .getByRole('checkbox', { name: weekDayLabel(fixture.planningWeek), exact: true });
    await off.check();
    await dialog.getByRole('button', { name: 'ตรวจสอบ', exact: true }).click();
    await captureManagement(page, info, 'review', dialog.getByRole('heading', { name: 'ตรวจสอบก่อนบันทึก' }));
    await dialog.getByRole('button', { name: 'กลับไปแก้ไข' }).click();
    await expect(off).toBeChecked();
    await dialog.getByRole('button', { name: 'ตรวจสอบ', exact: true }).click();
    await saveReviewedSchedule(page);
    await expect(dialog.getByText('ยังมีแถวที่จัดไม่ครบ กรุณาตรวจในตารางรายสัปดาห์')).toBeVisible();

    const context = await browser.newContext({ ...phone, baseURL, viewport: { width: 375, height: 812 } });
    await observeAdditionalContext(context);
    try {
      const cleaner = await context.newPage();
      await loginAsEmployee(cleaner, fixture.weekly);
      await expect(cleaner).toHaveURL(/\/my-work$/);
      const mine = await openSchedule(cleaner, '/my-schedule', fixture.planningWeek);
      expect(mine.rows.map((person) => person.userId)).toEqual([fixture.weekly.id]);
      expect(mine.rows[0].days.filter((day) => day.isDayOff).map((day) => day.date)).toEqual([fixture.planningWeek]);
      await expect(cleaner.getByRole('article').filter({ hasText: weekDayLabel(fixture.planningWeek) })
        .getByText('วันหยุด', { exact: true })).toBeVisible();
      await captureSchedule(cleaner, info, 'own-week');
      await cleaner.setViewportSize({ width: 320, height: 720 });
      await captureSchedule(cleaner, info, 'own-week');
    } finally {
      await context.close();
    }
  });

  test('rejects a stale competing editor and keeps the rejected draft visible', async ({ page, browser, baseURL }, info) => {
    const fixture = schedulingFixture();
    await loginAsEmployee(page, fixture.supervisorAB);
    await expect(page).toHaveURL(/\/my-building$/);
    const first = await openSchedule(page, '/schedule-management', fixture.planningWeek);
    const context = await browser.newContext({ baseURL, viewport: { width: 1280, height: 800 } });
    await observeAdditionalContext(context);
    try {
      const other = await context.newPage();
      await loginAsAdmin(other);
      await openSchedule(other, '/admin/schedules', fixture.planningWeek);
      const shift = other.getByRole('group', { name: 'กะ', exact: true }).getByRole('button', {
        name: fixture.shift === 'Day' ? 'กะเช้า' : 'กะดึก', exact: true,
      });
      const alreadySelected = await shift.getAttribute('aria-pressed') === 'true';
      const selectedRead = other.waitForResponse((response) => {
        const params = new URL(response.url()).searchParams;
        return apiAnswer(response, '/api/schedules') && params.get('weekStart') === fixture.planningWeek
          && params.get('shift') === fixture.shift;
      });
      await shift.click();
      if (alreadySelected) {
        await other.getByRole('button', { name: 'อัปเดต', exact: true }).click();
      }
      const selected = await selectedRead;
      expect(selected.ok()).toBe(true);
      await expect(shift).toHaveAttribute('aria-pressed', 'true');
      const second = await selected.json() as ScheduleWeek;
      const revision = (week: typeof first) => week.rows.find(({ userId }) => userId === fixture.competing.id)?.weeklyRevision;
      expect(revision(first)).toBe('0');
      expect(revision(second)).toBe(revision(first));

      const winnerDate = fixture.planningWeek;
      const loserDate = new Date(Date.parse(winnerDate) + 86_400_000).toISOString().slice(0, 10);
      await draftOff(page, fixture.competing.name, fixture.planningWeek, winnerDate);
      await draftOff(other, fixture.competing.name, fixture.planningWeek, loserDate);

      // Let the browser send its real stale payload only after the other transaction is committed.
      let release!: () => void;
      const commitFirst = new Promise<void>((resolve) => { release = resolve; });
      await other.route(`**${scheduleCommandPath}`, async (route) => {
        await commitFirst;
        await route.continue();
      });
      const held = other.waitForRequest((request) => new URL(request.url()).pathname === scheduleCommandPath
        && request.method() === 'POST');
      const refused = other.waitForResponse((response) => apiAnswer(response, scheduleCommandPath, 'POST'));
      try {
        await other.getByRole('button', { name: 'บันทึก', exact: true }).click();
        await held;
        await saveReviewedSchedule(page);
      } finally {
        release();
      }
      const answer = await refused;
      expect(answer.status()).toBe(409);
      const rejected = await answer.json() as CommandResult;
      expect(rejected.outcome).toBe('rejected');
      expect(rejected.problems).toEqual([expect.objectContaining({ code: 'stale-revision' })]);
      await expect(other.getByText('ตารางเปลี่ยนระหว่างแก้ไข', { exact: true })).toBeVisible();
      await other.getByRole('button', { name: 'โหลดเพื่อเปรียบเทียบ' }).click();
      await expect(other.getByText(`ล่าสุด · ${weekDayLabel(winnerDate)}/${winnerDate.slice(0, 4)}`)).toBeVisible();
      await expect(other.getByText(`ร่าง · ${weekDayLabel(loserDate)}/${loserDate.slice(0, 4)}`)).toBeVisible();
      await captureManagement(other, info, 'conflict', other.getByText('ตารางเปลี่ยนระหว่างแก้ไข', { exact: true }));
      await other.getByRole('button', { name: 'ใช้ข้อมูลล่าสุดและเก็บร่าง' }).click();
      await expect(other.getByRole('group', { name: fixture.competing.name })
        .getByRole('checkbox', { name: weekDayLabel(loserDate), exact: true })).toBeChecked();

      await page.getByRole('button', { name: 'เสร็จสิ้น', exact: true }).click();
      const fresh = await openSchedule(page, '/schedule-management', fixture.planningWeek);
      const actual = fresh.rows.find(({ userId }) => userId === fixture.competing.id);
      expect(actual?.days.filter((day) => day.edit?.weeklyOff).map((day) => day.date)).toEqual([winnerDate]);
      expect(actual?.weeklyRevision).toBe('1');
    } finally {
      await context.close();
    }
  });

  test('records a cross-Supervisor relief loan after the destination attests to the source agreement', async ({ page }) => {
    const fixture = schedulingFixture();
    await loginAsEmployee(page, fixture.supervisorAB);
    await expect(page).toHaveURL(/\/my-building$/);
    const before = await openSchedule(page, '/schedule-management', fixture.planningWeek);
    expect(before.relief.some(({ userId }) => userId === fixture.relief.id)).toBe(false);
    await page.getByRole('button', { name: 'จัดการเพิ่มเติม', exact: true }).click();
    await page.getByRole('menuitem', { name: 'บันทึกการยืมสแปร์', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'บันทึกการยืมสแปร์' });
    await dialog.getByLabel('วันที่ทำงาน').selectOption(fixture.planningWeek);
    await dialog.getByLabel('พื้นที่ปลายทาง').selectOption(String(fixture.destinationArea.id));
    await dialog.getByLabel('ตึกต้นทาง').selectOption(String(fixture.buildingC.id));
    await dialog.getByLabel('รหัสพนักงานสแปร์').fill(fixture.relief.employeeId);
    const candidate = page.waitForResponse((response) => apiAnswer(response, '/api/schedules/relief-candidates'));
    await dialog.getByRole('button', { name: 'ค้นหาสแปร์' }).click();
    expect((await candidate).ok()).toBe(true);
    await expect(dialog.getByText(fixture.relief.name, { exact: true })).toBeVisible();
    await expect(dialog.getByText(`ซุปตึกต้นทาง · ${fixture.supervisorC.name}`)).toBeVisible();
    await dialog.getByLabel('กลุ่มพักครั้งที่ 1').selectOption('2');
    const review = dialog.getByRole('button', { name: 'ตรวจทาน' });
    await expect(review).toBeDisabled();
    await dialog.getByLabel('ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว').selectOption('yes');
    await review.click();
    await saveReviewedSchedule(page);

    await dialog.getByRole('button', { name: 'เสร็จสิ้น', exact: true }).click();
    const after = await openSchedule(page, '/schedule-management', fixture.planningWeek);
    const duty = after.relief.find(({ userId }) => userId === fixture.relief.id);
    expect(duty).toMatchObject({
      shiftDate: fixture.planningWeek, shift: fixture.shift, breakGroup: 2,
      sourceBuildingId: fixture.buildingC.id, sourceSupervisorId: fixture.supervisorC.id,
      destinationBuildingId: fixture.buildingB.id, areaId: fixture.destinationArea.id,
      agreementReported: true, isCancelled: false,
    });
    await expect(page.getByText('ผู้บันทึกยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว')).toBeVisible();
    await expect(page.getByRole('button', { name: /อนุมัติ/ })).toHaveCount(0);
  });
});

async function openWeeklyEditor(page: Page, weekStart: string, keyboard = false): Promise<Locator> {
  const previousWeek = new Date(Date.parse(weekStart) - 7 * 86_400_000).toISOString().slice(0, 10);
  const source = page.waitForResponse((response) => apiAnswer(response, '/api/schedules')
    && new URL(response.url()).searchParams.get('weekStart') === previousWeek);
  const entry = page.getByRole('button', { name: 'จัดวันหยุดรายสัปดาห์', exact: true });
  if (keyboard) {
    await entry.focus();
    await page.keyboard.press('Enter');
  } else {
    await entry.click();
  }
  expect((await source).ok()).toBe(true);
  const dialog = page.getByRole('dialog', { name: 'จัดวันหยุดรายสัปดาห์' });
  // The source loading text above the rows changes their position as it disappears.
  await expect(dialog.getByRole('button', { name: 'คัดลอกลงช่องที่ยังไม่กำหนด' })).toBeEnabled();
  return dialog;
}

async function draftOff(page: Page, name: string, weekStart: string, date: string): Promise<void> {
  const dialog = await openWeeklyEditor(page, weekStart);
  const choice = dialog.getByRole('group', { name }).getByRole('checkbox', { name: weekDayLabel(date), exact: true });
  await choice.check();
  await dialog.getByRole('button', { name: 'ตรวจสอบ', exact: true }).click();
}

async function captureManagement(page: Page, info: TestInfo, name: string, state: Locator) {
  const desktop = page.viewportSize()!;
  for (const viewport of [desktop, { width: 375, height: 812 }, { width: 320, height: 720 }]) {
    await page.setViewportSize(viewport);
    await expect(state).toBeVisible();
    await captureSchedule(page, info, name);
  }
  await page.setViewportSize(desktop);
}
