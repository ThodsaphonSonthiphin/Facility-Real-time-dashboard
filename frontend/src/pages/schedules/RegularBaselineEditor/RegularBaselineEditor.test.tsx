import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { editorApp, fixturePerson, fixtureWeek } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

it('reviews a midweek future baseline separately from a one-date change', async () => {
  const app = await editorApp();
  await app.user.click(await screen.findByRole('button', { name: 'แก้ตารางประจำ' }));
  const drawer = within(await screen.findByRole('dialog', { name: 'แก้ตารางประจำ' }));
  expect(drawer.getByLabelText('วันเริ่มใช้')).toHaveValue('2026-11-18');
  await app.user.selectOptions(drawer.getByLabelText('พื้นที่ประจำ'), '5');
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  expect(drawer.getByText(/หลัง.*พื้นที่ใหม่.*ทุกวัน/)).toBeVisible();
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.find((call) => call.path === '/api/schedules/commands')?.body).toMatchObject({
    kind: 'set-regular-baseline', expectedRevisions: { 'baseline:7:regular': '2' },
    payload: { userId: 7, effectiveFrom: '2026-11-18', days: expect.arrayContaining([
      { weekday: 'Wednesday', areaId: 5, breakGroup: 1 },
    ]) },
  });
});

it('reviews the authoritative before values for the effective date in another week', async () => {
  const monday = '2026-11-23';
  const person = fixturePerson(7, 'แม่บ้านทดสอบ', monday);
  person.days[1].edit!.baselineAreaId = 6;
  person.days[1].edit!.baselineBreakGroup = 2;
  const app = await editorApp({ source: fixtureWeek([person], 'next-week', monday) });
  await app.user.click(await screen.findByRole('button', { name: 'แก้ตารางประจำ' }));
  const drawer = within(await screen.findByRole('dialog'));
  fireEvent.change(drawer.getByLabelText('วันเริ่มใช้'), { target: { value: '2026-11-24' } });
  await drawer.findByRole('option', { name: /AR05/ });
  await app.user.selectOptions(drawer.getByLabelText('พื้นที่ประจำ'), '5');
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  expect(drawer.getByText('ก่อน · พื้นที่ 6 · กลุ่มพัก 2')).toBeVisible();
  expect(drawer.getByText(/เริ่ม 24\/11\/2026/)).toBeVisible();
});
