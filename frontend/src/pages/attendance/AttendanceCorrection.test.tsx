import { act, screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { attendanceBoardApi, type AttendanceBoard, type BoardRow } from '../../shared/api/attendanceBoardApi';
import { baseApi } from '../../shared/api/baseApi';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, attendanceBoard, boardEvent, workingRow } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { overviewDetailFixture } from '../../test/overviewFixtures';

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

  it('shows a numbered slot on the board and lets Admin correct a second-break scan', async () => {
    const row: BoardRow = {
      ...workingRow,
      events: [
        ...workingRow.events,
        boardEvent(5001, 'Break2Out', '2026-10-15T09:00:00Z', { source: 'SupervisorAdd' }),
      ],
      entry: 'entered',
      entryEvidence: [{ kind: 'transfer-arrival', recordedAt: '2026-10-15T06:05:00Z', transferId: 5 }],
      responsibility: { userId: 21, employeeId: '650001', displayName: 'แม่บ้านทดสอบ', state: 'confirmed' },
    };
    const server = serve(
      { 'PUT /api/admin/attendance': () => savedEvent },
      attendanceBoard([row], { lifecycleVersion: 'Numbered6' }),
    );
    const { user } = await renderApp('/admin/attendance');

    expect(await screen.findByRole('columnheader', { name: 'ออกพัก 2' })).toBeVisible();
    expect(screen.queryByRole('columnheader', { name: /^ออกพัก$/ })).not.toBeInTheDocument();
    expect(screen.getByText('หัวหน้าเติมเวลา')).toBeVisible();
    expect(screen.getByText('ยืนยันเข้าพื้นที่ใหม่ 13:05')).toBeVisible();
    expect(screen.getByText(/ยืนยันถึงพื้นที่ใหม่แล้ว/)).toBeVisible();

    await user.click(screen.getByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    expect(time(form, 'กลับจากพัก 2')).toHaveValue('');
    expect(time(form, 'ออกพัก 2')).toHaveValue('16:00');
    await user.clear(time(form, 'ออกพัก 2'));
    await user.type(time(form, 'ออกพัก 2'), '16:05');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    expect(await screen.findByText('บันทึกเวลาออกพัก 2ของ แม่บ้านทดสอบ แล้ว')).toBeVisible();
    expect(puts(server).map((call) => call.body)).toEqual([
      expect.objectContaining({ eventType: 'Break2Out', time: '16:05', reason: 'ลืมลงเวลา' }),
    ]);
  });

  it('corrects the transferred Cleaner independently of the resident of their destination', async () => {
    const transferred = { id: 61, employeeId: '650061', displayName: 'แม่บ้านย้ายเข้า' };
    const row = {
      ...workingRow,
      additionalCleaners: [{
        cleaner: transferred,
        events: [boardEvent(5002, 'ShiftIn', '2026-10-15T00:10:00Z')],
      }],
    };
    const server = serve({ 'PUT /api/admin/attendance': () => savedEvent }, attendanceBoard([row]));
    const { user } = await renderApp('/admin/attendance');

    expect(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'แก้เวลา แม่บ้านย้ายเข้า' }));
    const form = screen.getByRole('dialog', { name: 'แก้เวลา' });
    expect(time(form, 'เข้างาน')).toHaveValue('07:10');
    await user.type(time(form, 'ออกพัก'), '10:05');
    await user.click(within(form).getByRole('radio', { name: 'ลืมลงเวลา' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    expect(await screen.findByText('บันทึกเวลาออกพักของ แม่บ้านย้ายเข้า แล้ว')).toBeVisible();
    expect(puts(server).map((call) => call.body)).toEqual([
      expect.objectContaining({ userId: transferred.id, eventType: 'BreakOut', time: '10:05' }),
    ]);
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

  it('no button for an Area with no regular Cleaner; empty row links to /admin/schedules (facility-0225)', async () => {
    serve();
    await renderApp('/admin/attendance');
    await screen.findByText('แสดง 1–6 จาก 6 พื้นที่');

    const row = screen.getAllByRole('row').find((r) => within(r).queryByRole('rowheader')?.textContent?.startsWith('AR09 '));
    expect(row).toBeDefined();
    expect(within(row as HTMLElement).queryAllByRole('button')).toHaveLength(0);
    expect(within(row as HTMLElement).getByRole('link', { name: 'ไปจัดคนแทนที่ตารางงาน' })).toHaveAttribute('href', '/admin/schedules');
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
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

const scopedUrl = '/admin/attendance?source=overview&date=2026-11-04&shift=Day&building=1&group=notEntered&page=2&areaId=91&view=attendance';
const scopedRow: BoardRow = {
  ...workingRow,
  areaId: 91,
  areaCode: 'AR91',
  areaName: 'พื้นที่ทดลอง 91',
  events: [boardEvent(1001, 'ShiftIn', '2026-11-04T00:05:00Z', { areaId: 91 })],
};
const reassignedRow: BoardRow = {
  ...scopedRow,
  cleaner: { id: 64, employeeId: '650064', displayName: 'แม่บ้านคนใหม่' },
  events: [],
};

function serveScoped(routes: FakeRoutes = {}) {
  return serve({
    'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
    'GET /api/admin/overview/areas/91': () => ({ status: 200, body: overviewDetailFixture() }),
    'GET /api/admin/attendance': () => ({
      status: 200, body: attendanceBoard([scopedRow], { shiftDate: '2026-11-04' }),
    }),
    ...routes,
  });
}

describe('Overview Attendance actions', () => {
  it('blocks an origin Shift-In when a confirmed transfer is corrected from the destination Area', async () => {
    const row = {
      ...scopedRow,
      events: [
        boardEvent(1001, 'ShiftIn', '2026-11-04T00:05:00Z', { areaId: 92 }),
        boardEvent(1002, 'BreakOut', '2026-11-04T02:30:00Z', { areaId: 91 }),
      ],
    };
    const server = serveScoped({
      'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard([row], { shiftDate: '2026-11-04' }) }),
      'PUT /api/admin/attendance': () => savedEvent,
    });
    const { user } = await renderApp(scopedUrl);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    const originTime = time(form, 'เข้างาน');
    expect(originTime).toBeDisabled();
    expect(originTime).toHaveValue('');
    expect(within(form).getByText('ลงไว้ที่พื้นที่อื่น')).toBeVisible();

    await user.clear(time(form, 'ออกพัก'));
    await user.type(time(form, 'ออกพัก'), '09:45');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));
    expect(await screen.findByText(/บันทึกเวลาออกพักของ/)).toBeVisible();
    expect(puts(server).map(({ body }) => body)).toEqual([expect.objectContaining({ eventType: 'BreakOut' })]);
  });

  it('refuses a save if its event is newly recorded in another Area before the fresh check', async () => {
    let row = scopedRow;
    const server = serveScoped({
      'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard([row], { shiftDate: '2026-11-04' }) }),
      'PUT /api/admin/attendance': () => savedEvent,
    });
    const { user } = await renderApp(scopedUrl);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'ออกพัก'), '09:45');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    row = {
      ...row,
      events: [...row.events, boardEvent(1002, 'BreakOut', '2026-11-04T02:30:00Z', { areaId: 92 })],
    };
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));
    await waitFor(() => expect(form).not.toBeInTheDocument());
    expect(puts(server)).toHaveLength(0);
  });

  it('opens the additional Cleaner’s own scoped correction and closes it when that Cleaner leaves', async () => {
    const incoming = { id: 74, employeeId: '650074', displayName: 'แม่บ้านย้ายมา' };
    let row: BoardRow = {
      ...scopedRow,
      additionalCleaners: [{
        cleaner: incoming,
        events: [boardEvent(1005, 'BreakOut', '2026-11-04T03:30:00Z', { areaId: 91 })],
      }],
    };
    const server = serveScoped({
      'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard([row], { shiftDate: '2026-11-04' }) }),
      'PUT /api/admin/attendance': () => savedEvent,
    });
    const { user, store } = await renderApp(scopedUrl);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านย้ายมา' }));
    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    expect(form).toHaveTextContent('แม่บ้านย้ายมา');
    expect(time(form, 'ออกพัก')).toHaveValue('10:30');
    await user.type(time(form, 'กลับจากพัก'), '10:45');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));
    expect(await screen.findByText(/บันทึกเวลากลับจากพักของ แม่บ้านย้ายมา/)).toBeVisible();
    expect(puts(server).map(({ body }) => body)).toEqual([expect.objectContaining({ userId: incoming.id })]);

    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านย้ายมา' }));
    const reopened = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    row = scopedRow;
    act(() => { store.dispatch(baseApi.internalActions.onFocus()); });
    await waitFor(() => expect(reopened).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'แก้เวลา แม่บ้านย้ายมา' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).toBeEnabled();
  });

  it('waits for a successful board read when mounting with a cached row for an old Cleaner', async () => {
    let hold = false;
    let release = () => {};
    const server = serveScoped({
      'GET /api/admin/attendance': () => {
        const fresh = { status: 200, body: attendanceBoard([reassignedRow], { shiftDate: '2026-11-04' }) };
        if (!hold) return { status: 200, body: attendanceBoard([scopedRow], { shiftDate: '2026-11-04' }) };
        return new Promise<typeof fresh>((resolve) => { release = () => resolve(fresh); });
      },
    });
    const { router, store } = await renderApp('/admin/attendance');
    expect(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).toBeVisible();
    await apiIdle(store);
    const cached = attendanceBoardApi.endpoints.getAttendanceBoard.select({})(store.getState()).data;
    expect(cached?.rows[0]?.cleaner?.id).toBe(scopedRow.cleaner?.id);
    const priorCalls = boardCalls(server).length;
    hold = true;
    await act(() => router.navigate(scopedUrl));
    expect(await screen.findByText('กำลังตรวจข้อมูลกะปัจจุบัน...')).toBeVisible();
    await waitFor(() => expect(boardCalls(server).length).toBeGreaterThan(priorCalls));
    expect(screen.queryByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();

    await act(async () => release());
    expect(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านคนใหม่' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).not.toBeInTheDocument();
  });

  it('unlocks scoped actions when a focus refresh succeeds after mount verification failed', async () => {
    let recovered = false;
    const server = serveScoped({
      'GET /api/admin/attendance': () => recovered
        ? { status: 200, body: attendanceBoard([scopedRow], { shiftDate: '2026-11-04' }) }
        : { status: 503, body: { message: 'โหลดข้อมูลกะปัจจุบันไม่สำเร็จ' } },
    });
    const { store } = await renderApp(scopedUrl);
    expect(await screen.findByRole('alert')).toHaveTextContent('โหลดข้อมูลกะปัจจุบันไม่สำเร็จ');
    await apiIdle(store);
    expect(screen.queryByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).not.toBeInTheDocument();

    const failedReads = boardCalls(server).length;
    recovered = true;
    act(() => { store.dispatch(baseApi.internalActions.onFocus()); });

    expect(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('กำลังตรวจข้อมูลกะปัจจุบัน...')).not.toBeInTheDocument();
    expect(boardCalls(server).length).toBeGreaterThan(failedReads);
  });

  it('writes using the exact current board row and refreshes both that board and the Overview detail', async () => {
    const server = serveScoped({ 'PUT /api/admin/attendance': () => savedEvent });
    const { router, user } = await renderApp(scopedUrl);
    const detailCalls = () => server.calls.filter((call) => call.path === '/api/admin/overview/areas/91');
    // Wait for the initial read and the connection's catch-up before checking the save's invalidation.
    await waitFor(() => expect(detailCalls()).toHaveLength(2));
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'ออกพัก'), '09:30');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    const boardBefore = boardCalls(server).length;
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));

    expect(await screen.findByText(/บันทึกเวลาออกพักของ แม่บ้านทดสอบ แล้ว/)).toBeVisible();
    expect(puts(server).map(({ body }) => body)).toEqual([
      expect.objectContaining({ userId: scopedRow.cleaner?.id, shiftDate: '2026-11-04', shift: 'Day', eventType: 'BreakOut' }),
    ]);
    expect(boardCalls(server).length).toBeGreaterThan(boardBefore);
    await waitFor(() => expect(detailCalls()).toHaveLength(3));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(scopedUrl);
  });

  it.each([false, true])('checks an unseen reassignment before saving with a pending older board read: %s', async (withPendingRead) => {
    let row = scopedRow;
    let holdNextRead = false;
    let releaseRead = () => {};
    const server = serveScoped({
      'GET /api/admin/attendance': () => {
        const answer = { status: 200, body: attendanceBoard([row], { shiftDate: '2026-11-04' }) };
        if (!holdNextRead) return answer;
        holdNextRead = false;
        return new Promise<typeof answer>((resolve) => { releaseRead = () => resolve(answer); });
      },
      'PUT /api/admin/attendance': () => savedEvent,
    });
    const { store, user } = await renderApp(scopedUrl);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const form = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(form, 'ออกพัก'), '09:30');
    await user.click(within(form).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    if (withPendingRead) {
      holdNextRead = true;
      act(() => { store.dispatch(baseApi.internalActions.onFocus()); });
      await waitFor(() => expect(holdNextRead).toBe(false));
    }
    row = reassignedRow;
    await user.click(within(form).getByRole('button', { name: 'บันทึกการแก้ไข (1 ช่อง)' }));
    if (withPendingRead) await act(async () => releaseRead());

    expect(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านคนใหม่' })).toBeEnabled();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(puts(server)).toHaveLength(0);
  });

  it.each([1, 2])('keeps a new form open after a delayed correction with %s changed fields', async (fieldCount) => {
    let row = scopedRow;
    let release = () => {};
    const held = new Promise<typeof savedEvent>((resolve) => { release = () => resolve(savedEvent); });
    const server = serveScoped({
      'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard([row], { shiftDate: '2026-11-04' }) }),
      'PUT /api/admin/attendance': () => held,
    });
    const { store, router, user } = await renderApp(scopedUrl);
    await user.click(await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านทดสอบ' }));
    const old = await screen.findByRole('dialog', { name: 'แก้เวลา' });
    await user.type(time(old, 'ออกพัก'), '09:30');
    if (fieldCount === 2) await user.type(time(old, 'กลับจากพัก'), '09:45');
    await user.click(within(old).getByRole('radio', { name: 'ไม่มีอินเทอร์เน็ต' }));
    await user.click(within(old).getByRole('button', { name: `บันทึกการแก้ไข (${fieldCount} ช่อง)` }));
    await waitFor(() => expect(puts(server)).toHaveLength(1));

    row = reassignedRow;
    act(() => { store.dispatch(baseApi.internalActions.onFocus()); });
    const replacement = await screen.findByRole('button', { name: 'แก้เวลา แม่บ้านคนใหม่' });
    expect(old).not.toBeInTheDocument();
    await user.click(replacement);
    expect(await screen.findByRole('dialog', { name: 'แก้เวลา' })).toHaveTextContent('แม่บ้านคนใหม่');

    await act(async () => release());
    await apiIdle(store);
    expect(screen.getByRole('dialog', { name: 'แก้เวลา' })).toHaveTextContent('แม่บ้านคนใหม่');
    expect(puts(server).map(({ body }) => body)).toEqual([
      expect.objectContaining({ userId: scopedRow.cleaner?.id, eventType: 'BreakOut' }),
    ]);
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(scopedUrl);
  });

  it.each([
    attendanceBoard([workingRow], { shiftDate: '2026-11-04' }),
    attendanceBoard([scopedRow], { shiftDate: '2026-11-05' }),
    attendanceBoard([scopedRow], { shiftDate: '2026-11-04', shift: 'Night' }),
  ])('keeps actions unavailable when the exact Area or clock slot is absent', async (board) => {
    serveScoped({ 'GET /api/admin/attendance': () => ({ status: 200, body: board }) });
    await renderApp(scopedUrl);
    expect(await screen.findByText('ไม่พบพื้นที่นี้ในกะปัจจุบัน กรุณาตรวจข้อมูลอีกครั้ง')).toBeVisible();
    expect(screen.queryByRole('button', { name: /แก้เวลา/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'มอบหมายทำแทน' })).not.toBeInTheDocument();
  });
});
