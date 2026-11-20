import { screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { commandId, editorApp, fixturePerson, fixtureWeek } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

it('compares the preserved draft with a fresh scoped read after a stale command', async () => {
  let changed = false;
  const app = await editorApp({
    week: () => {
      const person = fixturePerson();
      if (changed) {
        person.days[1].area.value = 9;
        person.days[1].edit!.areaRevision = '1';
      }
      return fixtureWeek([person], changed ? 'changed' : 'initial');
    },
    routes: {
      'POST /api/schedules/commands': () => {
        changed = true;
        return { status: 409, body: { commandId, outcome: 'rejected', revisions: {}, problems: [
          { rowId: 'exception:7:2026-11-17:Day:area', code: 'stale-revision', message: 'เวอร์ชันเปลี่ยน' },
        ] } };
      },
    },
  });
  await app.user.click(await screen.findByRole('button', { name: /แก้เฉพาะวันที่.*17\/11/ }));
  const panel = within(await screen.findByRole('dialog'));
  await app.user.selectOptions(panel.getByLabelText('พื้นที่เฉพาะวันนี้'), '5');
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  expect(await panel.findByText('เวอร์ชันเปลี่ยน')).toBeVisible();
  await app.user.click(panel.getByRole('button', { name: 'โหลดเพื่อเปรียบเทียบ' }));
  expect(await panel.findByText('ล่าสุด · พื้นที่ 9')).toBeVisible();
  expect(panel.getByText(/ร่าง.*พื้นที่ใหม่/)).toBeVisible();
  await app.user.click(panel.getByRole('button', { name: 'ใช้ข้อมูลล่าสุดและเก็บร่าง' }));
  expect(panel.getByLabelText('พื้นที่เฉพาะวันนี้')).toHaveValue('5');
});
