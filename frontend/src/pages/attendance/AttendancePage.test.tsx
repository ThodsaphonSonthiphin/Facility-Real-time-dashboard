import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/attendance': (request) => ({
      status: 200,
      body: request.search.includes('shift=Night') ? attendanceBoard([workingRow], { shift: 'Night' }) : attendanceBoard(),
    }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const boardCalls = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/attendance');

/** The table row whose Area cell starts with this code. */
function rowOf(code: string): HTMLElement {
  const row = screen
    .getAllByRole('row')
    .find((candidate) => within(candidate).queryByRole('rowheader')?.textContent?.startsWith(`${code} `));
  if (!row) {
    throw new Error(`no row for ${code}`);
  }
  return row;
}

/** The row's cells after the Area: Cleaner, status, the four times, the flag. */
const cells = (code: string) => within(rowOf(code)).getAllByRole('cell').slice(0, 7).map((cell) => cell.textContent);

describe('AttendancePage (D2, facility-0027, 0050)', () => {
  it("shows the clock's shift: Areas with nobody first, each Cleaner's four times, covers and flags", async () => {
    serve();
    await renderApp('/admin/attendance');

    expect(await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(
      within(screen.getByRole('list', { name: 'สรุปกะนี้' }))
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual([
      'พื้นที่ที่ต้องมีคนกะนี้6ไม่รวมพื้นที่ที่ปิดกะนี้',
      'มีคนแล้ว3รวมพักเบรก',
      'ยังไม่มีคน2ต้องหาคนทำแทน',
      'มีคนทำแทน1แอดมินมอบหมายเฉพาะกะนี้',
    ]);
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'AR09 Area เก้าตึก A',
      'AR12 Area สิบสองตึก B',
      'AR01 Area ทดลองตึก A',
      'AR02 Office ทดลองตึก B',
      'AR05 Area ห้าตึก A',
      'AR07 Area เจ็ดตึก A',
    ]);
    expect(cells('AR09')[0]).toBe('ไม่มีแม่บ้านประจำ');
    expect(cells('AR12')).toEqual(['แม่บ้านลายังไม่ลงเวลา', 'ยังไม่มีคน', '–', '–', '–', '–', '']);
    expect(cells('AR01')).toEqual(['แม่บ้านทดสอบ', 'ทำงานอยู่', '06:52', '–', '–', '–', 'เข้างาน ห่างป้าย 85 ม.']);
    expect(within(rowOf('AR02')).getByText('ทำแทน: แม่บ้านที่อื่น (AR03) · แอดมินมอบหมาย 07:10 · เข้างาน 06:50')).toBeInTheDocument();
    expect(cells('AR02')[1]).toBe('มีคนทำแทน');
    expect(cells('AR05')[2]).toBe('07:02แก้โดยแอดมิน');
    expect(cells('AR07')).toEqual(['แม่บ้านพัก', 'พักเบรก', '06:55', '09:30', '–', '–', '']);
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('กะดึก shows the night of the same date, and the address keeps it', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    await user.click(screen.getByRole('button', { name: 'กะดึก' }));

    expect(await screen.findByText('15/10/2026 · กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(router.state.location.search).toBe('?date=2026-10-15&shift=Night');
    expect(boardCalls(server).at(-1)?.search).toBe('?shiftDate=2026-10-15&shift=Night');
    expect(screen.getByRole('button', { name: 'กะดึก' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('a typed date opens that date; a wrong one is refused before asking; กะปัจจุบัน goes back to the clock', async () => {
    serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');
    const date = screen.getByRole('textbox', { name: 'วันที่' });

    await user.type(date, '31/02/2026');
    await user.click(screen.getByRole('button', { name: 'ดูวันที่นี้' }));
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(router.state.location.search).toBe('');

    await user.clear(date);
    await user.type(date, '14/10/2026');
    await user.click(screen.getByRole('button', { name: 'ดูวันที่นี้' }));
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Day');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'กะปัจจุบัน' }));
    expect(router.state.location.search).toBe('');
  });

  it('the building picker asks the server for that building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByRole('option', { name: 'B' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');

    expect(router.state.location.search).toBe('?building=2');
    await waitFor(() => expect(boardCalls(server).at(-1)?.search).toBe('?buildingId=2'));
  });

  it('search keeps the Areas whose name, Cleaner or cover matches', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    await user.type(screen.getByRole('searchbox', { name: 'ค้นหา' }), 'ที่อื่น');
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['AR02 Office ทดลองตึก B']);

    await user.clear(screen.getByRole('searchbox', { name: 'ค้นหา' }));
    await user.type(screen.getByRole('searchbox', { name: 'ค้นหา' }), 'ไม่มีชื่อนี้');
    expect(screen.getByText('ไม่พบพื้นที่ที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('a check-in pushed for the shift on screen asks again; one for another shift does not (facility-0027 rule 5)', async () => {
    const server = serve();
    const { store } = await renderApp('/admin/attendance');
    expect(await screen.findByText('SignalR Live')).toBeInTheDocument();
    await waitFor(() => expect(boardCalls(server)).toHaveLength(2)); // the hub's first start asks again
    await apiIdle(store);

    act(() => hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-14', shift: 'Night' }));
    act(() => hub.latest().push('AttendanceRecorded', { areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }));

    await waitFor(() => expect(boardCalls(server)).toHaveLength(3));
    await apiIdle(store);
    expect(boardCalls(server)).toHaveLength(3);
  });

  it('asks the server every 30 s (facility-0061)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const server = serve();
      const { store } = await renderApp('/admin/attendance');
      await waitFor(() => expect(boardCalls(server)).toHaveLength(2));
      await apiIdle(store);

      await act(() => vi.advanceTimersByTimeAsync(30_000));

      await waitFor(() => expect(boardCalls(server)).toHaveLength(3));
    } finally {
      vi.useRealTimers();
    }
  });

  it('"ส่งออก Excel" opens the export of the shift date and building on screen', async () => {
    serve();
    await renderApp('/admin/attendance?building=2');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute(
      'href',
      '/admin/export?from=2026-10-15&to=2026-10-15&building=2',
    );
  });
});
