import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Alert } from '../../shared/api/alertListApi';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { alerts, attendanceBoard, attendanceGpsAlert } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo } from '../../test/moreInfo';

function serve(routes: FakeRoutes = {}, list: () => Alert[] = () => alerts) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/alerts': () => ({ status: 200, body: list() }),
    'GET /api/admin/attendance': () => ({ status: 200, body: attendanceBoard() }),
    ...routes,
  });
}

const alertGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/alerts');
const alertTable = () => screen.getByRole('region', { name: 'เตือนจากการสแกน' });
/** The rows of the alert table, without its head: each row's first words are its time and who. */
const rows = () => within(alertTable()).queryAllByRole('row').slice(1);
const farRow = () => screen.findByRole('row', { name: /ลงเวลาไกลป้าย/ });

describe('AlertsPage (D3, facility-0035, 0041, 0051)', () => {
  // The fixtures' today: Thursday 15/10/2026, 12:00 Thai time
  beforeEach(() => vi.setSystemTime(new Date('2026-10-15T05:00:00Z')));
  afterEach(() => vi.useRealTimers());

  it("opens on today's refused scans; each tab counts the alerts nobody has talked about", async () => {
    serve();
    await renderApp('/admin/alerts');

    const blocked = await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    expect(screen.getByRole('tab', { name: /^สแกนผิดพื้นที่/ })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: /^สแกนผิดพื้นที่/ })).toHaveTextContent('สแกนผิดพื้นที่1');
    expect(screen.getByRole('tab', { name: /^ไกลป้าย/ })).toHaveTextContent('ไกลป้าย2');
    expect(screen.getByRole('tab', { name: /^ลืมลงเวลา/ })).toHaveTextContent('ลืมลงเวลา1');
    expect(screen.getByRole('button', { name: 'วันนี้ 15/10' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'ยังไม่ได้คุย (1)' })).toHaveAttribute('aria-pressed', 'true');
    expect(rows()).toHaveLength(1);
    expect(within(blocked).getByText('09:10')).toBeInTheDocument();
    expect(within(blocked).getByText('แม่บ้าน · AR03')).toBeInTheDocument();
    expect(within(blocked).getByText('สแกนป้าย AR12-02 ห้องน้ำหญิง ของ AR12 Area สิบสอง · AR12 ยังไม่มีคน')).toBeInTheDocument();
    expect(within(blocked).getByText('ไม่บันทึก')).toBeInTheDocument();
  });

  it('ไกลป้าย puts the three GPS flags together; คุยแล้ว and เมื่อวาน show the one closed yesterday', async () => {
    serve();
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });

    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    expect(rows().map((row) => within(row).getAllByRole('cell')[1].textContent)).toEqual([
      'หัวหน้าทดสอบหัวหน้า',
      'แม่บ้านทดสอบแม่บ้าน · AR01',
    ]);
    const inspection = screen.getByRole('row', { name: /ตรวจงานไกลป้าย/ });
    expect(
      within(inspection).getByText('ป้าย AR01-03 จุดทิ้งขยะ · AR01 Area ทดลอง · ห่าง 62 ม. (รัศมี 50 ม.) · ความแม่นยำ ±25 ม.'),
    ).toBeInTheDocument();
    expect(within(inspection).getByText('บันทึก ติดธง')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (0)' }));
    expect(screen.getByText('ยังไม่มีรายการที่คุยแล้ว')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'เมื่อวาน 14/10' }));
    const closed = screen.getByRole('row', { name: /ส่งงานไกลป้าย/ });
    expect(within(closed).getByText('คุยแล้ว เมื่อวาน 16:30')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ยังไม่ได้คุย (0)' }));
    expect(screen.getByText('ไม่มีรายการที่ต้องคุยในแท็บนี้')).toBeInTheDocument();
  });

  it('closing an alert sends the note, moves it to คุยแล้ว, and the menu count goes down', async () => {
    let closed = false;
    const server = serve(
      {
        'POST /api/admin/alerts/review': () => {
          closed = true;
          return { status: 204 };
        },
      },
      () => alerts.map((a) => (closed && a === attendanceGpsAlert ? { ...a, reviewed: true, reviewedAt: '2026-10-15T03:30:00Z' } : a)),
    );
    const { user } = await renderApp('/admin/alerts');
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(await within(menu).findByRole('link', { name: 'รายการที่ขึ้นเตือน (4)' })).toHaveAttribute('aria-current', 'page');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    const item = await farRow();

    const toggle = within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' });
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await user.type(screen.getByRole('textbox', { name: 'หมายเหตุการคุยกับ แม่บ้านทดสอบ (ไม่บังคับ)' }), 'GPS ในอาคารเพี้ยน');
    expect(screen.getByText('17 / 500')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดเตือน' }));

    expect(await screen.findByText('ปิดเตือนลงเวลาไกลป้ายของ แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({
      kind: 'AttendanceGps',
      recordId: 401,
      note: 'GPS ในอาคารเพี้ยน',
    });
    await waitFor(() => expect(screen.queryByRole('row', { name: /ลงเวลาไกลป้าย/ })).not.toBeInTheDocument());
    await waitFor(() => expect(within(menu).getByRole('link', { name: 'รายการที่ขึ้นเตือน (3)' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (1)' }));
    expect(within(await farRow()).getByText('คุยแล้ว 10:30')).toBeInTheDocument();
  });

  it('a note left empty is not sent; ยกเลิก closes the note without sending', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => ({ status: 204 }) });
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    const item = await farRow();

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(screen.getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดเตือน' }));

    await waitFor(() =>
      expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({ kind: 'AttendanceGps', recordId: 401 }),
    );
  });

  it('only one note is open at a time', async () => {
    serve();
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));

    await user.click(within(await farRow()).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(screen.getByRole('row', { name: /ตรวจงานไกลป้าย/ })).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));

    expect(screen.getAllByRole('textbox')).toHaveLength(1);
    expect(screen.getByRole('textbox', { name: 'หมายเหตุการคุยกับ หัวหน้าทดสอบ (ไม่บังคับ)' })).toBeInTheDocument();
  });

  it('a missing Shift-Out has no close button; it links to the attendance page with that shift and the form open', async () => {
    serve();
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ลืมลงเวลา/ }));
    const item = screen.getByRole('row', { name: /ลืมลงเวลา/ });

    expect(within(item).getByText('ไม่ได้ลงเวลาเลิกงาน · กะดึก 14/10/2026 · AR01 Area ทดลอง')).toBeInTheDocument();
    expect(within(item).getByText('รอเติมเวลา')).toBeInTheDocument();
    expect(within(item).queryByRole('button', { name: 'คุยแล้ว ปิดเตือน' })).not.toBeInTheDocument();
    expect(within(item).getByRole('link', { name: 'เติมเวลาเลิกงาน' })).toHaveAttribute(
      'href',
      '/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut',
    );
    expect(screen.getByText(/ปิดเตือนด้วยมือไม่ได้ ต้องเติมเวลาที่หน้าการเข้างาน/)).toBeInTheDocument();
  });

  it('a refusal (another Admin closed it first) is shown at the top, and the list is asked again', async () => {
    const server = serve({
      'POST /api/admin/alerts/review': () => ({ status: 409, body: { message: 'ปิดเตือนนี้ไปแล้ว' } }),
    });
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    const item = await farRow();
    const before = alertGets(server).length;

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดเตือน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ปิดเตือนนี้ไปแล้ว');
    await waitFor(() => expect(alertGets(server).length).toBeGreaterThan(before));
  });

  it('a refusal does not go by itself: it stays until ✕', async () => {
    serve({
      'POST /api/admin/alerts/review': () => ({ status: 409, body: { message: 'ปิดเตือนนี้ไปแล้ว' } }),
    });
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date('2026-10-15T05:00:00Z') });
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    await user.click(within(await farRow()).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดเตือน' }));
    const refusal = await screen.findByText('ปิดเตือนนี้ไปแล้ว');

    act(() => vi.advanceTimersByTime(60_000));
    expect(refusal).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ปิดข้อความ' }));
    expect(refusal).not.toBeInTheDocument();
  });

  it('one close at a time: the buttons wait for the answer, ยกเลิก too', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/alerts');
    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    await user.click(screen.getByRole('tab', { name: /^ไกลป้าย/ }));
    const item = await farRow();
    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    const confirm = screen.getByRole('button', { name: 'ยืนยัน ปิดเตือน' });

    await user.click(confirm);
    await waitFor(() => expect(confirm).toBeDisabled());
    expect(screen.getByRole('button', { name: 'ยกเลิก' })).toBeDisabled();
    expect(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' })).toBeDisabled();
    await user.click(confirm);

    expect(server.calls.filter((call) => call.path === '/api/admin/alerts/review')).toHaveLength(1);
  });

  it('the page notes wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/alerts');

    await screen.findByRole('row', { name: /สแกนผิดพื้นที่/ });
    expectBehindMoreInfo(
      'ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน',
      'เกี่ยวกับหน้านี้',
    );
    expectBehindMoreInfo('รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า', 'เกี่ยวกับหน้านี้');
  });
});
