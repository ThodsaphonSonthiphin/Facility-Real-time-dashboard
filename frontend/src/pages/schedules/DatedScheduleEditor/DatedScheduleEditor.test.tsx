import { screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { editorApp, fixturePerson, fixtureWeek } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

it('labels a past plan correction and requires a reason before submitting the dated payload', async () => {
  const app = await editorApp();
  await app.user.click(await screen.findByRole('button', { name: /เลือกช่อง แม่บ้านทดสอบ .* 16 พ\.ย\. 2569/ }));
  await app.user.click(await screen.findByRole('button', { name: /แก้ไขแผนย้อนหลัง.*16 พ\.ย\. 2569/ }));
  const drawer = within(await screen.findByRole('dialog', { name: 'แก้ไขแผนย้อนหลัง' }));
  await app.user.selectOptions(drawer.getByLabelText('พื้นที่เฉพาะวันนี้'), '5');
  expect(drawer.getByRole('button', { name: 'ตรวจสอบ' })).toBeDisabled();
  await app.user.type(drawer.getByLabelText('เหตุผลที่แก้ไขแผนย้อนหลัง'), 'แก้ข้อมูลเดิม');
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.find((call) => call.path === '/api/schedules/commands')?.body).toMatchObject({
    kind: 'correct-plan', expectedRevisions: { 'exception:7:2026-11-16:Day:area': '0' },
    payload: { userId: 7, date: '2026-11-16', shift: 'Day', areaId: 5, reason: 'แก้ข้อมูลเดิม' },
  });
});

it('records Thai night-shift leave using UTC instants and the date the shift starts', async () => {
  const person = fixturePerson();
  person.days[1].shift = 'Night';
  const app = await editorApp({ week: () => fixtureWeek([person]) });
  await app.user.click(await screen.findByRole('button', { name: /เลือกช่อง แม่บ้านทดสอบ .* 17 พ\.ย\. 2569/ }));
  await app.user.click(await screen.findByRole('button', { name: /แก้เฉพาะวันที่.*17 พ\.ย\. 2569/ }));
  const drawer = within(await screen.findByRole('dialog', { name: 'แก้ตารางเฉพาะวันที่' }));
  await app.user.selectOptions(drawer.getByLabelText('รายการที่จะแก้'), 'leave');
  expect(drawer.getByLabelText('เริ่มลา')).toHaveValue('2026-11-17T19:00');
  expect(drawer.getByLabelText('สิ้นสุดลา')).toHaveValue('2026-11-18T07:00');
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.find((call) => call.path === '/api/schedules/commands')?.body).toMatchObject({
    kind: 'record-leave', expectedRevisions: { 'leave:7:2026-11-17:Night': '0' },
    payload: { userId: 7, date: '2026-11-17', shift: 'Night',
      startsAt: '2026-11-17T12:00:00.000Z', endsAt: '2026-11-18T00:00:00.000Z' },
  });
});
