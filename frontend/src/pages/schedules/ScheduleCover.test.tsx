import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession, buildings, managedBuildings } from '../../test/adminFixtures';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { attendanceBoard, candidates } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serveAdmin(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard() }),
    'GET /api/schedules': () => ({
      status: 200,
      body: {
        weekStart: '2026-10-12',
        weekEnd: '2026-10-18',
        availability: 'available',
        rows: [],
        relief: [],
      },
    }),
    ...routes,
  });
}

describe('Schedules emergency cover consolidation (facility-0225)', () => {
  it('opens CoverDrawer from /admin/schedules, assigns a cover, and cancels an existing cover', async () => {
    const server = serveAdmin({
      'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }),
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: {
          id: 72,
          userId: 21,
          personName: 'แม่บ้านทดสอบ',
          areaId: 12,
          areaCode: 'AR12',
          shiftDate: '2026-10-15',
          shift: 'Day',
          assignedAt: '2026-10-15T03:20:00Z',
        },
      }),
      'DELETE /api/admin/cover-assignments/71': () => ({ status: 204 }),
    });
    const { user } = await renderApp('/admin/schedules');

    const moreButton = await screen.findByRole('button', { name: 'จัดการเพิ่มเติม' });
    await user.click(moreButton);
    const trigger = await screen.findByRole('menuitem', { name: 'มอบหมายทำแทนฉุกเฉิน' });
    await waitFor(() => expect(trigger).toBeEnabled());
    await user.click(trigger);

    const form = await screen.findByRole('dialog', { name: 'มอบหมายทำแทน' });
    expect(within(form).getByText('เลือกพื้นที่ก่อน')).toBeInTheDocument();
    expect(within(form).getByRole('button', { name: 'มอบหมาย' })).toBeDisabled();

    const areas = within(form).getByRole('radiogroup', { name: '2. พื้นที่ที่ต้องมีคนทำแทน' });
    await user.click(await within(areas).findByRole('radio', { name: /^AR12 Area สิบสอง/ }));
    await within(form).findByRole('radiogroup', { name: 'แม่บ้านที่จะทำแทน' });
    await user.click(within(form).getByRole('radio', { name: /^แม่บ้านทดสอบ/ }));
    expect(within(form).getByText(/จะดูแล 2 พื้นที่ในกะเช้า พฤ\. 15\/10\/2026/)).toBeInTheDocument();

    await user.click(within(form).getByRole('button', { name: 'มอบหมาย' }));
    expect(await screen.findByText('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(
      server.calls
        .filter((call) => call.method === 'POST' && call.path === '/api/admin/cover-assignments')
        .map((call) => call.body),
    ).toEqual([{ userId: 21, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(moreButton);
    await user.click(await screen.findByRole('menuitem', { name: 'มอบหมายทำแทนฉุกเฉิน' }));
    const reopen = await screen.findByRole('dialog', { name: 'มอบหมายทำแทน' });
    await user.click(await within(reopen).findByRole('radio', { name: /^AR02/ }));
    expect(within(reopen).getByText('AR02 มีคนทำแทนอยู่แล้ว')).toBeInTheDocument();
    expect(within(reopen).getByRole('heading', { name: 'ทำแทนที่มอบหมายไว้แล้วกะนี้ · 1' })).toBeInTheDocument();
    await user.click(within(reopen).getByRole('button', { name: 'ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ยกเลิกการทำแทน' }));

    await waitFor(() =>
      expect(server.calls.some((call) => call.method === 'DELETE' && call.path === '/api/admin/cover-assignments/71')).toBe(true),
    );
  });

  it('does not show the Admin emergency cover button on Supervisor /schedule-management', async () => {
    fakeServer({
      'POST /api/auth/refresh': () => authAnswer('supervisor'),
      'GET /api/schedules': () => ({
        status: 200,
        body: {
          weekStart: '2026-10-12',
          weekEnd: '2026-10-18',
          availability: 'available',
          rows: [],
          relief: [],
        },
      }),
    });
    await renderApp('/schedule-management');

    await screen.findByRole('heading', { level: 1, name: 'จัดตารางงาน' });
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทนฉุกเฉิน' })).not.toBeInTheDocument();
  });
});
