import { screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { commandId, editorApp, fixtureMonday, fixturePerson, fixtureWeek } from '../scheduleEditorTestFixture';
import { copyOffChoices } from './useWeeklyOffEditor';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

it('maps Monday to local Monday, keeps a local choice, leaves a source gap unset, and never adds a removed member', () => {
  const target = { weekStart: fixtureMonday, choices: { 7: ['2026-11-20'] } };
  const prior = { weekStart: '2026-11-09', choices: { 7: ['2026-11-09'], 8: ['2026-11-09'], 100: ['2026-11-10'] } };
  const result = copyOffChoices(target, prior, [fixturePerson(7), fixturePerson(8), fixturePerson(9)]);
  expect(result).toEqual({
    draft: { weekStart: fixtureMonday, choices: { 7: ['2026-11-20'], 8: [fixtureMonday] } },
    missingSourceIds: [9],
  });
  expect(prior.choices[8]).toEqual(['2026-11-09']);
  expect(target.choices).toEqual({ 7: ['2026-11-20'] });
});

it('keeps input when returning from review and restores saved choices after discarding', async () => {
  const person = fixturePerson();
  person.days[5].edit!.weeklyOff = true;
  person.hasWeeklyChoice = true;
  const app = await editorApp({ week: () => fixtureWeek([person, fixturePerson(8, 'ยังไม่จัดคนที่สอง')]) });
  const open = await screen.findByRole('button', { name: 'จัดวันหยุดรายสัปดาห์' });
  await app.user.click(open);
  const drawer = within(await screen.findByRole('dialog'));
  const first = within(drawer.getByRole('group', { name: 'แม่บ้านทดสอบ' }));
  const weekDay = first.getByRole('checkbox', { name: /19\/11/ });
  const savedDay = first.getByRole('checkbox', { name: /21\/11/ });
  expect(savedDay).toBeChecked();
  expect(within(drawer.getByRole('group', { name: 'ยังไม่จัดคนที่สอง' })).getByText('ยังจัดไม่ครบ')).toBeVisible();
  await app.user.click(weekDay);
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  expect(drawer.getByRole('heading', { name: 'ตรวจสอบก่อนบันทึก' })).toBeVisible();
  expect(drawer.queryByText('ยังไม่จัดคนที่สอง')).not.toBeInTheDocument();
  await app.user.click(drawer.getByRole('button', { name: 'กลับไปแก้ไข' }));
  expect(within(drawer.getByRole('group', { name: 'แม่บ้านทดสอบ' })).getByRole('checkbox', { name: /19\/11/ })).toBeChecked();
  await app.user.click(drawer.getByRole('button', { name: 'ยกเลิก' }));
  await app.user.click(drawer.getByRole('button', { name: 'ทิ้งร่าง' }));
  await app.user.click(open);
  const reopened = within(await screen.findByRole('group', { name: 'แม่บ้านทดสอบ' }));
  expect(reopened.getByRole('checkbox', { name: /19\/11/ })).not.toBeChecked();
  expect(reopened.getByRole('checkbox', { name: /21\/11/ })).toBeChecked();
  expect(app.calls.filter((call) => call.method === 'POST' && call.path.includes('schedules'))).toHaveLength(0);
});

it('submits edited rows atomically and shows an invalid row without reporting partial success', async () => {
  const app = await editorApp({
    week: () => fixtureWeek([fixturePerson(), fixturePerson(8, 'แม่บ้านคนที่สอง'), fixturePerson(9, 'ยังไม่จัด')]),
    routes: {
      'POST /api/schedules/commands': () => ({
        status: 409, body: { commandId, outcome: 'rejected', revisions: {}, problems: [
          { rowId: `week:8:${fixtureMonday}`, code: 'row-invalid', message: 'ตรวจวันหยุดของแม่บ้านคนที่สอง' },
        ] },
      }),
    },
  });
  await app.user.click(await screen.findByRole('button', { name: 'จัดวันหยุดรายสัปดาห์' }));
  const drawer = within(await screen.findByRole('dialog'));
  for (const name of ['แม่บ้านทดสอบ', 'แม่บ้านคนที่สอง']) {
    await app.user.click(within(drawer.getByRole('group', { name })).getByRole('checkbox', { name: /19\/11/ }));
  }
  await app.user.click(drawer.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('ตรวจวันหยุดของแม่บ้านคนที่สอง')).toBeVisible();
  expect(drawer.queryByText('บันทึกการเปลี่ยนแปลงแล้ว')).not.toBeInTheDocument();
  expect(drawer.getAllByRole('checkbox', { checked: true })).toHaveLength(2);
  const sent = app.calls.filter((call) => call.path === '/api/schedules/commands');
  expect(sent).toHaveLength(1);
  expect(sent[0].body).toMatchObject({
    kind: 'set-weekly-days-off', payload: { weekStart: fixtureMonday, rows: [
      { userId: 7, offDates: ['2026-11-19'] }, { userId: 8, offDates: ['2026-11-19'] },
    ] },
  });
});
