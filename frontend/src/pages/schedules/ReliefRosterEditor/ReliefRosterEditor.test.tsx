import { screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import { reliefWeek, installReliefFixtureClock } from '../reliefEditorTestFixture';
import { editorApp } from '../scheduleEditorTestFixture';

installReliefFixtureClock();

it('saves edited cells for multiple people in one partial batch', async () => {
  const { user, calls } = await editorApp({ week: reliefWeek, source: reliefWeek('2026-11-23') });
  await user.click(await screen.findByRole('button', { name: 'จัดตารางประจำสแปร์' }));
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
