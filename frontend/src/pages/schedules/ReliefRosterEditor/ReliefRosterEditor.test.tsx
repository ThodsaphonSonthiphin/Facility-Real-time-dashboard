import { screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import { reliefWeek, installReliefFixtureClock } from '../reliefEditorTestFixture';
import { commandId, committed, editorApp } from '../scheduleEditorTestFixture';

installReliefFixtureClock();

it('saves edited cells for multiple people in one partial batch', async () => {
  const { user, calls } = await editorApp({ week: reliefWeek, source: reliefWeek('2026-11-23') });
  await user.click(await screen.findByRole('button', { name: 'จัดการเพิ่มเติม' }));
  await user.click(await screen.findByRole('menuitem', { name: 'จัดตารางประจำสแปร์' }));
  const drawer = within(await screen.findByRole('dialog', { name: 'จัดตารางประจำสแปร์' }));
  const first = within(drawer.getByRole('group', { name: 'สแปร์หนึ่ง' }));
  const second = within(drawer.getByRole('group', { name: 'สแปร์สอง' }));
  expect(first.getByText('ตึกประจำ: A')).toBeVisible();
  expect(second.getByText('ตึกประจำ: B')).toBeVisible();
  await first.findByText('จัดแล้ว 0 วัน · เป้าหมาย 6 วัน');
  await user.selectOptions(first.getByLabelText('อ.'), '5');
  await user.selectOptions(second.getByLabelText('พ.'), 'off');
  expect(first.getByText('จัดแล้ว 1 วัน · เป้าหมาย 6 วัน')).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  expect(drawer.getByText(/หลัง.*อ\..*B · AR05/)).toBeVisible();
  expect(drawer.getByText(/หลัง.*พ\..*วันหยุด/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  const commits = calls.filter((call) => call.path === '/api/schedules/commands');
  expect(commits).toHaveLength(1);
  expect(commits[0].body).toMatchObject({
    kind: 'set-relief-baseline',
    expectedRevisions: { 'baseline:7:relief': '9007199254740993', 'baseline:8:relief': '0' },
    payload: { rows: [
      { userId: 7, effectiveFrom: '2026-11-17', days: [{ weekday: 'Tuesday', areaId: 5, isDayOff: false }] },
      { userId: 8, effectiveFrom: '2026-11-17', days: [{ weekday: 'Wednesday', isDayOff: true }] },
    ] },
  });
});

it('keeps the first-edit, refreshed and draft weekday values distinct until the recorder rebases', async () => {
  let changed = false;
  const { user, calls } = await editorApp({
    week: () => {
      const week = reliefWeek();
      week.rows[0].days[1].edit!.baselineAreaId = changed ? 5 : 4;
      week.rows[0].reliefBaselineRevision = changed ? '4' : '1';
      return week;
    },
    source: reliefWeek('2026-11-23'),
    routes: { 'POST /api/schedules/commands': () => {
      if (changed) return committed();
      changed = true;
      return { status: 409, body: { commandId, outcome: 'rejected', revisions: {}, problems: [
        { rowId: 'baseline:7:relief', code: 'stale-revision', message: 'ตารางเปลี่ยนแล้ว' },
      ] } };
    } },
  });
  await user.click(await screen.findByRole('button', { name: 'จัดการเพิ่มเติม' }));
  await user.click(await screen.findByRole('menuitem', { name: 'จัดตารางประจำสแปร์' }));
  const drawer = within(await screen.findByRole('dialog'));
  const first = within(drawer.getByRole('group', { name: 'สแปร์หนึ่ง' }));
  await first.findByText('จัดแล้ว 1 วัน · เป้าหมาย 6 วัน');
  await user.selectOptions(first.getByLabelText('อ.'), 'off');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  await user.click(await drawer.findByRole('button', { name: 'โหลดเพื่อเปรียบเทียบ' }));
  expect(await drawer.findByText(/ล่าสุด.*B · AR05/)).toBeVisible();
  expect(drawer.getByText(/ตอนเปิด.*A · AR04/)).toBeVisible();
  expect(drawer.getByText(/ร่าง.*วันหยุด/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'ใช้ข้อมูลล่าสุดและเก็บร่าง' }));
  expect(within(drawer.getByRole('group', { name: 'สแปร์หนึ่ง' })).getByLabelText('อ.')).toHaveValue('off');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  expect(drawer.getByText(/ก่อน.*B · AR05/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(calls.filter((call) => call.path === '/api/schedules/commands').at(-1)?.body).toMatchObject({
    expectedRevisions: { 'baseline:7:relief': '4' },
    payload: { rows: [{ userId: 7, days: [{ weekday: 'Tuesday', isDayOff: true }] }] },
  });
});

it('clears dirty state when a cell is reverted back to its original value and resets edits when effectiveFrom changes', async () => {
  const { user } = await editorApp({ week: reliefWeek, source: reliefWeek('2026-11-23') });
  await user.click(await screen.findByRole('button', { name: 'จัดการเพิ่มเติม' }));
  await user.click(await screen.findByRole('menuitem', { name: 'จัดตารางประจำสแปร์' }));
  const drawer = within(await screen.findByRole('dialog', { name: 'จัดตารางประจำสแปร์' }));
  const first = within(drawer.getByRole('group', { name: 'สแปร์หนึ่ง' }));
  await first.findByText('จัดแล้ว 0 วัน · เป้าหมาย 6 วัน');

  // Edit Tuesday then revert it back to empty: review button returns to disabled and closing needs no discard prompt.
  await user.selectOptions(first.getByLabelText('อ.'), '5');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeEnabled();
  await user.selectOptions(first.getByLabelText('อ.'), '');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeDisabled();

  // Edit again, then change effectiveFrom: pending edits from the old date are cleared.
  await user.selectOptions(first.getByLabelText('อ.'), '5');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeEnabled();
  const dateInput = drawer.getByLabelText('วันเริ่มใช้');
  await user.clear(dateInput);
  await user.type(dateInput, '2026-11-18');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeDisabled();
});
