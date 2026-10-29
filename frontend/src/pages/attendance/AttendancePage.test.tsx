import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, buildings, managedBuildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, emptyRow, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo, expectInView } from '../../test/moreInfo';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/attendance': (request) => ({
      status: 200,
      body: request.search.includes('shift=Night') ? attendanceBoard([workingRow], { shift: 'Night' }) : attendanceBoard(),
    }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
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

/** The row's cells after the Area: Cleaner, status, the four times. */
const cells = (code: string) => within(rowOf(code)).getAllByRole('cell').slice(0, 6).map((cell) => cell.textContent);

/** The bar under the table, once the board is in. */
const loaded = (shown = 'แสดง 1–6 จาก 6 พื้นที่') => screen.findByText(shown);

describe('AttendancePage (D2, facility-0027, 0050)', () => {
  it("shows the clock's shift: Areas with nobody first, each Cleaner's four times, covers and flags", async () => {
    serve();
    await renderApp('/admin/attendance');

    await loaded();
    expect(screen.getByLabelText('วันที่')).toHaveValue('2026-10-15');
    expect(screen.getByRole('button', { name: 'กะเช้า 07:00–19:00' })).toHaveAttribute('aria-pressed', 'true');
    expect(
      within(screen.getByRole('list', { name: 'สรุปกะนี้' }))
        .getAllByRole('listitem')
        .map((tile) => tile.textContent),
    ).toEqual([
      'พื้นที่ที่ต้องมีคน6',
      'มีคนแล้ว3',
      'ยังไม่มีคน2',
      'มีคนทำแทน1',
      'พักเบรก1',
    ]);
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'AR09 Area เก้าตึก A · ตึกทดลอง',
      'AR12 Area สิบสองตึก B · ตึกข้างเคียง',
      'AR01 Area ทดลองตึก A · ตึกทดลอง',
      'AR02 Office ทดลองตึก B · ตึกข้างเคียง',
      'AR05 Area ห้าตึก A · ตึกทดลอง',
      'AR07 Area เจ็ดตึก A · ตึกทดลอง',
    ]);
    expect(cells('AR09')[0]).toBe('ไม่มีแม่บ้านประจำ');
    expect(cells('AR12')).toEqual(['แม่บ้านลายังไม่ลงเวลา', 'ยังไม่มีคน', '–', '–', '–', '–']);
    // A scan outside its sign's radius carries the distance beside its time (facility-0035)
    expect(cells('AR01')).toEqual(['แม่บ้านทดสอบ', 'ทำงานอยู่', '06:5285 ม.', '–', '–', '–']);
    expect(within(rowOf('AR01')).getByRole('img', { name: 'ไกลป้าย 85 ม.' })).toBeInTheDocument();
    expect(within(rowOf('AR02')).getByText('ทำแทน')).toBeInTheDocument();
    expect(within(rowOf('AR02')).getByText('พื้นที่ของตัวเอง AR03 · แอดมินมอบหมาย 07:10 · เข้างาน 06:50')).toBeInTheDocument();
    expect(within(rowOf('AR12')).getAllByRole('button').map((button) => button.textContent)).toEqual(['แก้เวลา', 'หาคนทำแทน']);
    expect(cells('AR02')[1]).toBe('มีคนทำแทน');
    // the [แก้] mark comes before the time (DESIGN_SPEC §5 Table); its name is แก้โดยแอดมิน
    expect(cells('AR05')[2]).toBe('แก้07:02');
    expect(within(rowOf('AR05')).getByRole('img', { name: 'แก้โดยแอดมิน' })).toBeInTheDocument();
    expect(cells('AR07')).toEqual(['แม่บ้านพัก', 'พักเบรก', '06:55', '09:30', '–', '–']);
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('กะดึก shows the night of the same date, and the address keeps it', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await loaded();

    await user.click(screen.getByRole('button', { name: 'กะดึก 19:00–07:00' }));

    expect(await screen.findByText('แสดง 1–1 จาก 1 พื้นที่')).toBeInTheDocument();
    expect(router.state.location.search).toBe('?date=2026-10-15&shift=Night');
    expect(boardCalls(server).at(-1)?.search).toBe('?shiftDate=2026-10-15&shift=Night');
    expect(screen.getByRole('button', { name: 'กะดึก 19:00–07:00' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('a date picked in the calendar opens that date at once, on the shift shown', async () => {
    serve();
    const { user, router } = await renderApp('/admin/attendance');
    await loaded();

    fireEvent.change(screen.getByLabelText('วันที่'), { target: { value: '2026-10-14' } });
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Day');

    await user.click(screen.getByRole('button', { name: 'กะดึก 19:00–07:00' }));
    expect(router.state.location.search).toBe('?date=2026-10-15&shift=Night');
  });

  it('shows 25 Areas a page; ก่อนหน้า and ถัดไป move, and a search starts again at the first page', async () => {
    const many = Array.from({ length: 30 }, (_, at) => ({
      ...emptyRow,
      areaId: 100 + at,
      areaCode: `AR${100 + at}`,
      areaName: `Area ${100 + at}`,
    }));
    serve({ 'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard(many) }) });
    const { user } = await renderApp('/admin/attendance');

    await loaded('แสดง 1–25 จาก 30 พื้นที่');
    expect(screen.getAllByRole('rowheader')).toHaveLength(25);
    expect(screen.getByRole('button', { name: 'ก่อนหน้า' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    expect(screen.getByText('แสดง 26–30 จาก 30 พื้นที่')).toBeInTheDocument();
    expect(screen.getAllByRole('rowheader')[0]).toHaveTextContent(/^AR125 /);
    expect(screen.getByRole('button', { name: 'ถัดไป' })).toBeDisabled();

    await user.type(screen.getByRole('searchbox', { name: 'ค้นหาพื้นที่หรือแม่บ้าน' }), 'Area 1');
    expect(screen.getByText('แสดง 1–25 จาก 30 พื้นที่')).toBeInTheDocument();
  });

  it('the building picker asks the server for that building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/attendance');
    await screen.findByRole('option', { name: 'B · ตึกข้างเคียง' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B · ตึกข้างเคียง');

    expect(router.state.location.search).toBe('?building=2');
    await waitFor(() => expect(boardCalls(server).at(-1)?.search).toBe('?buildingId=2'));
  });

  it('search keeps the Areas whose name, Cleaner or cover matches', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');
    await loaded();

    await user.type(screen.getByRole('searchbox', { name: 'ค้นหาพื้นที่หรือแม่บ้าน' }), 'ที่อื่น');
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['AR02 Office ทดลองตึก B · ตึกข้างเคียง']);

    await user.clear(screen.getByRole('searchbox', { name: 'ค้นหาพื้นที่หรือแม่บ้าน' }));
    await user.type(screen.getByRole('searchbox', { name: 'ค้นหาพื้นที่หรือแม่บ้าน' }), 'ไม่มีชื่อนี้');
    expect(screen.getByText('ไม่พบพื้นที่ที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('a check-in pushed for the shift on screen asks again; one for another shift does not (facility-0027 rule 5)', async () => {
    const server = serve();
    const { store } = await renderApp('/admin/attendance');
    await waitFor(() => expect(screen.getByRole('status')).toBeEmptyDOMElement()); // the hub is live
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
    await loaded();

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute(
      'href',
      '/admin/export?from=2026-10-15&to=2026-10-15&building=2',
    );
  });

  it('the one-line note is in view; the longer notes wait behind ℹ (the design file)', async () => {
    serve();
    await renderApp('/admin/attendance');

    await loaded();
    expectInView('ไม่ใช่ระบบลงเวลาทำงาน และไม่คิดสาย');
    expectBehindMoreInfo(/^เรียงพื้นที่ที่ยังไม่มีคนขึ้นก่อน/, 'เกี่ยวกับหน้านี้');
    expectBehindMoreInfo(/^ลืมลงเวลาเลิกงาน/, 'เกี่ยวกับหน้านี้');
  });
});
