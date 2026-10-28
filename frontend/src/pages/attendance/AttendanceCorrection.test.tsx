import { act, screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, boardEvent, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}, board: AttendanceBoard = attendanceBoard()) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/attendance': () => ({ status: 200, body: board }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

const savedEvent = { status: 200, body: boardEvent(1100, 'ShiftIn', '2026-10-15T00:05:00Z', { source: 'AdminAdd' }) };
const boardCalls = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/attendance' && call.method === 'GET');
const puts = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.method === 'PUT');

/** Adds แม่บ้านลา's missing Shift-In at 07:05 through the form. */
async function addShiftIn(user: UserEvent) {
  await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
  const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
  await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
  await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'มือถือแบตหมด หัวหน้ายืนยัน');
  await user.click(within(form).getByRole('button', { name: 'บันทึก' }));
}

/** Last night's shift of AR01: แม่บ้านกะดึก checked in 18:55 and has no Shift-Out. */
const nightRow: BoardRow = {
  areaId: 1,
  areaCode: 'AR01',
  areaName: 'Area ทดลอง',
  buildingCode: 'A',
  cleaner: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก' },
  state: 'Working',
  events: [boardEvent(1200, 'ShiftIn', '2026-10-14T11:55:00Z')],
  covers: [],
};
const nightBoard = attendanceBoard([nightRow], { shiftDate: '2026-10-14', shift: 'Night', shiftEnded: true });

