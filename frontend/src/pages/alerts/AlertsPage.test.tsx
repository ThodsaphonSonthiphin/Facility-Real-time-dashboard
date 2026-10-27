import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
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
const items = () => screen.queryAllByRole('article').map((item) => item.getAttribute('aria-label'));

describe('AlertsPage (D3, facility-0035, 0041, 0051)', () => {
  it('lists the alerts nobody has talked about yet, newest first, with who, what and where', async () => {
    serve();
    await renderApp('/admin/alerts');

    await screen.findAllByRole('article');
    expect(items()).toEqual([
      'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)',
      'สแกนผิดพื้นที่ · แม่บ้านที่อื่น',
      'ไม่ได้ลงเวลาเลิกงาน · แม่บ้านกะดึก',
      'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ',
    ]);
    const inspection = screen.getByRole('article', { name: 'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)' });
    expect(within(inspection).getByText('15/10 10:15')).toBeInTheDocument();
    expect(
      within(inspection).getByText('ป้าย AR01-03 จุดทิ้งขยะ · AR01 Area ทดลอง · ห่าง 62 ม. (รัศมี 50 ม.) · ความแม่นยำ ±25 ม.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ยังไม่ได้คุย (4)' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('คุยแล้ว and ทั้งหมด; the type picker puts the three GPS flags together', async () => {
    serve();
    const { user } = await renderApp('/admin/alerts');
    await screen.findAllByRole('article');

    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (1)' }));
    expect(items()).toEqual(['ส่งงานไกลจากป้าย · แม่บ้านทดสอบ']);
    expect(screen.getByText('คุยแล้ว 14/10 16:30')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ทั้งหมด' }));
    expect(items()).toHaveLength(5);

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'ไกลจากป้าย');
    expect(items()).toEqual([
      'ตรวจงานไกลจากป้าย · หัวหน้าทดสอบ (หัวหน้า)',
      'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ',
      'ส่งงานไกลจากป้าย · แม่บ้านทดสอบ',
    ]);

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'ไม่ได้ลงเวลา');
    await user.click(screen.getByRole('button', { name: 'คุยแล้ว (1)' }));
    expect(screen.getByText('ไม่มีรายการ')).toBeInTheDocument();
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
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.type(within(item).getByRole('textbox', { name: 'หมายเหตุ (ไม่บังคับ)' }), 'GPS ในอาคารเพี้ยน');
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    expect(await screen.findByText('ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({
      kind: 'AttendanceGps',
      recordId: 401,
      note: 'GPS ในอาคารเพี้ยน',
    });
    await waitFor(() => expect(screen.queryByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' })).not.toBeInTheDocument());
    await waitFor(() => expect(within(menu).getByRole('link', { name: 'รายการที่ขึ้นเตือน (3)' })).toBeInTheDocument());
  });

  it('a note left empty is not sent; ยกเลิก closes the note without sending', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => ({ status: 204 }) });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยกเลิก' }));
    expect(within(item).queryByRole('textbox')).not.toBeInTheDocument();

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    await waitFor(() =>
      expect(server.calls.find((call) => call.path === '/api/admin/alerts/review')?.body).toEqual({ kind: 'AttendanceGps', recordId: 401 }),
    );
  });

  it('a missing Shift-Out has no close button; it links to the attendance page with that shift and the form open', async () => {
    serve();
    await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ไม่ได้ลงเวลาเลิกงาน · แม่บ้านกะดึก' });

    expect(within(item).getByText('กะดึก 14/10/2026 · AR01 Area ทดลอง')).toBeInTheDocument();
    expect(within(item).queryByRole('button', { name: 'คุยแล้ว ปิดเตือน' })).not.toBeInTheDocument();
    expect(within(item).getByRole('link', { name: 'เติมเวลาเลิกงาน' })).toHaveAttribute(
      'href',
      '/admin/attendance?date=2026-10-14&shift=Night&fill=22&event=ShiftOut',
    );
  });

  it('a refusal (another Admin closed it first) is shown at the top, and the list is asked again', async () => {
    const server = serve({
      'POST /api/admin/alerts/review': () => ({ status: 409, body: { message: 'ปิดเตือนนี้ไปแล้ว' } }),
    });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
    const before = alertGets(server).length;

    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ปิดเตือนนี้ไปแล้ว');
    await waitFor(() => expect(alertGets(server).length).toBeGreaterThan(before));
  });

  it('a refusal does not go by itself: it stays until ✕', async () => {
    serve({
      'POST /api/admin/alerts/review': () => ({ status: 409, body: { message: 'ปิดเตือนนี้ไปแล้ว' } }),
    });
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const { user } = await renderApp('/admin/alerts');
      const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
      await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
      await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));
      const refusal = await screen.findByText('ปิดเตือนนี้ไปแล้ว');

      act(() => vi.advanceTimersByTime(60_000));
      expect(refusal).toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: 'ปิดข้อความ' }));
      expect(refusal).not.toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('one close at a time: the button waits for the answer', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));
    const confirm = within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' });

    await user.click(confirm);
    await waitFor(() => expect(confirm).toBeDisabled());
    await user.click(confirm);

    expect(server.calls.filter((call) => call.path === '/api/admin/alerts/review')).toHaveLength(1);
  });

  it('ยกเลิก waits for the answer: one request even after pressing it and opening the form again', async () => {
    const server = serve({ 'POST /api/admin/alerts/review': () => new Promise(() => {}) });
    const { user } = await renderApp('/admin/alerts');
    const item = await screen.findByRole('article', { name: 'ลงเวลาไกลจากป้าย · แม่บ้านทดสอบ' });
    await user.click(within(item).getByRole('button', { name: 'คุยแล้ว ปิดเตือน' }));

    await user.click(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' }));

    const cancel = within(item).getByRole('button', { name: 'ยกเลิก' });
    await waitFor(() => expect(cancel).toBeDisabled());
    await user.click(cancel);
    expect(within(item).getByRole('button', { name: 'ยืนยันปิดเตือน' })).toBeDisabled();
    expect(within(item).queryByRole('button', { name: 'คุยแล้ว ปิดเตือน' })).not.toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/admin/alerts/review')).toHaveLength(1);
  });

  it('the page notes wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/alerts');

    await screen.findAllByRole('article');
    expectBehindMoreInfo(
      'ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน',
      'เกี่ยวกับหน้านี้',
    );
    expectBehindMoreInfo('รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า', 'เกี่ยวกับหน้านี้');
  });
});
