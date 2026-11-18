import { screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { editorApp } from '../scheduleEditorTestFixture';

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