describe('attendance corrections (D2, facility-0051)', () => {
  it('adds a missing Shift-In with a reason, then asks for the board again', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    expect(within(form).getByText('กะเช้า 15/10/2026')).toBeInTheDocument();
    expect(within(form).getByRole('combobox', { name: 'รายการ' })).toHaveValue('ShiftIn');
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'มือถือแบตหมด หัวหน้ายืนยัน');
    const before = boardCalls(server).length;

    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว')).toBeInTheDocument();
    expect(puts(server).map((call) => call.body)).toEqual([
      { userId: 26, shiftDate: '2026-10-15', shift: 'Day', eventType: 'ShiftIn', time: '07:05', reason: 'มือถือแบตหมด หัวหน้ายืนยัน' },
    ]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(boardCalls(server).length).toBeGreaterThan(before));
  });

  it('the saved notice floats over the page and ✕ closes it at once', async () => {
    serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');

    await addShiftIn(user);

    expect(await screen.findByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว')).toHaveAttribute('role', 'status');
    await user.click(screen.getByRole('button', { name: 'ปิดข้อความ' }));
    expect(screen.queryByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว')).not.toBeInTheDocument();
  });

  it('the saved notice goes by itself after 5 s, but not while the pointer is on it', async () => {
    serve({ 'PUT /api/admin/attendance': () => savedEvent });
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const { user } = await renderApp('/admin/attendance');
      await addShiftIn(user);
      const notice = await screen.findByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว');

      await user.hover(notice);
      act(() => vi.advanceTimersByTime(8_000));
      expect(notice).toBeInTheDocument();

      await user.unhover(notice);
      act(() => vi.advanceTimersByTime(4_000));
      expect(notice).toBeInTheDocument();
      act(() => vi.advanceTimersByTime(1_500));
      expect(notice).not.toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('แก้เวลา opens on the first event not scanned, shows each current time, and a chosen event brings its time', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านทดสอบ · AR01' });
    expect(within(form).getByRole('heading', { name: 'เพิ่ม/แก้เวลา · แม่บ้านทดสอบ · AR01' })).toHaveFocus();
    const event= within(form).getByRole('combobox', { name: 'รายการ' });
    const time = within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' });

    expect(event).toHaveValue('BreakOut');
    expect(time).toHaveValue('');
    expect(within(event).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'เข้างาน (ตอนนี้ 06:52)',
      'ออกพัก (ยังไม่มี)',
      'กลับจากพัก (ยังไม่มี)',
      'เลิกงาน (ยังไม่มี)',
    ]);

    await user.selectOptions(event, 'ShiftIn');
    expect(time).toHaveValue('06:52');

    await user.click(within(form).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('Escape closes the form without saving, as ยกเลิก does', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(puts(server)).toHaveLength(0);
  });

  it('closing the form puts the keyboard back on the row it was opened from', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');
    const opener = await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' });
    await user.click(opener);

    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'ยกเลิก' }));

    expect(opener).toHaveFocus();
  });

  it("a wrong time or no reason is refused before sending; the server's refusal keeps the draft", async () => {
    const server = serve({
      'PUT /api/admin/attendance': () => ({ status: 400, body: { message: 'เวลาที่ใส่ยังมาไม่ถึง' } }),
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    const time = within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' });
    const reason = within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' });
    const save = within(form).getByRole('button', { name: 'บันทึก' });

    await user.type(time, '7.05');
    await user.click(save);
    expect(within(form).getByRole('alert')).toHaveTextContent('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');

    await user.clear(time);
    await user.type(time, '18:30');
    await user.click(save);
    expect(within(form).getByRole('alert')).toHaveTextContent('ต้องกรอกเหตุผล');
    expect(puts(server)).toHaveLength(0);

    await user.type(reason, 'ลืมสแกน');
    await user.click(save);

    expect(await within(form).findByText('เวลาที่ใส่ยังมาไม่ถึง')).toBeInTheDocument();
    expect(time).toHaveValue('18:30');
    expect(reason).toHaveValue('ลืมสแกน');
  });

  it('one save at a time: the button waits for the answer', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'ลืมสแกน');

    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    const waiting = await within(form).findByRole('button', { name: 'กำลังบันทึก...' });
    expect(waiting).toBeDisabled();
    await user.click(waiting);
    expect(puts(server)).toHaveLength(1);
  });

  it('no button for an Area with no regular Cleaner', async () => {
    serve();
    await renderApp('/admin/attendance');
    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');

    const row = screen.getAllByRole('row').find((r) => within(r).queryByRole('rowheader')?.textContent?.startsWith('AR09 '));
    expect(row).toBeDefined();
    expect(within(row as HTMLElement).queryByRole('button')).not.toBeInTheDocument();
  });

  it('a link from a missing Shift-Out alert opens the form on that Cleaner and event; closing it leaves the address clean', async () => {
    serve({}, nightBoard);
    const { user, router } = await renderApp('/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut');

    const form = await screen.findByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านกะดึก · AR01' });
    expect(within(form).getByText('กะดึก 14/10/2026')).toBeInTheDocument();
    expect(within(form).getByRole('combobox', { name: 'รายการ' })).toHaveValue('ShiftOut');

    await user.click(within(form).getByRole('button', { name: 'ยกเลิก' }));

    expect(router.state.location.search).toBe('?date=2026-10-14&shift=Night');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('a link for a Cleaner not on the board says so', async () => {
    serve({}, nightBoard);
    await renderApp('/admin/attendance?date=2026-10-14&shift=Night&fill=99&event=ShiftOut');

    expect(await screen.findByText('ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้ายพื้นที่หรือปิดใช้งานแล้ว')).toBeInTheDocument();
  });

  it('ยกเลิก waits for the answer: it is disabled while the save is in flight', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'เพิ่มเวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' });
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '07:05');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'มือถือแบตหมด');

    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    const cancel = within(form).getByRole('button', { name: 'ยกเลิก' });
    await waitFor(() => expect(cancel).toBeDisabled());
    await user.click(cancel);
    expect(screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านลา · AR12' })).toBeInTheDocument();
    expect(puts(server)).toHaveLength(1);
  });

  it('a board that flips under an open form closes it: the draft cannot be saved to the next shift\'s Cleaner', async () => {
    const dayBoard = attendanceBoard([workingRow]);
    const nightFlip = attendanceBoard(
      [{ ...workingRow, cleaner: { id: 22, employeeId: '650002', displayName: 'แม่บ้านกะดึก' }, events: [] }],
      { shift: 'Night' },
    );
    let night = false;
    const server = serve(
      {
        'GET /api/admin/attendance': () => ({ status: 200, body: night ? nightFlip : dayBoard }),
        'PUT /api/admin/attendance': () => savedEvent,
      },
      dayBoard,
    );
    const { user, store } = await renderApp('/admin/attendance');
    await waitFor(() => expect(screen.getByRole('status')).toBeEmptyDOMElement()); // the hub is live
    await apiIdle(store);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านทดสอบ · AR01' });
    await user.selectOptions(within(form).getByRole('combobox', { name: 'รายการ' }), 'ShiftOut');
    await user.type(within(form).getByRole('textbox', { name: 'เวลา (ชม.:นาที)' }), '18:55');
    await user.type(within(form).getByRole('textbox', { name: 'เหตุผล (ต้องกรอก)' }), 'ลืมสแกนออก');

    night = true;
    act(() => hub.latest().push('AttendanceRecorded', { areaId: 1, shiftDate: '2026-10-15', shift: 'Day' }));

    expect(await screen.findByText('กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน')).toHaveAttribute('role', 'status');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(puts(server)).toHaveLength(0);
  });

  it('pressing กะดึก with a form open closes it with the notice', async () => {
    serve({
      'GET /api/admin/attendance': (request) => ({
        status: 200,
        body: request.search.includes('Night') ? nightBoard : attendanceBoard(),
      }),
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    expect(screen.getByRole('dialog', { name: 'เพิ่ม/แก้เวลา · แม่บ้านทดสอบ · AR01' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'กะดึก' }));

    expect(await screen.findByText('กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
