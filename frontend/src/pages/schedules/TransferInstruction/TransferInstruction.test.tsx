import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { AttendanceState, AttendanceTransfer } from '../../../shared/api/types';
import { editorApp, fixturePerson, fixtureWeek } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T06:00:00Z'));
});
afterEach(() => vi.useRealTimers());

const checkedIn: AttendanceState = {
  shiftDate: '2026-11-17', shift: 'Day', dutyAreaId: 4, events: [
    { eventType: 'ShiftIn', occurredAt: '2026-11-17T00:03:00Z', source: 'Scan', withinRadius: true },
    { eventType: 'Break1Out', occurredAt: '2026-11-17T04:30:00Z', source: 'Scan', withinRadius: true },
  ],
};
const confirmed: AttendanceTransfer = {
  id: 18, fromAreaId: 4, toAreaId: 5, effectiveAt: '2026-11-17T04:00:00Z',
  state: 'confirmed', confirmedAt: '2026-11-17T04:05:00Z', revision: '2',
};

const openTransfer = async (user: Awaited<ReturnType<typeof editorApp>>['user']) => {
  await user.click(await screen.findByRole('button', { name: /ย้ายพื้นที่ระหว่างกะ.*17 พ\.ย\./ }));
  return within(await screen.findByRole('dialog', { name: 'ย้ายพื้นที่ระหว่างกะ' }));
};

function weekWithDestinations() {
  const week = fixtureWeek();
  week.destinations = [
    { date: '2026-11-17', shift: 'Day', buildingId: 1, buildingCode: 'A', areas: week.rows[0].days[1].edit!.areas },
  ];
  return week;
}

it('reviews a dated move for the scoped person and Area, with a separate Thai effective time', async () => {
  const week = weekWithDestinations();
  week.destinations?.push(
    { date: '2026-11-17', shift: 'Night', buildingId: 2, buildingCode: 'B', areas: [{ id: 8, label: 'AR08' }] },
    { date: '2026-11-16', shift: 'Day', buildingId: 2, buildingCode: 'B', areas: [{ id: 9, label: 'AR09' }] },
  );
  const app = await editorApp({
    week: () => week,
    routes: { 'GET /api/team-attendance/7': () => ({ status: 200, body: checkedIn }) },
  });
  const drawer = await openTransfer(app.user);
  await app.user.selectOptions(await drawer.findByLabelText('พื้นที่ปลายทาง'), '5');
  expect(drawer.queryByRole('option', { name: /AR04|AR08|AR09/ })).not.toBeInTheDocument();
  fireEvent.change(drawer.getByLabelText('เวลาที่มีผล'), { target: { value: '2026-11-17T13:30' } });
  const review = drawer.getByRole('button', { name: 'ตรวจสอบ' });
  expect(review).toBeDisabled();
  await app.user.type(drawer.getByLabelText('เหตุผลที่ย้ายพื้นที่'), 'ช่วยงานพื้นที่ใหม่');
  await app.user.click(review);
  expect(drawer.getByText(/หลัง · .*AR05.*13:30/)).toBeVisible();
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));

  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.find(({ path }) => path === '/api/schedules/commands')?.body).toMatchObject({
    kind: 'instruct-transfer', expectedRevisions: { 'transfer:7:2026-11-17:Day': '0' },
    payload: {
      userId: 7, shiftDate: '2026-11-17', shift: 'Day', fromAreaId: 4, toAreaId: 5,
      effectiveAt: '2026-11-17T06:30:00.000Z', reason: 'ช่วยงานพื้นที่ใหม่',
    },
  });
});

it('keeps confirmed arrivals as read-only history and cancels only the active instruction', async () => {
  const pending: AttendanceTransfer = {
    id: 19, fromAreaId: 5, toAreaId: 4, effectiveAt: '2026-11-17T07:00:00Z',
    state: 'instructed', revision: '1',
  };
  const app = await editorApp({
    week: weekWithDestinations,
    routes: { 'GET /api/team-attendance/7': () => ({
      status: 200, body: { ...checkedIn, dutyAreaId: 5, transfer: pending, transferHistory: [confirmed] },
    }) },
  });
  const drawer = await openTransfer(app.user);
  expect(await drawer.findByText('รอเข้าพื้นที่ใหม่')).toBeVisible();
  expect(drawer.getByText(/ถึงพื้นที่ใหม่แล้ว · .*AR04.*AR05/)).toBeVisible();
  expect(drawer.getByText(/เวลาที่ถึงจริง · .*11:05/)).toBeVisible();
  expect(drawer.getAllByText(/เวลาที่ถึงจริง/)).toHaveLength(1);
  expect(drawer.queryByLabelText('พื้นที่ปลายทาง')).not.toBeInTheDocument();

  await app.user.type(drawer.getByLabelText('เหตุผลที่ยกเลิกคำสั่ง'), 'พื้นที่เดิมยังต้องการคน');
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.find(({ path }) => path === '/api/schedules/commands')?.body).toMatchObject({
    kind: 'cancel-transfer', expectedRevisions: { 'transfer:7:2026-11-17:Day': '1' },
    payload: { transferId: 19, reason: 'พื้นที่เดิมยังต้องการคน' },
  });
});

