import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { AttendanceState } from '../../../shared/api/types';
import { editorApp } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T06:00:00Z'));
});
afterEach(() => vi.useRealTimers());

const initial: AttendanceState = {
  shiftDate: '2026-11-17', shift: 'Day', stateRevision: 'attendance-1',
  events: [
    { eventType: 'ShiftIn', occurredAt: '2026-11-17T00:03:00Z', source: 'Scan', withinRadius: true },
    { eventType: 'ShiftOut', occurredAt: '2026-11-17T06:00:00Z', source: 'Scan', withinRadius: true },
  ],

  actions: [
    { eventType: 'ShiftIn', state: 'recorded', occurredAt: '2026-11-17T00:03:00Z' },
    { eventType: 'Break1Out', state: 'missing' },
    { eventType: 'Break1In', state: 'missing' },
    { eventType: 'Break2Out', state: 'not-applicable' },
    { eventType: 'Break2In', state: 'not-applicable' },
    { eventType: 'ShiftOut', state: 'recorded', occurredAt: '2026-11-17T06:00:00Z' },
  ],
  fillableActions: ['Break1Out', 'Break1In'],
};

const openMissingTime = async (user: Awaited<ReturnType<typeof editorApp>>['user']) => {
  await user.click(await screen.findByRole('button', { name: /เพิ่มเวลาที่ขาด.*17\/11/ }));
  const drawer = within(await screen.findByRole('dialog', { name: 'เพิ่มเวลาที่ขาด' }));
  await drawer.findByLabelText('รายการลงเวลา');
  return drawer;
};

it('uses server eligibility and the selected slot revision, requires a reason and disables overwrite', async () => {
  let current = initial;
  const app = await editorApp({
    routes: {
      'GET /api/team-attendance/7': () => ({ status: 200, body: current }),
      'POST /api/team-attendance/missing': () => {
        current = {
          ...current, stateRevision: 'attendance-2', fillableActions: ['Break1In'],
          actions: current.actions?.map((action) => action.eventType === 'Break1Out'
            ? { ...action, state: 'recorded', occurredAt: '2026-11-17T03:00:00Z' } : action),
        };
        return { status: 200, body: current };
      },
    },
  });

  const drawer = await openMissingTime(app.user);
  expect(app.calls.find(({ path }) => path === '/api/team-attendance/7')?.search).toContain('shiftDate=2026-11-17&shift=Day');
  expect(drawer.getByRole('option', { name: 'เข้างาน · ลงเวลาแล้ว 07:03' })).toBeDisabled();
  expect(drawer.getByRole('option', { name: 'กลับจากพัก 2 · ไม่ต้องลงเวลาเนื่องจากลา' })).toBeDisabled();

  await app.user.selectOptions(drawer.getByLabelText('รายการลงเวลา'), 'Break1Out');
  fireEvent.change(drawer.getByLabelText('เวลาที่เกิดขึ้นจริง (เวลาไทย)'), { target: { value: '10:00' } });
  const save = drawer.getByRole('button', { name: 'บันทึกเวลาที่ขาด' });
  expect(save).toBeDisabled();
  await app.user.type(drawer.getByLabelText('เหตุผลที่เพิ่มเวลา'), '  ลืมสแกน  ');
  await app.user.click(save);

  expect(await drawer.findByText('บันทึกเวลาที่ขาดแล้ว')).toBeVisible();
  expect(app.calls.find(({ method, path }) => method === 'POST' && path === '/api/team-attendance/missing')?.body).toEqual({
    userId: 7, shiftDate: '2026-11-17', shift: 'Day', eventType: 'Break1Out',
    time: '10:00', reason: 'ลืมสแกน', expectedRevision: 'attendance-1',
  });
  expect(await drawer.findByRole('option', { name: 'ออกพัก 1 · ลงเวลาแล้ว 10:00' })).toBeDisabled();
});

it('shows the stored departure and leave mismatch in the scoped slot', async () => {
  const app = await editorApp({
    routes: {
      'GET /api/team-attendance/7': () => ({ status: 200, body: { ...initial, departureLeaveMismatch: true } }),
    },
  });

  const drawer = await openMissingTime(app.user);
  expect(drawer.getByText('เวลาเลิกงานไม่ตรงกับช่วงลา')).toBeVisible();
  expect(drawer.getByRole('option', { name: 'เลิกงาน · ลงเวลาแล้ว 13:00' })).toBeDisabled();
});

