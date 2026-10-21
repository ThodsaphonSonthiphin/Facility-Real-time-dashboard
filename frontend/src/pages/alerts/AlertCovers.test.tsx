import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { alerts, apiIdle, attendanceBoard, candidates, supervisorBlockedAlert } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/alerts': () => ({ status: 200, body: [supervisorBlockedAlert, ...alerts] }),
    'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard() }),
    'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }),
    ...routes,
  });
}

const posts = (server: ReturnType<typeof serve>) =>
  server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/cover-assignments');

describe('covers on the alert page (D3, facility-0041, 0048)', () => {
  it('lists the Areas with nobody this shift; มอบหมายคนทำแทน offers the candidates, best first, and assigns one', async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 72, userId: 21, personName: 'แม่บ้านทดสอบ', areaId: 12, areaCode: 'AR12', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T03:20:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');

    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });
    expect(within(empty).getByText('แม่บ้านลา ยังไม่ลงเวลา')).toBeInTheDocument();
    expect(within(empty).getByText('ไม่มีแม่บ้านประจำ')).toBeInTheDocument();

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    expect(within(form).getByText('แม่บ้านประจำ: แม่บ้านลา · ยังไม่ลงเวลา')).toBeInTheDocument();
    expect(within(form).getByText('มีผลเฉพาะกะเช้า 15/10/2026 · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก')).toBeInTheDocument();
    const who = await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });
    expect(within(who).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'แม่บ้านที่อื่น · AR03 · เข้างานแล้ว 06:55 · สแกนป้าย Area นี้เมื่อ 09:10',
      'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52',
      'แม่บ้านมาสาย · AR08 · ยังไม่ลงเวลา',
    ]);
    expect(server.calls.find((call) => call.path === '/api/admin/cover-assignments/candidates')?.search).toBe(
      '?areaId=12&shiftDate=2026-10-15&shift=Day',
    );

    await user.selectOptions(who, 'แม่บ้านทดสอบ · AR01 · เข้างานแล้ว 06:52');
    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([{ userId: 21, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }]);
    expect(screen.queryByRole('region', { name: 'มอบหมายคนทำแทน · AR12' })).not.toBeInTheDocument();
  });

  it('says when nobody can cover, shows a refusal in the form, and ยกเลิก closes it', async () => {
    serve({
      'GET /api/admin/cover-assignments/candidates': (request) => ({
        status: 200,
        body: request.search.includes('areaId=9') ? [] : candidates,
      }),
      'POST /api/admin/cover-assignments': () => ({ status: 400, body: { message: 'กะนี้จบไปแล้ว' } }),
    });
    const { user } = await renderApp('/admin/alerts');
    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR09' }));
    const none = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR09' });
    expect(await within(none).findByText('ไม่มีแม่บ้านกะนี้ที่ทำแทนได้')).toBeInTheDocument();
    expect(within(none).getByRole('button', { name: 'ยืนยันมอบหมาย' })).toBeDisabled();
    await user.click(within(none).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('region', { name: 'มอบหมายคนทำแทน · AR09' })).not.toBeInTheDocument();

    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });
    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    expect(await within(form).findByRole('alert')).toHaveTextContent('กะนี้จบไปแล้ว');
  });

  it("a Cleaner's Blocked Scan gives its Area cover in one press, which also closes the alert (facility-0041 rule 4)", async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 73, userId: 25, personName: 'แม่บ้านที่อื่น', areaId: 12, areaCode: 'AR12', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T03:20:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'สแกนนอก Area (ถูกบล็อก) · แม่บ้านที่อื่น' });
    const alertGets = server.calls.filter((call) => call.path === '/api/admin/alerts').length;

    await user.click(within(item).getByRole('button', { name: 'ให้ทำแทน AR12 กะนี้' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านที่อื่น ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([
      { userId: 25, areaId: 12, shiftDate: '2026-10-15', shift: 'Day', blockedScanId: 301 },
    ]);
    await waitFor(() =>
      expect(server.calls.filter((call) => call.path === '/api/admin/alerts').length).toBeGreaterThan(alertGets),
    );
  });

  it("a Supervisor's Blocked Scan gives a building cover (facility-0048)", async () => {
    const server = serve({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 74, userId: 31, personName: 'หัวหน้าทดสอบ', buildingId: 2, buildingCode: 'B', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T04:05:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'สแกนนอกตึก (ถูกบล็อก) · หัวหน้าทดสอบ (หัวหน้า)' });
    expect(within(item).getByText('ป้าย BR01-01 ห้องประชุม ของ BR01 Area ตึกอื่น · ไม่ได้บันทึกงาน')).toBeInTheDocument();

    await user.click(within(item).getByRole('button', { name: 'ให้ตรวจแทนตึกนี้กะนี้' }));

    expect(await screen.findByText('มอบหมาย หัวหน้าทดสอบ ตรวจแทนตึก B แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([
      { userId: 31, buildingId: 2, shiftDate: '2026-10-15', shift: 'Day', blockedScanId: 302 },
    ]);
  });

  it("this shift's covers can be cancelled before the shift ends", async () => {
    const server = serve({ 'DELETE /api/admin/cover-assignments/71': () => ({ status: 204 }) });
    const { user } = await renderApp('/admin/alerts');

    const covers = await screen.findByRole('region', { name: 'คนทำแทนกะนี้ (1)' });
    expect(within(covers).getByRole('listitem')).toHaveTextContent(
      'แม่บ้านที่อื่น ทำแทน AR02 · แทน แม่บ้านป่วย · Admin มอบหมาย 07:10',
    );

    await user.click(within(covers).getByRole('button', { name: 'ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02' }));

    expect(await screen.findByText('ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02 แล้ว')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.method === 'DELETE').map((call) => call.path)).toEqual([
      '/api/admin/cover-assignments/71',
    ]);
  });

  it('ยกเลิก waits for the answer: the cover form stays while the assignment is in flight', async () => {
    const server = serve({ 'POST /api/admin/cover-assignments': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/alerts');
    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });
    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });

    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    const cancel = within(form).getByRole('button', { name: 'ยกเลิก' });
    await waitFor(() => expect(cancel).toBeDisabled());
    await user.click(cancel);
    expect(screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' })).toBeInTheDocument();
    expect(posts(server)).toHaveLength(1);
  });

  it('a board of another shift closes an open cover form', async () => {
    let night = false;
    serve({
      'GET /api/admin/attendance': () => ({
        status: 200,
        body: night ? attendanceBoard(undefined, { shift: 'Night' }) : attendanceBoard(),
      }),
    });
    const { user, store } = await renderApp('/admin/alerts');
    await waitFor(() => expect(() => hub.latest()).not.toThrow());
    await apiIdle(store);
    const empty = await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' });
    await user.click(within(empty).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    expect(screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' })).toBeInTheDocument();

    night = true;
    act(() => hub.latest().push('AttendanceRecorded', { areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'มอบหมายคนทำแทน · AR12' })).not.toBeInTheDocument());
  });
});