it('offers a forward move from a confirmed destination, without editing that arrival', async () => {
  const app = await editorApp({
    week: weekWithDestinations,
    routes: { 'GET /api/team-attendance/7': () => ({
      status: 200, body: { ...checkedIn, dutyAreaId: 5, transfer: confirmed, transferHistory: [confirmed] },
    }) },
  });
  const drawer = await openTransfer(app.user);
  expect(await drawer.findByText(/พื้นที่ปัจจุบัน · .*AR05/)).toBeVisible();
  expect(drawer.getAllByText(/เวลาที่ถึงจริง · .*11:05/)).toHaveLength(1);
  expect(drawer.queryByLabelText('เหตุผลที่ยกเลิกคำสั่ง')).not.toBeInTheDocument();
  expect(within(drawer.getByLabelText('พื้นที่ปลายทาง')).getByRole('option', { name: /AR04/ })).toBeEnabled();
});

it('offers only the shift-eligible destination list for a night transfer', async () => {
  const person = fixturePerson();
  person.days[1].shift = 'Night';
  person.days[1].edit!.areas.push({ id: 8, label: 'AR08 · พื้นที่เฉพาะกะเช้า' });
  const week = fixtureWeek([person]);
  week.destinations = [
    { date: '2026-11-17', shift: 'Night', buildingId: 1, buildingCode: 'A', areas: [{ id: 5, label: 'AR05' }] },
    { date: '2026-11-17', shift: 'Day', buildingId: 1, buildingCode: 'A', areas: [{ id: 8, label: 'AR08' }] },
  ];
  const app = await editorApp({
    week: () => week,
    routes: { 'GET /api/team-attendance/7': () => ({ status: 200, body: { ...checkedIn, shift: 'Night' } }) },
  });
  const drawer = await openTransfer(app.user);
  const select = within(await drawer.findByLabelText('พื้นที่ปลายทาง'));
  expect(select.getByRole('option', { name: /AR05/ })).toBeEnabled();
  expect(select.queryByRole('option', { name: /AR08/ })).not.toBeInTheDocument();
});

it('opens a confirmed inbound slot from the destination roster without any original plan row', async () => {
  const week = fixtureWeek([]);
  week.transferSlots = [{
    id: confirmed.id, userId: 7, displayName: 'แม่บ้านจากตึกต้นทาง', shiftDate: '2026-11-17', shift: 'Day',
    fromAreaCode: 'AR04', toAreaCode: 'AR05', revision: confirmed.revision, confirmedAt: confirmed.confirmedAt,
  }];
  const app = await editorApp({
    week: () => week,
    routes: { 'GET /api/team-attendance/7': () => ({
      status: 200, body: { ...checkedIn, dutyAreaId: 5, transfer: confirmed, transferHistory: [confirmed] },
    }) },
  });
  const region = within(await screen.findByRole('region', { name: 'การย้ายพื้นที่ที่เกี่ยวข้อง' }));
  expect(region.getByText(/ถึงพื้นที่ใหม่แล้ว.*11:05/)).toBeVisible();
  expect(region.getByRole('button', { name: /เพิ่มเวลาที่ขาด.*แม่บ้านจากตึกต้นทาง/ })).toBeEnabled();
  expect(screen.queryByRole('table', { name: 'ตารางงานรายสัปดาห์' })).not.toBeInTheDocument();
  await app.user.click(region.getByRole('button', { name: /ย้ายพื้นที่ระหว่างกะ.*แม่บ้านจากตึกต้นทาง/ }));
  const drawer = within(await screen.findByRole('dialog', { name: 'ย้ายพื้นที่ระหว่างกะ' }));
  expect(await drawer.findByText(/เวลาที่ถึงจริง · .*11:05/)).toBeVisible();
  const requested = app.calls.find(({ path }) => path === '/api/team-attendance/7');
  expect(requested?.search).toBe('?shiftDate=2026-11-17&shift=Day');
});
