import { act, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { authAnswer } from '../../../test/fakeServer';
import { commandId, committed, editorApp, fixtureWeek } from '../scheduleEditorTestFixture';

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-17T02:00:00Z'));
});
afterEach(() => vi.useRealTimers());

async function enterDraft(app: Awaited<ReturnType<typeof editorApp>>) {
  await app.user.click(await screen.findByRole('button', { name: /แก้เฉพาะวันที่.*17 พ\.ย\. 2569/ }));
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

it.each([
  { caseName: 'expiry', status: 404, expiresAt: '2026-11-17T01:59:00Z' },
  { caseName: 'lookup permission loss', status: 403, expiresAt: '2026-11-18T02:00:00Z' },
])('can leave an unresolved command after $caseName without replay', async ({ status, expiresAt }) => {
  const app = await editorApp({ routes: {
    'POST /api/schedules/command-tokens': () => ({ status: 200, body: { token: 'signed', commandId, expiresAt } }),
    'POST /api/schedules/commands': () => ({ status: 503 }),
    [`GET /api/schedules/commands/${commandId}`]: () => ({ status }),
  } });
  const panel = await enterDraft(app);
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  await app.user.click(await panel.findByRole('button', { name: 'ตรวจผลการบันทึก' }));
  await panel.findByRole('button', { name: 'ตรวจผลการบันทึก' });
  expect(panel.queryByRole('button', { name: 'ส่งคำสั่งเดิมอีกครั้ง' })).not.toBeInTheDocument();
  await app.user.click(panel.getByRole('button', { name: 'ปิด' }));
  expect(panel.getByText(/หากออกตอนนี้ ระบบยังยืนยันไม่ได้/)).toBeVisible();
  expect(panel.getByText(`รหัสอ้างอิง · ${commandId}`)).toBeVisible();
  await app.user.click(panel.getByRole('button', { name: 'ออกโดยยังไม่ทราบผล' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(app.calls.filter((call) => call.path === '/api/schedules/commands')).toHaveLength(1);
});

it('offers the original envelope only after fresh authority following a missing receipt', async () => {
  let sends = 0;
  let reads = 0;
  let readsWhenLookedUp = 0;
  const app = await editorApp({ routes: {
    'GET /api/schedules': () => {
      reads += 1;
      return { status: 200, body: fixtureWeek() };
    },
    'POST /api/schedules/commands': () => ++sends === 1 ? { status: 503 } : committed(),
    [`GET /api/schedules/commands/${commandId}`]: () => {
      readsWhenLookedUp = reads;
      return { status: 404 };
    },
  } });
  const panel = await enterDraft(app);
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  await app.user.click(await panel.findByRole('button', { name: 'ตรวจผลการบันทึก' }));
  const retry = await panel.findByRole('button', { name: 'ส่งคำสั่งเดิมอีกครั้ง' });
  expect(reads).toBeGreaterThan(readsWhenLookedUp);
  await app.user.click(retry);
  expect(await panel.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  const bodies = app.calls.filter((call) => call.path === '/api/schedules/commands').map((call) => call.body);
  expect(bodies).toHaveLength(2);
  expect(bodies[1]).toEqual(bodies[0]);
  expect(app.calls.filter((call) => call.path.endsWith('command-tokens'))).toHaveLength(1);
});

it('does not offer replay when a hidden receipt 404 is followed by an unauthorised week', async () => {
  let lost = false;
  const app = await editorApp({ routes: {
    'GET /api/schedules': () => lost ? { status: 403 } : { status: 200, body: fixtureWeek() },
    'POST /api/schedules/commands': () => ({ status: 503 }),
    [`GET /api/schedules/commands/${commandId}`]: () => {
      lost = true;
      return { status: 404 };
    },
  } });
  const panel = await enterDraft(app);
  await app.user.click(panel.getByRole('button', { name: 'ตรวจสอบ' }));
  await app.user.click(panel.getByRole('button', { name: 'บันทึก' }));
  await app.user.click(await panel.findByRole('button', { name: 'ตรวจผลการบันทึก' }));
  await panel.findByText('ตารางเปลี่ยนระหว่างแก้ไข');
  expect(panel.queryByRole('button', { name: 'ส่งคำสั่งเดิมอีกครั้ง' })).not.toBeInTheDocument();
  expect(app.calls.filter((call) => call.path === '/api/schedules/commands')).toHaveLength(1);
});