it('refreshes on a competing scan and cannot resubmit that action', async () => {
  let current = initial;
  const app = await editorApp({
    routes: {
      'GET /api/team-attendance/7': () => ({ status: 200, body: current }),
      'POST /api/team-attendance/missing': () => {
        current = {
          ...current, stateRevision: 'attendance-2',
          events: [...current.events, {
            eventType: 'Break1Out', occurredAt: '2026-11-17T03:01:00Z', source: 'Scan', withinRadius: true,
          }],
        };
        return { status: 409, body: { message: 'มีการลงเวลารายการนี้แล้ว' } };
      },
    },
  });

  const drawer = await openMissingTime(app.user);
  await app.user.selectOptions(drawer.getByLabelText('รายการลงเวลา'), 'Break1Out');
  fireEvent.change(drawer.getByLabelText('เวลาที่เกิดขึ้นจริง (เวลาไทย)'), { target: { value: '10:00' } });
  await app.user.type(drawer.getByLabelText('เหตุผลที่เพิ่มเวลา'), 'ลืมสแกน');
  await app.user.click(drawer.getByRole('button', { name: 'บันทึกเวลาที่ขาด' }));

  expect(await drawer.findByText('มีการลงเวลารายการนี้แล้ว')).toBeVisible();
  await waitFor(() => expect(drawer.getByRole('option', { name: 'ออกพัก 1 · ลงเวลาแล้ว 10:01' })).toBeDisabled());
  expect(drawer.getByRole('button', { name: 'บันทึกเวลาที่ขาด' })).toBeDisabled();
  expect(drawer.getByLabelText('รายการลงเวลา')).toHaveValue('');
});

it('keeps a dirty draft on its original revision until the supervisor accepts a polled change', async () => {
  let current = initial;
  const app = await editorApp({
    routes: {
      'GET /api/team-attendance/7': () => ({ status: 200, body: current }),
      'POST /api/team-attendance/missing': () => ({ status: 200, body: current }),
    },
  });
  const drawer = await openMissingTime(app.user);
  const selection = drawer.getByLabelText('รายการลงเวลา');
  const time = drawer.getByLabelText('เวลาที่เกิดขึ้นจริง (เวลาไทย)');
  const reason = drawer.getByLabelText('เหตุผลที่เพิ่มเวลา');
  const save = drawer.getByRole('button', { name: 'บันทึกเวลาที่ขาด' });
  await app.user.selectOptions(selection, 'Break1Out');
  fireEvent.change(time, { target: { value: '10:00' } });
  await app.user.type(reason, 'ลืมสแกนก่อนพัก');
  expect(save).toBeEnabled();

  current = {
    ...initial, stateRevision: 'attendance-2', fillableActions: ['Break1Out'],
    events: [...initial.events, {
      eventType: 'Break1In', occurredAt: '2026-11-17T04:00:00Z', source: 'Scan', withinRadius: true,
    }],
  };
  await act(async () => { await vi.advanceTimersByTimeAsync(31_000); });

  expect(await drawer.findByText('ข้อมูลการลงเวลาเปลี่ยนแล้ว')).toBeVisible();
  expect(drawer.getByRole('option', { name: 'กลับจากพัก 1 · ลงเวลาแล้ว 11:00' })).toBeDisabled();
  expect(selection).toHaveValue('Break1Out');
  expect(time).toHaveValue('10:00');
  expect(reason).toHaveValue('ลืมสแกนก่อนพัก');
  expect(save).toBeDisabled();
  expect(app.calls.filter(({ path }) => path === '/api/team-attendance/missing')).toHaveLength(0);

  await app.user.click(drawer.getByRole('button', { name: 'ใช้ข้อมูลล่าสุด' }));
  expect(save).toBeEnabled();
  await app.user.click(save);
  expect(await drawer.findByText('บันทึกเวลาที่ขาดแล้ว')).toBeVisible();
  expect(app.calls.find(({ path }) => path === '/api/team-attendance/missing')?.body).toMatchObject({
    eventType: 'Break1Out', time: '10:00', reason: 'ลืมสแกนก่อนพัก', expectedRevision: 'attendance-2',
  });
});
