import { act, screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, boardEvent, candidates, workingRow } from '../../test/liveFixtures';
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
  await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' }));
  const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
  await user.type(within(form).getByRole('textbox', { name: 'เวลาใหม่ เข้างาน' }), '07:05');
  await user.click(within(form).getByRole('radio', { name: 'มือถือแบตหมด' }));
  await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));
}

const time = (form: HTMLElement, event: string) => within(form).getByRole('textbox', { name: `เวลาใหม่ ${event}` });
const detail = (form: HTMLElement) => within(form).getByRole('textbox', { name: /^รายละเอียดเพิ่มเติม/ });

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

    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    expect(within(form).getByText('AR12 Area สิบสอง · แม่บ้านลา · กะเช้า 07:00–19:00 15/10/2026')).toBeInTheDocument();
    await user.type(time(form, 'เข้างาน'), '07:05');
    await user.click(within(form).getByRole('radio', { name: 'มือถือแบตหมด' }));
    await user.type(detail(form), 'หัวหน้ายืนยัน');
    expect(within(form).getByText('จะแก้ 1 ช่อง: เข้างาน ไม่ได้ลง → 07:05')).toBeInTheDocument();
    const before = boardCalls(server).length;

    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานของ แม่บ้านลา แล้ว')).toBeInTheDocument();
    expect(puts(server).map((call) => call.body)).toEqual([
      { userId: 26, shiftDate: '2026-10-15', shift: 'Day', eventType: 'ShiftIn', time: '07:05', reason: 'มือถือแบตหมด · หัวหน้ายืนยัน' },
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

  it('แก้เวลา shows all four times: what is recorded, a box for each, and the one not scanned first tinted', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    expect(within(form).getByRole('heading', { name: 'แก้เวลา' })).toHaveFocus();
    const rows = within(form).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[1].textContent)).toEqual([
      '06:52',
      'ไม่ได้ลง',
      'ไม่ได้ลง',
      'ไม่ได้ลง',
    ]);
    expect(time(form, 'เข้างาน')).toHaveValue('06:52');
    expect(time(form, 'ออกพัก')).toHaveValue('');
    expect(rows[1].className).toMatch(/accent-subtle/);
    expect(rows[2].className).not.toMatch(/accent-subtle/);

    await user.clear(time(form, 'เข้างาน'));
    await user.type(time(form, 'เข้างาน'), '06:58');
    expect(within(form).getByText('จะแก้ 1 ช่อง: เข้างาน 06:52 → 06:58')).toBeInTheDocument();
    await user.click(within(form).getByRole('button', { name: 'คืนค่าเข้างาน' }));
    expect(time(form, 'เข้างาน')).toHaveValue('06:52');
    expect(within(form).getByText('ยังไม่ได้แก้เวลาช่องไหน')).toBeInTheDocument();

    await user.click(within(form).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('several times are saved in one press, one request each, with the same reason', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });

    await user.type(time(form, 'ออกพัก'), '09:30');
    await user.type(time(form, 'กลับจากพัก'), '09:45');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (2 ช่อง)' }));

    expect(await screen.findByText('บันทึกเวลาออกพัก และกลับจากพักของ แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(puts(server).map((call) => call.body)).toEqual([
      { userId: 21, shiftDate: '2026-10-15', shift: 'Day', eventType: 'BreakOut', time: '09:30', reason: 'ไม่มีอินเทอร์เน็ต' },
      { userId: 21, shiftDate: '2026-10-15', shift: 'Day', eventType: 'BreakIn', time: '09:45', reason: 'ไม่มีอินเทอร์เน็ต' },
    ]);
  });

  it('a time the server refuses for its order is sent again after the others', async () => {
    let breakInMoved = false;
    const server = serve({
      'PUT /api/admin/attendance': (request) => {
        const body = request.body as { eventType: string };
        if (body.eventType === 'BreakIn') {
          breakInMoved = true;
          return savedEvent;
        }
        return breakInMoved ? savedEvent : { status: 400, body: { message: 'เวลาต้องเรียงตามลำดับ' } };
      },
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });

    await user.type(time(form, 'ออกพัก'), '09:30');
    await user.type(time(form, 'กลับจากพัก'), '09:45');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (2 ช่อง)' }));

    expect(await screen.findByText('บันทึกเวลาออกพัก และกลับจากพักของ แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(puts(server).map((call) => (call.body as { eventType: string }).eventType)).toEqual(['BreakOut', 'BreakIn', 'BreakOut']);
  });

  it('a send that stops half way says what was saved and keeps the form', async () => {
    serve({
      'PUT /api/admin/attendance': (request) =>
        (request.body as { eventType: string }).eventType === 'BreakOut'
          ? savedEvent
          : { status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง' } },
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });

    await user.type(time(form, 'ออกพัก'), '09:30');
    await user.type(time(form, 'กลับจากพัก'), '09:45');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (2 ช่อง)' }));

    const failed = await within(form).findByRole('alert');
    expect(failed).toHaveTextContent('บันทึกไม่สำเร็จ');
    expect(failed).toHaveTextContent('บันทึกแล้ว: ออกพัก · ยังไม่ได้บันทึก: กลับจากพัก');
    // What was saved is no longer a change: pressing again sends only the rest
    expect(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' })).toBeEnabled();
  });

  it('Escape closes the form without saving, as ยกเลิก does', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'เข้างาน'), '07:05');

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(puts(server)).toHaveLength(0);
  });

  it('closing the form puts the keyboard back on the row it was opened from', async () => {
    serve();
    const { user } = await renderApp('/admin/attendance');
    const opener = await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' });
    await user.click(opener);

    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'ยกเลิก' }));

    expect(opener).toHaveFocus();
  });

  it("a wrong time, the wrong order or no reason is refused before sending; the server's refusal keeps the draft", async () => {
    const server = serve({
      'PUT /api/admin/attendance': () => ({ status: 400, body: { message: 'เวลาที่ใส่ยังมาไม่ถึง' } }),
    });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    const save = () => within(form).getByRole('button', { name: /^บันทึกการแก้ไข/ });

    await user.click(save());
    expect(within(form).getByRole('alert')).toHaveTextContent('ยังบันทึกไม่ได้ มี 2 จุดที่ต้องแก้');
    expect(within(form).getByRole('alert')).toHaveTextContent('ยังไม่ได้แก้เวลาช่องไหน · เลือกเหตุผลอย่างน้อย 1 ข้อ');
    expect(within(form).getByRole('radiogroup', { name: 'เหตุผล' })).toHaveAccessibleDescription('เลือกเหตุผลอย่างน้อย 1 ข้อ');

    await user.type(time(form, 'ออกพัก'), '7.05');
    expect(within(form).getByText('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05')).toBeInTheDocument();
    expect(time(form, 'ออกพัก')).toHaveAttribute('aria-invalid', 'true');
    expect(time(form, 'ออกพัก')).toHaveAccessibleDescription('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');

    await user.clear(time(form, 'ออกพัก'));
    await user.type(time(form, 'ออกพัก'), '06:30');
    expect(within(form).getByText('ต้องหลังเวลาเข้างาน (06:52)')).toBeInTheDocument();

    await user.clear(time(form, 'ออกพัก'));
    await user.type(time(form, 'ออกพัก'), '18:30');
    await user.click(within(form).getByRole('radio', { name: 'อื่น ๆ' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('เลือก "อื่น ๆ" ต้องเขียนรายละเอียด');
    await user.click(save());
    expect(puts(server)).toHaveLength(0);
    expect(detail(form)).toHaveAccessibleDescription('เลือก "อื่น ๆ" ต้องเขียนรายละเอียด');

    await user.type(detail(form), 'ลืมสแกน');
    expect(detail(form)).not.toHaveAttribute('aria-describedby');
    await user.click(save());

    expect(await within(form).findByText('เวลาที่ใส่ยังมาไม่ถึง')).toBeInTheDocument();
    expect(puts(server).map((call) => (call.body as { reason: string }).reason)).toEqual(['อื่น ๆ · ลืมสแกน']);
    expect(time(form, 'ออกพัก')).toHaveValue('18:30');
    expect(detail(form)).toHaveValue('ลืมสแกน');
  });

  it('one save at a time: the button waits for the answer', async () => {
    const server = serve({ 'PUT /api/admin/attendance': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/attendance');
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'เข้างาน'), '07:05');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));

    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    const waiting = await within(form).findByRole('button', { name: 'กำลังบันทึก…' });
    expect(waiting).toBeDisabled();
    await user.click(waiting);
    expect(puts(server)).toHaveLength(1);
  });

  it('no button for an Area with no regular Cleaner', async () => {
    serve();
    await renderApp('/admin/attendance');
    await screen.findByText('แสดง 1–6 จาก 6 พื้นที่');

    const row = screen.getAllByRole('row').find((r) => within(r).queryByRole('rowheader')?.textContent?.startsWith('AR09 '));
    expect(row).toBeDefined();
    // nobody to correct, so no time button; the Area has nobody, so "หาคนทำแทน" is the only one
    expect(within(row as HTMLElement).getAllByRole('button').map((button) => button.textContent)).toEqual(['หาคนทำแทน']);
  });

  it('หาคนทำแทน opens the cover Drawer on that Area: pick the Cleaner, see the summary, assign (the design file D2)', async () => {
    const server = serve({
      'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }),
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: { id: 72, userId: 21, personName: 'แม่บ้านทดสอบ', areaId: 12, areaCode: 'AR12', shiftDate: '2026-10-15', shift: 'Day', assignedAt: '2026-10-15T03:20:00Z' },
      }),
    });
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'หาคนทำแทน AR12' }));
    const form = screen.getByRole('dialog', { name: 'มอบหมายทำแทน' });
    expect(within(form).getByRole('combobox', { name: /^วันที่/ })).toHaveValue('2026-10-15');
    expect(within(form).getByRole('button', { name: 'เช้า 07:00–19:00' })).toHaveAttribute('aria-pressed', 'true');
    const areas = within(form).getByRole('radiogroup', { name: '2. พื้นที่ที่ต้องมีคนทำแทน' });
    expect(within(areas).getByRole('radio', { name: /^AR12 Area สิบสอง/ })).toBeChecked();
    expect(within(areas).getByRole('radio', { name: /^AR12 Area สิบสอง/ }).closest('label')).toHaveTextContent('ยังไม่มีคน');

    await within(form).findByRole('radiogroup', { name: 'แม่บ้านที่จะทำแทน' });
    await user.click(within(form).getByRole('radio', { name: /^แม่บ้านทดสอบ/ }));
    expect(within(form).getByText(/จะดูแล 2 พื้นที่ในกะเช้า พฤ\. 15\/10\/2026/)).toBeInTheDocument();
    await user.click(within(form).getByRole('button', { name: 'มอบหมาย' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านทดสอบ ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(
      server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/cover-assignments').map((call) => call.body),
    ).toEqual([{ userId: 21, areaId: 12, shiftDate: '2026-10-15', shift: 'Day' }]);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('the main มอบหมายทำแทน opens the Drawer with no Area; a later shift lists its Areas and asks its candidates', async () => {
    const server = serve({ 'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }) });
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'มอบหมายทำแทน' }));
    const form = screen.getByRole('dialog', { name: 'มอบหมายทำแทน' });
    expect(within(form).getByText('เลือกพื้นที่ก่อน แล้วรายชื่อแม่บ้านที่ทำแทนได้จะขึ้นที่นี่')).toBeInTheDocument();
    expect(within(form).getByRole('button', { name: 'มอบหมาย' })).toBeDisabled();

    await user.selectOptions(within(form).getByRole('combobox', { name: /^วันที่/ }), '2026-10-16');
    await user.click(await within(form).findByRole('radio', { name: /^AR09/ }));
    await within(form).findByRole('radiogroup', { name: 'แม่บ้านที่จะทำแทน' });

    expect(server.calls.filter((call) => call.path === '/api/admin/cover-assignments/candidates').map((call) => call.search)).toEqual([
      '?areaId=9&shiftDate=2026-10-16&shift=Day',
    ]);
  });

  it('an Area that already has a cover is warned about: the new one is added, the old one is cancelled from the list', async () => {
    const server = serve({
      'GET /api/admin/cover-assignments/candidates': () => ({ status: 200, body: candidates }),
      'DELETE /api/admin/cover-assignments/71': () => ({ status: 204 }),
    });
    const { user } = await renderApp('/admin/attendance');

    await user.click(await screen.findByRole('button', { name: 'มอบหมายทำแทน' }));
    const form = screen.getByRole('dialog', { name: 'มอบหมายทำแทน' });
    await user.click(await within(form).findByRole('radio', { name: /^AR02/ }));

    expect(within(form).getByText('AR02 มีคนทำแทนอยู่แล้ว')).toBeInTheDocument();
    expect(within(form).getByText(/พื้นที่นี้จะมีคนทำแทน 2 คน/)).toBeInTheDocument();
    expect(within(form).getByRole('heading', { name: 'ทำแทนที่มอบหมายไว้แล้วกะนี้ · 1' })).toBeInTheDocument();
    await user.click(within(form).getByRole('button', { name: 'ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR02' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ยกเลิกการทำแทน' }));

    await waitFor(() =>
      expect(server.calls.some((call) => call.method === 'DELETE' && call.path === '/api/admin/cover-assignments/71')).toBe(true),
    );
  });

  it('a link from a missing Shift-Out alert opens the form on that Cleaner and event; closing it leaves the address clean', async () => {
    serve({}, nightBoard);
    const { user, router } = await renderApp('/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut');

    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    expect(within(form).getByText('AR01 Area ทดลอง · แม่บ้านกะดึก · กะดึก 19:00–07:00 14/10/2026')).toBeInTheDocument();
    // The row the link asks for is tinted, so the Admin sees where to type
    const shiftOut = within(form).getAllByRole('row')[4];
    expect(shiftOut).toHaveTextContent(/^เลิกงานไม่ได้ลง/);
    expect(shiftOut.className).toMatch(/accent-subtle/);

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
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านลา' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'เข้างาน'), '07:05');
    await user.click(within(form).getByRole('radio', { name: 'มือถือแบตหมด' }));

    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    const cancel = within(form).getByRole('button', { name: 'ยกเลิก' });
    await waitFor(() => expect(cancel).toBeDisabled());
    await user.click(cancel);
    expect(screen.getByRole('dialog', { name: 'แก้เวลา' })).toBeInTheDocument();
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
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'เลิกงาน'), '18:55');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));

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
    expect(screen.getByRole('dialog', { name: 'แก้เวลา' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'กะดึก 19:00–07:00' }));

    expect(await screen.findByText('กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
