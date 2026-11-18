import { act, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { authAnswer } from '../../../test/fakeServer';
import { commandId, committed, editorApp } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

async function enterDraft(app: Awaited<ReturnType<typeof editorApp>>) {
  await app.user.click(await screen.findByRole('button', { name: /แก้เฉพาะวันที่.*17\/11/ }));
  const panel = within(await screen.findByRole('dialog', { name: 'แก้ตารางเฉพาะวันที่' }));
  await app.user.selectOptions(panel.getByLabelText('พื้นที่เฉพาะวันนี้'), '5');
  return panel;
}

it('blocks dirty route navigation and keeps editing if the user stays', async () => {
  const app = await editorApp();
  const panel = await enterDraft(app);
  await act(() => { void app.router.navigate('/my-building'); });
  expect(await panel.findByText('มีข้อมูลที่ยังไม่ได้บันทึก')).toBeVisible();
  await app.user.click(panel.getByRole('button', { name: 'อยู่หน้านี้' }));
  expect(app.router.state.location.pathname).toBe('/schedule-management');
  expect(panel.getByLabelText('พื้นที่เฉพาะวันนี้')).toHaveValue('5');
});

it('keeps an uncertain edit until the command result is known and checks the exact id', async () => {
  let attempts = 0;
  const app = await editorApp({ routes: {
    'POST /api/schedules/commands': () => ({ status: 503 }),
    [`GET /api/schedules/commands/${commandId}`]: () => ++attempts === 1 ? { status: 503 } : committed(),
  } });
  const panel = await enterDraft(app);
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  expect(await panel.findByText('ยังยืนยันผลการบันทึกไม่ได้')).toBeVisible();
  await app.user.click(panel.getByRole('button', { name: 'ตรวจผลการบันทึก' }));
  expect(await panel.findByRole('button', { name: 'ตรวจผลการบันทึก' })).toBeEnabled();
  expect(panel.queryByRole('button', { name: 'ส่งคำสั่งเดิมอีกครั้ง' })).not.toBeInTheDocument();
  await app.user.click(panel.getByRole('button', { name: 'ตรวจผลการบันทึก' }));
  expect(await panel.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(app.calls.filter((call) => call.method === 'POST' && call.path.startsWith('/api/schedules')))
    .toHaveLength(2);
});

it('refreshes a 401 with the exact same command identity, revisions and payload', async () => {
  let refreshes = 0;
  let submissions = 0;
  const app = await editorApp({ routes: {
    'POST /api/auth/refresh': () => authAnswer('supervisor', `access-${++refreshes}`),
    'POST /api/schedules/commands': () => ++submissions === 1 ? { status: 401 } : committed(),
  } });
  const panel = await enterDraft(app);
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  expect(await panel.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  const [first, second] = app.calls.filter((call) => call.path === '/api/schedules/commands');
  expect(second.body).toEqual(first.body);
  expect(second.authorization).not.toEqual(first.authorization);
  expect(app.calls.filter((call) => call.path.endsWith('command-tokens'))).toHaveLength(1);
});
