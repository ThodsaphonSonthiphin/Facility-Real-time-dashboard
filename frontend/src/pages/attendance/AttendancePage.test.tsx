import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, buildings, managedBuildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, emptyRow, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { overviewDetailFixture } from '../../test/overviewFixtures';

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
  it('keeps historical Area actions, transfer arrival, and assignment separate from attendance', async () => {
    const base = overviewDetailFixture();
    const server = serve({
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas/91': () => ({
        status: 200,
        body: {
          ...base,
          context: { ...base.context, isClockCurrent: false, phase: 'closed' },
          entryEvidence: [{ areaId: 91, shiftDate: base.context.shiftDate, shift: 'Day', kind: 'transfer-arrival', recordedAt: '2026-11-04T05:05:00Z', transferId: 77 }],
          currentResponsibility: { userId: 201, areaId: 91, state: 'confirmed', lastConfirmedAreaId: 91, confirmedAt: '2026-11-04T05:05:00Z' },
          facts: {
            ...base.facts,
            attendance: [
              { id: 1, areaId: 92, userId: 201, event: 'ShiftIn', occurredAt: '2026-11-04T00:00:00Z', edited: false },
              { id: 2, areaId: 91, userId: 201, event: 'Break1In', occurredAt: '2026-11-04T06:30:00Z', edited: true, source: 'SupervisorAdd', reason: 'ปรับเวลาใหม่',
                addition: { reason: 'สแกนไม่ติด', recordedAt: '2026-11-04T06:35:00Z' },
                edit: { reason: 'ปรับเวลาใหม่', recordedAt: '2026-11-04T06:45:00Z' } },
              { id: 6, areaId: 91, userId: 201, event: 'ShiftOut', occurredAt: '2026-11-04T08:05:00Z', edited: false },
              { id: 7, areaId: 91, userId: 202, event: 'ShiftIn', occurredAt: '2026-11-04T00:05:00Z', edited: false },
            ],
            people: [...base.facts.people, { id: 202, displayName: 'แม่บ้านอีกคน' }],
            attendanceGaps: [
              { id: 3, areaId: 91, userId: 201, event: 'Break1Out', reason: 'ข้ามช่วงพัก' },
              { id: 4, areaId: 91, userId: 201, event: 'Break1In', reason: 'ช่องว่างที่เติมแล้ว' },
            ],
            attendanceLeaves: [
              { id: 5, userId: 201, startsAt: '2026-11-04T08:00:00Z', endsAt: '2026-11-04T12:00:00Z' },
              { id: 8, userId: 202, startsAt: '2026-11-04T00:00:00Z', endsAt: '2026-11-04T02:00:00Z' },
            ],
            attendanceLeaveEffects: [
              { userId: 201, event: 'Break2Out', kind: 'exempt' },
              { userId: 202, event: 'ShiftIn', kind: 'check-in-conflict' },
            ],
          },
        },
      }),
    });
    await renderApp('/admin/attendance?source=overview&date=2026-11-04&shift=Day&building=1&group=entered&page=2&areaId=91&view=attendance');

    const evidence = await screen.findByRole('region', { name: 'การเข้าพื้นที่' });
    expect(within(evidence).getByText(/ยืนยันถึงพื้นที่ใหม่/)).toHaveTextContent('รายการย้าย #77');
    const actions = screen.getByRole('region', { name: 'เวลาที่บันทึก' });
    expect(within(actions).getByText(/กลับจากพักเที่ยง/)).toBeVisible();
    const addition = within(actions).getByText(/หัวหน้างานเติมเวลา/);
    expect(addition).toHaveTextContent('สแกนไม่ติด');
    expect(addition).not.toHaveTextContent('ปรับเวลาใหม่');
    expect(within(actions).getByText(/แอดมินแก้เวลา/)).toHaveTextContent('ปรับเวลาใหม่');
    expect(within(actions).getByText(/· พักเที่ยง ·/)).toHaveTextContent('— (ข้ามการสแกน)');
    expect(within(actions).queryByText(/ช่องว่างที่เติมแล้ว|พักบ่าย|พื้นที่ #92|แม่บ้านทดลอง 91 · เข้างาน ·/)).not.toBeInTheDocument();
    const leave = within(screen.getByRole('region', { name: 'การลาของกะนี้' }));
    expect(leave.getByText(/แม่บ้านทดลอง 91 · ช่วงลาที่บันทึก/)).toBeVisible();
    const exempt = leave.getByText(/พักบ่าย · ไม่ต้องลงเวลาเนื่องจากลา/);
    expect(exempt).toBeVisible();
    expect(exempt.querySelector('time')).toBeNull();
    expect(leave.getByText(/แม่บ้านอีกคน · เข้างาน · ลงเวลาในช่วงลาที่บันทึก/)).toBeVisible();
    expect(within(actions).queryByText(/ไม่ต้องลงเวลาเนื่องจากลา/)).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'การย้ายพื้นที่' })).toHaveTextContent('แม่บ้านทดลอง 91');
    expect(screen.getByRole('region', { name: 'คนทำแทน' })).toHaveTextContent('มอบหมาย');
    expect(screen.getByText('ดูย้อนหลังเท่านั้น')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
    expect(boardCalls(server)).toHaveLength(0);
  });

  it('keeps the receiving window read-only even when the API marks the selected slot as clock-current', async () => {
    const base = overviewDetailFixture();
    const server = serve({
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas/91': () => ({
        status: 200, body: { ...base, context: { ...base.context, phase: 'receiving', isClockCurrent: true } },
      }),
    });
    await renderApp('/admin/attendance?source=overview&date=2026-11-04&shift=Day&areaId=91&view=attendance');

    expect(await screen.findByText('ดูย้อนหลังเท่านั้น')).toBeVisible();
    expect(screen.getByText(/ยังรับข้อมูลถึง/)).toBeVisible();
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
    expect(boardCalls(server)).toHaveLength(0);
  });

  it.each(['source=other&source=overview&areaId=91', 'source=overview&date=2026-11-04&shift=Day&areaId=0&view=attendance'])(
    'rejects malformed Overview context without starting a board or detail read', async (search) => {
      const server = serve({ 'GET /api/admin/alerts': () => ({ status: 200, body: [] }) });
      await renderApp(`/admin/attendance?${search}`);
      expect(await screen.findByRole('alert')).toBeVisible();
      expect(screen.getByRole('link', { name: 'กลับภาพรวม' })).toBeVisible();
      expect(boardCalls(server)).toHaveLength(0);
      expect(server.calls.some((call) => call.path.startsWith('/api/admin/overview/areas/'))).toBe(false);
    },
  );

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
    expect(within(rowOf('AR12')).getAllByRole('button').map((button) => button.textContent)).toEqual(['แก้เวลา']);
    expect(within(rowOf('AR12')).getByRole('link', { name: 'ไปจัดคนแทนที่ตารางงาน' })).toHaveAttribute('href', '/admin/schedules');
    expect(within(rowOf('AR09')).getByRole('link', { name: 'ไปจัดคนแทนที่ตารางงาน' })).toHaveAttribute('href', '/admin/schedules');
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
    expect(cells('AR02')[1]).toBe('มีคนทำแทน');
    // the [แก้] mark comes before the time (DESIGN_SPEC §5 Table); its name is แก้โดยแอดมิน
    expect(cells('AR05')[2]).toBe('แก้07:02');
    expect(within(rowOf('AR05')).getByRole('img', { name: 'แก้โดยแอดมิน' })).toBeInTheDocument();
    expect(cells('AR07')).toEqual(['แม่บ้านพัก', 'พักเบรก', '06:55', '09:30', '–', '–']);
    // facility-0059: no link to a sign page; empty rows only link to /admin/schedules (facility-0225)
    expect(
      within(screen.getByRole('table'))
        .queryAllByRole('link')
        .every((link) => link.getAttribute('href') === '/admin/schedules'),
    ).toBe(true);
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
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Night');
  });

  it('a date or shift picked while the last pick is still loading keeps that pick (E2E, 2026-11-02)', async () => {
    let release = () => {};
    const held = new Promise<void>((resolve) => (release = resolve));
    serve({
      'GET /api/admin/attendance': async (request) => {
        if (request.search.includes('shift=Night')) {
          await held;
          return { status: 200, body: attendanceBoard([workingRow], { shift: 'Night' }) };
        }
        return { status: 200, body: attendanceBoard() };
      },
    });
    const { user, router } = await renderApp('/admin/attendance');
    await loaded();

    await user.click(screen.getByRole('button', { name: 'กะดึก 19:00–07:00' }));
    // The Day board is still on screen while the Night one loads
    fireEvent.change(screen.getByLabelText('วันที่'), { target: { value: '2026-10-14' } });
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Night');

    await user.click(screen.getByRole('button', { name: 'กะเช้า 07:00–19:00' }));
    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Day');
    act(() => release());
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
    expect(hub.latest().serverTimeoutInMilliseconds).toBe(10_000); // an Admin page hears of a dead link sooner (facility-0107)

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

  it('keeps Export only in the Admin menu (facility-0100)', async () => {
    serve();
    await renderApp('/admin/attendance?building=2');
    await loaded();

    expect(within(screen.getByRole('main')).queryByRole('link', { name: 'ส่งออก Excel' })).not.toBeInTheDocument();
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getAllByRole('link', { name: 'ส่งออก Excel' })).toHaveLength(1);
    expect(within(menu).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('href', '/admin/export');
  });

  it('keeps page header concise without redundant MoreInfo notes', async () => {
    serve();
    await renderApp('/admin/attendance');

    await loaded();
    expect(screen.queryByText('เกี่ยวกับหน้านี้')).not.toBeInTheDocument();
  });
});
