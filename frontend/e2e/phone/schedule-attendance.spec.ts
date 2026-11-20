import type { AttendanceState } from '../../src/shared/api/types.ts';
import { formatShiftDate } from '../../src/shared/utils/thaiTime.ts';
import { recordTransferArrival } from '../support/attendance.ts';
import { desktop } from '../support/devices.ts';
import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { apiAnswer, authenticatedRead, captureSchedule, observeLogin, openSchedule, saveReviewedSchedule } from '../support/scheduling.ts';
import { schedulingFixture } from '../support/seed.ts';
import { expect, observeAdditionalContext, test } from '../support/test.ts';

interface AttendanceBoard {
  rows: {
    areaId: number;
    entry?: string;
    state: string;
    cleaner?: { id: number };
    entryEvidence?: { kind: string; recordedAt: string; transferId?: number }[];
    responsibility?: { userId: number };
  }[];
}

const breakLabels = { Break1Out: 'ออกพัก 1', Break1In: 'กลับจากพัก 1', Break2Out: 'ออกพัก 2', Break2In: 'กลับจากพัก 2' };

const attendancePath = '/api/attendance';

test.describe('scheduling attendance', () => {
  test.skip(process.env.E2E_DATASET !== 'large', 'Requires the run-owned large scheduling roster');

  test('records actual final out with partial leave and never invents break times', async ({ page, browser, baseURL }, info) => {
    const fixture = schedulingFixture();
    const context = await browser.newContext({ ...desktop, baseURL });
    await observeAdditionalContext(context);
    try {
      const manager = await context.newPage();
      await loginAsEmployee(manager, fixture.supervisorAB);
      await expect(manager).toHaveURL(/\/my-building$/);
      await openSchedule(manager, '/schedule-management', fixture.weekStart);
      const date = formatShiftDate(fixture.shiftDate);
      await manager.getByRole('button', { name: new RegExp(`^เลือกช่อง ${fixture.leave.name} .* ${date}$`) }).click();
      await manager.getByRole('button', { name: new RegExp(`^(แก้เฉพาะวันที่|แก้ไขแผนย้อนหลัง) ${fixture.leave.name} ${date}$`) }).click();
      const dialog = manager.getByRole('dialog');
      await dialog.getByLabel('รายการที่จะแก้').selectOption('leave');
      await dialog.getByLabel('เริ่มลา').fill(thaiInput(fixture.leaveStartsAt));
      await dialog.getByLabel('สิ้นสุดลา').fill(thaiInput(fixture.leaveEndsAt));
      await dialog.getByRole('button', { name: 'ตรวจสอบ', exact: true }).click();
      await saveReviewedSchedule(manager);
    } finally {
      await context.close();
    }

    const token = await observeLogin(page, () => loginAsEmployee(page, fixture.leave));
    await expect(page).toHaveURL(/\/my-work$/);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto(`/check-in/${fixture.leaveArea.checkInToken}`);
    await expect(page.getByText('มีการลาที่บันทึกไว้ในกะนี้')).toBeVisible();
    const before = await authenticatedRead<AttendanceState>(page.request, `${attendancePath}/me`, token);
    expect(before).toMatchObject({ shiftDate: fixture.shiftDate, shift: fixture.shift, isLeave: true });
    expect(before.events.map(({ eventType }) => eventType)).toEqual(['ShiftIn']);
    await expect(page.getByRole('list', { name: 'วันนี้' }).getByRole('listitem')).toHaveCount(6);

    await page.getByRole('button', { name: 'เลิกงาน', exact: true }).click();
    await expect(page.getByText(/ต้องยืนยันรายการที่ข้ามก่อน/)).toBeVisible();
    const finalOut = page.waitForResponse((response) => apiAnswer(response, attendancePath, 'POST'));
    const pressedAt = Date.now();
    await page.getByRole('button', { name: 'ยืนยันและบันทึก' }).click();
    const answer = await finalOut;
    expect(answer.ok()).toBe(true);
    expect((await answer.json() as AttendanceState).outcome).toBe('Recorded');

    const after = await authenticatedRead<AttendanceState>(page.request, `${attendancePath}/me`, token);
    expect(after.events.map(({ eventType }) => eventType)).toEqual(['ShiftIn', 'ShiftOut']);
    expect(after.events[0]).toEqual(before.events[0]);
    expect(after.nextActions).toEqual([]);
    const departure = Date.parse(after.events[1].occurredAt);
    expect(departure).toBeGreaterThanOrEqual(pressedAt - 1000);
    expect(departure).toBeLessThanOrEqual(Date.now());
    const breaks = after.actions!.filter(({ eventType }) => eventType.startsWith('Break'));
    expect(breaks).toHaveLength(4);
    for (const action of breaks) {
      expect(action.occurredAt).toBeUndefined();
      const due = Date.parse(action.expectedAt!);
      const covered = due >= departure && due >= Date.parse(fixture.leaveStartsAt) && due < Date.parse(fixture.leaveEndsAt);
      expect(action.state).toBe(covered ? 'not-applicable' : 'skipped');
      await expect(page.getByRole('listitem').filter({ has: page.getByText(breakLabels[action.eventType as keyof typeof breakLabels], { exact: true }) }))
        .toContainText(covered ? 'ไม่ต้องลงเวลาเนื่องจากลา' : 'ข้ามการสแกน');
    }
    const completed = page.getByText(/^(สิ้นสุดกะแล้ว|ลงเวลาครบแล้วสำหรับกะนี้)$/);
    await expect(completed).toBeVisible();
    await expect(page.getByRole('button', { name: 'เลิกงาน', exact: true })).toHaveCount(0);
    await expect(page.getByText('เวลาเลิกงานไม่ตรงกับช่วงลา')).toHaveCount(0);
    await captureSchedule(page, info, 'attendance', completed);
    await page.setViewportSize({ width: 320, height: 720 });
    await captureSchedule(page, info, 'attendance', completed);
  });

  test('confirms transfer arrival at B and preserves original ShiftIn and work at A', async ({ page, browser, baseURL }, info) => {
    const fixture = schedulingFixture();
    const observer = await browser.newContext({ ...desktop, baseURL });
    await observeAdditionalContext(observer);
    try {
      const admin = await observer.newPage();
      const adminToken = await observeLogin(admin, () => loginAsAdmin(admin));
      const boardPath = `/api/admin/attendance?shiftDate=${fixture.shiftDate}&shift=${fixture.shift}`;
      const boardRead = () => authenticatedRead<AttendanceBoard>(observer.request, boardPath, adminToken);
      const pointRead = () => authenticatedRead<{ areaId: number; lastScan?: { cleanerName: string; submittedAt: string } }>(
        observer.request, `/api/service-points/by-token/${fixture.homeArea.pointToken}`, adminToken);
      const area = (board: AttendanceBoard, id: number) => {
        const row = board.rows.find(({ areaId }) => areaId === id);
        expect(row, 'Area must exist on the independent board').toBeDefined();
        return row!;
      };

      const token = await observeLogin(page, () => loginAsEmployee(page, fixture.transfer));
      await expect(page).toHaveURL(/\/my-work$/);
      const mine = () => authenticatedRead<AttendanceState>(page.request, `${attendancePath}/me`, token);
      const before = await mine();
      expect(before).toMatchObject({
        shiftDate: fixture.shiftDate, shift: fixture.shift,
        transfer: { state: 'instructed', fromAreaId: fixture.homeArea.id, toAreaId: fixture.destinationArea.id },
      });
      expect(before.transfer?.confirmedAt).toBeUndefined();
      expect(before.events.map(({ eventType }) => eventType)).toEqual(['ShiftIn', 'Break1Out']);
      const pending = await boardRead();
      const oldEvidence = area(pending, fixture.homeArea.id).entryEvidence;
      expect(oldEvidence).toEqual([expect.objectContaining({ kind: 'shift-in' })]);
      const pendingDestination = area(pending, fixture.destinationArea.id);
      expect(pendingDestination.entryEvidence ?? []).toEqual([]);
      expect(pendingDestination.entry).toBe('covered');
      const work = await pointRead();
      expect(work).toMatchObject({ areaId: fixture.homeArea.id, lastScan: { cleanerName: fixture.transfer.name } });

      await page.setViewportSize({ width: 375, height: 812 });
      const result = await recordTransferArrival(page, `/check-in/${fixture.destinationArea.checkInToken}`);
      expect(result).toMatchObject({ id: before.transfer?.id, state: 'confirmed' });
      expect(Date.parse(result.confirmedAt!)).toBeGreaterThan(Date.parse(fixture.transferEffectiveAt));
      await expect(page.getByRole('button', { name: 'กลับจากพัก 1', exact: true })).toBeVisible();
      const confirmation = page.getByText('ยืนยันถึงพื้นที่ใหม่แล้ว', { exact: true });
      await captureSchedule(page, info, 'transfer', confirmation);
      await page.setViewportSize({ width: 320, height: 720 });
      await captureSchedule(page, info, 'transfer', confirmation);

      const after = await mine();
      expect(after.events).toEqual(before.events);
      expect(after.dutyAreaId).toBe(fixture.destinationArea.id);
      const confirmedAt = result.confirmedAt.replace(/\+00:00$/, 'Z');
      expect(after.transfer).toMatchObject({ state: 'confirmed', confirmedAt });
      const confirmed = await boardRead();
      expect(area(confirmed, fixture.homeArea.id).entryEvidence).toEqual(oldEvidence);
      expect(area(confirmed, fixture.destinationArea.id).entryEvidence).toEqual([
        { kind: 'transfer-arrival', transferId: result.id, recordedAt: confirmedAt },
      ]);
      expect(area(confirmed, fixture.destinationArea.id)).toMatchObject({
        entry: 'entered', state: 'OnBreak', responsibility: { userId: fixture.transfer.id },
      });
      expect(area(confirmed, fixture.homeArea.id).cleaner?.id).not.toBe(fixture.transfer.id);
      expect((await pointRead()).lastScan).toEqual(work.lastScan);
    } finally {
      await observer.close();
    }
  });
});

function thaiInput(instant: string) {
  return new Date(Date.parse(instant) + 7 * 3_600_000).toISOString().slice(0, 16);
}
