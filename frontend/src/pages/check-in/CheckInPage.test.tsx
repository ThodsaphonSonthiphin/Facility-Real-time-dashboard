import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import type { AttendanceAction, AttendanceActionState, AttendanceEvent, AttendanceState } from '../../shared/api/types';
import {
  area,
  attendance,
  cleanerSession,
  entry,
  myWork,
  nightShift,
  notCheckedIn,
  onDuty,
  otherAreaBlocked,
  outsideShift,
} from '../../test/cleanerFixtures';
import { browserPage } from '../../shared/utils/reloadPage';
import { fakeGps, farFromSign, nearSign, permissionsSay } from '../../test/fakeGps';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
import { expectBehindMoreInfo } from '../../test/moreInfo';
import { renderApp } from '../../test/renderApp';

const SIGN = '/check-in/token-checkin-ar01';
const shiftIn0652 = entry('ShiftIn', '2026-10-14T23:52:00Z');

function serve(routes: FakeRoutes, work = myWork(notCheckedIn)) {
  return fakeServer({
    ...cleanerSession,
    'GET /api/my-work': () => ({ status: 200, body: work }),
    'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 204 }),
    ...routes,
  });
}

/** The four tiles of "วันนี้", as a person reads them. */
function steps() {
  return within(screen.getByRole('list', { name: 'วันนี้' }))
    .getAllByRole('listitem')
    .map((li) => li.textContent);
}

/** The hub's first start asks again (facility-0064): wait for that catch-up answer before counting what a press asks. */
async function hubCatchUp(server: { calls: { path: string }[] }) {
  await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2));
}

describe('CheckInPage (M1, M2, M3, M8, M10)', () => {
  it('retries a failed check-in read in place, then enables attendance actions', async () => {
    let signReads = 0;
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => {
        signReads += 1;
        return signReads === 1 ? { status: 503 } : { status: 204 };
      },
    });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('alert')).toHaveTextContent('ระบบขัดข้อง ลองอีกครั้งในอีกสักครู่');
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
    const retry = screen.getByRole('button', { name: 'ลองอีกครั้ง' });
    expect(retry).toBeInTheDocument();

    await user.click(retry);

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(await screen.findByText('เชื่อมต่อสำเร็จแล้ว')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ลงเวลา' })).toHaveFocus();
    expect(server.calls.filter((call) => call.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(2);
  });

  it('retries a failed work read without leaving the scanned sign', async () => {
    let recovered = false;
    const server = serve({
      'GET /api/my-work': () => recovered
        ? { status: 200, body: myWork(notCheckedIn) }
        : { status: 503 },
    });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('alert')).toHaveTextContent('ระบบขัดข้อง ลองอีกครั้งในอีกสักครู่');
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();

    recovered = true;
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/my-work').length).toBeGreaterThan(1);
  });

  it('disables read retry while its requests are still pending', async () => {
    let releaseRetry!: () => void;
    let signReads = 0;
    const retryPending = new Promise<void>((resolve) => { releaseRetry = resolve; });
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': async () => {
        signReads += 1;
        if (signReads === 1) return { status: 503 };
        await retryPending;
        return { status: 204 };
      },
    });
    const { user } = await renderApp(SIGN);

    await screen.findByRole('alert');
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    await waitFor(() => expect(server.calls.filter((call) => call.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(2));
    const pending = await screen.findByRole('button', { name: 'กำลังโหลด...' });
    expect(pending).toBeDisabled();
    expect(await screen.findByText('กำลังลองเชื่อมต่ออีกครั้ง')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(2);
    releaseRetry();
    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(await screen.findByText('เชื่อมต่อสำเร็จแล้ว')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ลงเวลา' })).toHaveFocus();
  });

  it('announces when a retry finishes with the read still unavailable', async () => {
    serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 503 }),
    });
    const { user } = await renderApp(SIGN);
    await screen.findByRole('alert');
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(await screen.findByText('ยังเชื่อมต่อไม่ได้ ลองอีกครั้ง')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ลองอีกครั้ง' })).toBeInTheDocument();
  });

  it('shows the shift, the own Area and the Shift-In button (M1)', async () => {
    serve({});
    await renderApp(SIGN);

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ลงเวลา' })).toBeInTheDocument();
    expect(screen.getByText('กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(screen.getByText('พื้นที่ของคุณ: Area ทดลอง · 1 จุด')).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน –', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
  });

  it('เข้างาน sends the sign token, the event and the GPS, shows the time and offers the next events (M10)', async () => {
    fakeGps(nearSign);
    const server = serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);
    await screen.findByRole('button', { name: 'เข้างาน' });
    await hubCatchUp(server);

    await user.click(screen.getByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/attendance')?.body).toEqual({
      qrToken: 'token-checkin-ar01',
      eventType: 'ShiftIn',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    // facility-0078: two events may come next, and only the buttons say so
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
    expect(screen.queryByText('ถัดไป')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ออกพัก' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'เลิกงาน' })).toBeInTheDocument();
    // the answer updated My Work's cached copy; nothing was asked again after the hub's catch-up
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2);
  });

  it('far from the sign: recorded but flagged, with the phone accuracy (M3, facility-0035)', async () => {
    fakeGps(farFromSign);
    serve({
      'POST /api/attendance': () => ({
        status: 200,
        body: attendance([entry('ShiftIn', '2026-10-14T23:52:00Z', false)], ['BreakOut', 'ShiftOut'], 'Recorded'),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52 แต่อยู่ไกลป้าย')).toBeInTheDocument();
    expect(screen.getByText('ความแม่นยำของตำแหน่ง ±40 ม.')).toBeInTheDocument();
    expect(steps()[0]).toBe('เข้างาน 06:52 ไกลป้าย');
  });

  it('after the break only เลิกงาน is offered, and then the shift is complete (M10)', async () => {
    fakeGps(nearSign);
    const afterBreak = attendance(
      [shiftIn0652, entry('BreakOut', '2026-10-15T05:01:00Z'), entry('BreakIn', '2026-10-15T05:58:00Z')], // 12:01, 12:58
      ['ShiftOut'],
    );
    serve(
      {
        'POST /api/attendance': () => ({
          status: 200,
          body: attendance([...afterBreak.events, entry('ShiftOut', '2026-10-15T11:55:00Z')], [], 'Recorded'), // 18:55
        }),
      },
      myWork(afterBreak),
    );
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เลิกงาน' }));

    expect(await screen.findByText('บันทึกเวลาเลิกงานแล้ว 18:55')).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก 12:01', 'กลับจากพัก 12:58', 'เลิกงาน 18:55']);
    expect(screen.getByText('ลงเวลาครบแล้วสำหรับกะนี้')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เลิกงาน' })).not.toBeInTheDocument();
  });

  it('a press within 5 minutes of the last one is not counted (facility-0031)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'TooSoon') }) },
      myWork(onDuty),
    );
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ออกพัก' }));

    expect(
      await screen.findByText('ยังไม่ได้ลงเวลาออกพัก รอให้ครบ 5 นาทีจากครั้งก่อน แล้วกด “ออกพัก” อีกครั้ง'),
    ).toBeInTheDocument();
  });

  // First-in rule: docs/adr/facility-0026-shift-check-in-two-shifts-strict-presence.md.
  it('an event already recorded keeps its first time (facility-0026)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/attendance': () => ({
        status: 200,
        body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'AlreadyRecorded'),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('เข้างาน: ลงเวลาไว้แล้วตอน 06:52 ระบบเก็บเวลาแรกไว้')).toBeInTheDocument();
  });

  it('Location off shows how to turn it on, and "เปิดแล้ว ลองอีกครั้ง" records (M2, facility-0036)', async () => {
    const gps = fakeGps('denied', nearSign);
    const server = serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนลงเวลา' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Android' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'iPhone' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(gps.calls).toBe(3); // the refusal, the quick check that it is gone, the reading
  });

  it('location turned on but the page keeps the refusal (iPhone Safari): "เปิดแล้ว ลองอีกครั้ง" reloads the page', async () => {
    fakeGps('denied');
    permissionsSay('granted');
    const reload = vi.spyOn(browserPage, 'reload').mockImplementation(() => {});
    const server = serve({});
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));
    await user.click(await screen.findByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    await waitFor(() => expect(reload).toHaveBeenCalledTimes(1));
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);
  });

  it('location still off: "เปิดแล้ว ลองอีกครั้ง" keeps the help and does not reload (nothing on the page is lost)', async () => {
    const gps = fakeGps('denied');
    permissionsSay('denied');
    const reload = vi.spyOn(browserPage, 'reload').mockImplementation(() => {});
    serve({});
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));
    await user.click(await screen.findByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    await waitFor(() => expect(gps.calls).toBe(3));
    expect(await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนลงเวลา' })).toBeInTheDocument();
    expect(reload).not.toHaveBeenCalled();
  });

  it('on plain http the page asks for a working link and offers no retry (facility-0049)', async () => {
    fakeGps(nearSign);
    serve({});
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    expect(screen.getByText(/ขอลิงก์ที่ใช้งานได้จากแอดมิน/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();
  });

  it('the check-in sign of another Area is blocked and nothing is recorded (M8, facility-0041)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/attendance': () => ({ status: 403, body: otherAreaBlocked }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ไม่ใช่พื้นที่ของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกเวลา')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป (AR02) · ตึก B')).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปงานของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('outside the attendance window: no button, the own shift and the times it can be recorded (facility-0069, 0077)', async () => {
    serve({}, myWork(outsideShift, [area()], nightShift));
    await renderApp(SIGN);

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ')).toBeInTheDocument();
    expect(screen.getByText('กะของคุณ: กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(screen.getByText('ลงเวลาได้ตั้งแต่ 18:00 ถึง 08:00')).toBeInTheDocument();
    expect(screen.queryByText(/60 นาที/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });

  it('an out-of-order press shows the server message and asks for the allowed buttons again', async () => {
    fakeGps(nearSign);
    const server = serve(
      { 'POST /api/attendance': () => ({ status: 409, body: { message: 'ลงเวลาไม่ตรงลำดับ', nextEvents: ['ShiftOut'] } }) },
      myWork(onDuty),
    );
    const { user } = await renderApp(SIGN);
    await screen.findByRole('button', { name: 'ออกพัก' });
    await hubCatchUp(server);

    await user.click(screen.getByRole('button', { name: 'ออกพัก' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ลงเวลาไม่ตรงลำดับ');
    await screen.findByRole('button', { name: 'ออกพัก' });
    // the first answer, the hub's catch-up, and the ask after the refused press
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(3);
  });

  it('no signal: says the time was not recorded and keeps the buttons (facility-0052)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/attendance': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'ยังไม่ได้บันทึกเวลา เชื่อมต่อระบบไม่ได้ ตรวจดูสัญญาณอินเทอร์เน็ตแล้วลองอีกครั้ง',
    );
    expect(screen.getByRole('button', { name: 'เข้างาน' })).toBeEnabled();
  });

  it('no answer in 20 s also says the time was not recorded, and the buttons come back (plan 8 decision 8)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/attendance': () => new Promise<never>(() => {}) });
    const { user } = await renderApp(SIGN);
    const shiftIn = await screen.findByRole('button', { name: 'เข้างาน' });

    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      await user.click(shiftIn);
      await vi.waitFor(() => expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(true));
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS - 1_000);
      expect(screen.getByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();

      await vi.advanceTimersByTimeAsync(1_000);

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'ยังไม่ได้บันทึกเวลา เชื่อมต่อระบบไม่ได้ ตรวจดูสัญญาณอินเทอร์เน็ตแล้วลองอีกครั้ง',
      );
      expect(screen.getByRole('button', { name: 'เข้างาน' })).toBeEnabled();
    } finally {
      vi.useRealTimers();
    }
  });

  // Return-to-page rule: docs/adr/facility-0026-frontend-router-shape.md, item 4.
  it('a logged-out Cleaner who scans the sign returns to it after login (facility-0026)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const { router } = await renderApp(SIGN);

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: SIGN } });
  });

  it('no fix from the phone says so and "ลองอีกครั้ง" records (M2, facility-0036)', async () => {
    fakeGps('unavailable', nearSign);
    serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
  });

  it('one send at a time: while a press waits for the GPS, every button is disabled', async () => {
    const gps = fakeGps('pending');
    const server = serve({}, myWork(onDuty));
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ออกพัก' }));

    expect(await screen.findByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();
    const other = screen.getByRole('button', { name: 'เลิกงาน' });
    expect(other).toBeDisabled();
    await user.click(other);

    expect(gps.calls).toBe(1);
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);
  });

  it('off shift for an account with no shift: only the headline, no shift and no times (facility-0077)', async () => {
    const work = myWork(outsideShift);
    delete work.ownShift;
    serve({}, work);
    await renderApp(SIGN);

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ')).toBeInTheDocument();
    expect(screen.queryByText(/กะของคุณ:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/ลงเวลาได้ตั้งแต่/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });

  it('a sign that no longer works is told on opening, with no button (facility-0082)', async () => {
    serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 404, body: { message: 'ไม่พบป้ายลงเวลานี้' } }),
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ใช้ไม่ได้แล้ว' })).toBeInTheDocument();
    expect(
      screen.getByText('ยังไม่ได้ลงเวลา ลองสแกนป้ายลงเวลาที่ติดอยู่ล่าสุด ถ้ายังใช้ไม่ได้ ให้แจ้งแอดมิน'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ดูงานของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('the buttons wait until the server says the sign works, asked once per opening (facility-0082)', async () => {
    let answer: (response: FakeResponse) => void = () => {};
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': () =>
        new Promise<FakeResponse>((resolve) => {
          answer = resolve;
        }),
    });
    await renderApp(SIGN);

    expect(await screen.findByText('กะเช้า 07:00–19:00')).toBeInTheDocument();
    await waitFor(() => expect(server.calls.some((c) => c.path === '/api/attendance/signs/token-checkin-ar01')).toBe(true));
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
    expect(screen.getByText('กำลังโหลด...')).toBeInTheDocument();

    answer({ status: 204 });

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(screen.queryByText('กำลังโหลด...')).not.toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(1);
  });

  it('if the sign cannot be checked, the page says why and offers no button', async () => {
    serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('alert')).toHaveTextContent('เชื่อมต่อระบบไม่ได้ ตรวจดูสัญญาณอินเทอร์เน็ตแล้วลองอีกครั้ง');
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });

  it('how the buttons work waits behind a phone-size ℹ (owner, 2026-10-27)', async () => {
    serve({});
    await renderApp(SIGN);

    await screen.findByRole('button', { name: 'เข้างาน' });
    expectBehindMoreInfo('กดตามปุ่มที่ขึ้นบนหน้าจอ เว้นอย่างน้อย 5 นาทีระหว่างการลงเวลาแต่ละครั้ง ระบบนี้ใช้ดูว่าพื้นที่มีคนทำงานแล้วหรือยัง', 'เกี่ยวกับการลงเวลา');
    expect(screen.getByText('เกี่ยวกับการลงเวลา').closest('summary')).toHaveClass('min-h-12');
  });
});

function act(
  eventType: AttendanceEvent,
  state: AttendanceActionState,
  occurredAt?: string,
  expectedAt?: string,
): AttendanceAction {
  return { eventType, state, occurredAt, expectedAt };
}

/** A slot as the server answers it with the lifecycle fields (spec §8): the timeline is the server's own list. */
function slot(rows: AttendanceAction[], overrides: Partial<AttendanceState> = {}): AttendanceState {
  return {
    shiftDate: '2026-10-15',
    shift: 'Day',
    events: [],
    nextEvents: [],
    lifecycleVersion: 'Numbered6',
    stateRevision: '5',
    actions: rows,
    gaps: [],
    nextActions: [],
    dutyAreaId: 1,
    dutySource: 'dated',
    isDayOff: false,
    isLeave: false,
    ...overrides,
  };
}

const sixPending = [
  act('ShiftIn', 'pending'),
  act('Break1Out', 'pending'),
  act('Break1In', 'pending'),
  act('Break2Out', 'pending'),
  act('Break2In', 'pending'),
  act('ShiftOut', 'pending'),
];

describe('the six-action lifecycle (spec §8)', () => {
  it('a legacy slot keeps its stored four rows, from the server action list, and never shows a numbered second break', async () => {
    const legacy = slot(
      [
        act('ShiftIn', 'recorded', '2026-10-14T23:52:00Z'),
        act('BreakOut', 'pending'),
        act('BreakIn', 'pending'),
        act('ShiftOut', 'pending'),
      ],
      { lifecycleVersion: 'Legacy4', stateRevision: '2', nextActions: [{ action: 'BreakOut', requiredGaps: [] }] },
    );
    serve({}, myWork(legacy));
    await renderApp(SIGN);

    expect(await screen.findByRole('button', { name: 'ออกพัก' })).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
    expect(screen.queryByText(/กลับจากพัก 2/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ออกพัก 1' })).not.toBeInTheDocument();
  });

  it('a numbered slot shows all six actions and sends the contract and the revision it read', async () => {
    fakeGps(nearSign);
    const answered = slot(
      [act('ShiftIn', 'recorded', '2026-10-14T23:52:00Z'), ...sixPending.slice(1)],
      {
        events: [shiftIn0652],
        nextEvents: ['Break1Out', 'ShiftOut'],
        nextActions: [
          { action: 'Break1Out', requiredGaps: [] },
          { action: 'ShiftOut', requiredGaps: ['Break1Out', 'Break1In', 'Break2Out', 'Break2In'] },
        ],
      },
    );
    const server = serve(
      {
        'POST /api/attendance': () => ({ status: 200, body: { ...answered, outcome: 'Recorded' } }),
      },
      myWork(slot(sixPending, { nextActions: [{ action: 'ShiftIn', requiredGaps: [] }] })),
    );
    const { user } = await renderApp(SIGN);
    await screen.findByRole('button', { name: 'เข้างาน' });

    expect(steps()).toEqual([
      'เข้างาน –',
      'ออกพัก 1 –',
      'กลับจากพัก 1 –',
      'ออกพัก 2 –',
      'กลับจากพัก 2 –',
      'เลิกงาน –',
    ]);

    await user.click(screen.getByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/attendance')?.body).toEqual({
      qrToken: 'token-checkin-ar01',
      eventType: 'ShiftIn',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
      lifecycleVersion: 'Numbered6',
      stateRevision: '5',
    });
    expect(steps()).toEqual([
      'เข้างาน 06:52',
      'ออกพัก 1 –',
      'กลับจากพัก 1 –',
      'ออกพัก 2 –',
      'กลับจากพัก 2 –',
      'เลิกงาน –',
    ]);
    expect(screen.getByRole('button', { name: 'ออกพัก 1' })).toBeInTheDocument();
  });

  it('a press that steps over earlier breaks asks first, then sends exactly those skip markers and invents no time', async () => {
    fakeGps(nearSign);
    const before = slot(
      [act('ShiftIn', 'recorded', '2026-10-14T23:52:00Z'), ...sixPending.slice(1)],
      {
        events: [shiftIn0652],
        nextEvents: ['Break2Out', 'ShiftOut'],
        nextActions: [
          { action: 'Break2Out', requiredGaps: ['Break1Out', 'Break1In'] },
          { action: 'ShiftOut', requiredGaps: ['Break1Out', 'Break1In', 'Break2Out', 'Break2In'] },
        ],
      },
    );
    const after = slot(
      [
        act('ShiftIn', 'recorded', '2026-10-14T23:52:00Z'),
        act('Break1Out', 'skipped'),
        act('Break1In', 'skipped'),
        act('Break2Out', 'recorded', '2026-10-15T07:00:00Z'),
        act('Break2In', 'pending'),
        act('ShiftOut', 'pending'),
      ],
      {
        events: [shiftIn0652, entry('Break2Out', '2026-10-15T07:00:00Z')],
        nextEvents: ['Break2In', 'ShiftOut'],
        nextActions: [
          { action: 'Break2In', requiredGaps: [] },
          { action: 'ShiftOut', requiredGaps: ['Break2In'] },
        ],
        gaps: [
          { eventType: 'Break1Out', status: 'Skipped', recordedAt: '2026-10-15T07:00:00Z' },
          { eventType: 'Break1In', status: 'Skipped', recordedAt: '2026-10-15T07:00:00Z' },
        ],
      },
    );
    const server = serve({ 'POST /api/attendance': () => ({ status: 200, body: { ...after, outcome: 'Recorded' } }) }, myWork(before));
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ออกพัก 2' }));

    expect(await screen.findByText(/ต้องยืนยันรายการที่ข้ามก่อน: ออกพัก 1 และ กลับจากพัก 1/)).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/attendance')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'ยืนยันและบันทึก' }));

    expect(await screen.findByText('บันทึกเวลาออกพัก 2แล้ว 14:00')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/attendance')?.body).toMatchObject({
      eventType: 'Break2Out',
      confirmGaps: ['Break1Out', 'Break1In'],
      lifecycleVersion: 'Numbered6',
      stateRevision: '5',
    });
    expect(steps()).toEqual([
      'เข้างาน 06:52',
      'ออกพัก 1 ข้ามการสแกน',
      'กลับจากพัก 1 ข้ามการสแกน',
      'ออกพัก 2 14:00',
      'กลับจากพัก 2 –',
      'เลิกงาน –',
    ]);
  });

  it('a recorded 13:00 departure and leave excuse later breaks while an earlier break stays missing', async () => {
    const shiftOut1300 = entry('ShiftOut', '2026-10-15T06:00:00Z');
    const leaving = slot(
      [
        act('ShiftIn', 'recorded', '2026-10-14T23:52:00Z'),
        act('Break1Out', 'missing'),
        act('Break1In', 'not-applicable'),
        act('Break2Out', 'not-applicable'),
        act('Break2In', 'not-applicable', undefined, '2026-10-15T10:00:00Z'), // 17:00 is the plan, never a scan
        act('ShiftOut', 'recorded', shiftOut1300.occurredAt),
      ],
      { events: [shiftIn0652, shiftOut1300], isLeave: true },
    );
    serve({}, myWork(leaving));
    await renderApp(SIGN);

    await screen.findByRole('list', { name: 'วันนี้' });
    const rows = steps();
    expect(rows[4]).toContain('ไม่ต้องลงเวลาเนื่องจากลา');
    expect(rows[4]).not.toContain('17:00');
    expect(rows[5]).toBe('เลิกงาน 13:00');
    expect(screen.queryByText('เวลาเลิกงานก่อนจบกะไม่ตรงกับช่วงลาที่บันทึกไว้')).not.toBeInTheDocument();
    expect(screen.getByText('สิ้นสุดกะแล้ว')).toBeVisible();
    expect(rows[1]).toBe('ออกพัก 1 ไม่ได้สแกน');
    expect(rows.join(' ')).not.toContain('กลับจากพัก 2: 17:00');
  });

  it('shows the actual early departure and the leave mismatch together', async () => {
    const actual = entry('ShiftOut', '2026-10-15T06:00:00Z');
    serve({}, myWork(slot(
      [...sixPending.slice(0, -1), act('ShiftOut', 'recorded', actual.occurredAt)],
      { events: [shiftIn0652, actual], departureLeaveMismatch: true },
    )));
    await renderApp(SIGN);

    expect(await screen.findByText('เวลาเลิกงานก่อนจบกะไม่ตรงกับช่วงลาที่บันทึกไว้')).toBeVisible();
    expect(steps().at(-1)).toBe('เลิกงาน 13:00');
  });

  it('a recorded day off or leave never blocks an eligible Shift-In', async () => {
    const conflicted = slot(sixPending, {
      isDayOff: true,
      isLeave: true,
      dutyAreaId: 1,
      nextActions: [{ action: 'ShiftIn', requiredGaps: [] }],
    });
    serve({}, myWork(conflicted));
    await renderApp(SIGN);

    const shiftIn = await screen.findByRole('button', { name: 'เข้างาน' });
    expect(shiftIn).toBeEnabled();
    expect(screen.getByText('กะนี้ตรงกับวันหยุดตามแผน')).toBeVisible();
    expect(screen.getByText('มีการลาที่บันทึกไว้ในกะนี้')).toBeVisible();
  });

  it('a stale page is told to read again, and the plan and the attendance are read again', async () => {
    fakeGps(nearSign);
    const server = serve(
      {
        'POST /api/attendance': () => ({
          status: 409,
          body: {
            message: 'หน้านี้ไม่ตรงกับการลงเวลาล่าสุด โหลดหน้าใหม่แล้วลองอีกครั้ง',
            nextEvents: [],
            code: 'refresh-required',
          },
        }),
      },
      myWork(slot(sixPending, { nextActions: [{ action: 'ShiftIn', requiredGaps: [] }] })),
    );
    const { user } = await renderApp(SIGN);
    await screen.findByRole('button', { name: 'เข้างาน' });
    await hubCatchUp(server);

    await user.click(screen.getByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'หน้านี้ไม่ตรงกับการลงเวลาล่าสุด โหลดหน้าใหม่แล้วลองอีกครั้ง',
    );
    await waitFor(() => expect(server.calls.filter((call) => call.path === '/api/my-work')).toHaveLength(3));
  });
});

describe('the transfer arrival (spec §9)', () => {
  const breakOut = entry('Break1Out', '2026-10-15T03:00:00Z');
  const actions = [
    act('ShiftIn', 'recorded', shiftIn0652.occurredAt),
    act('Break1Out', 'recorded', breakOut.occurredAt),
    ...sixPending.slice(2),
  ];
  const instructed = slot(actions, {
    dutyAreaId: undefined,
    dutySource: 'transfer-pending',
    events: [shiftIn0652, breakOut],
    transfer: {
      id: 9,
      fromAreaId: 1,
      toAreaId: 2,
      effectiveAt: '2026-10-15T05:00:00Z',
      state: 'instructed',
      revision: '7',
    },
  });
  const destinationSign = {
    'GET /api/attendance/signs/token-checkin-ar01': ({ search }: { search: string }) => (
      search === '?transferId=9' ? { status: 200, body: { canConfirmTransfer: true } } : { status: 204 }
    ),
  };

  it('shows the pending instruction at the origin sign without offering a confirmation or an attendance action', async () => {
    serve(
      { 'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 200, body: { canConfirmTransfer: false } }) },
      myWork(instructed),
    );
    await renderApp(SIGN);

    expect(await screen.findByText('มีคำสั่งย้ายพื้นที่ที่ยังไม่ได้ยืนยัน')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'ยืนยันเข้าพื้นที่ใหม่' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });

  it('offers only the arrival confirmation, and a confirmed arrival reports it instead of a new shift-in or a break', async () => {
    fakeGps(nearSign);
    const confirmed = { id: 9, state: 'confirmed' as const, confirmedAt: '2026-10-15T05:30:00Z', revision: '8' };
    let work = myWork(instructed);
    const server = serve(
      {
        ...destinationSign,
        'GET /api/my-work': () => ({ status: 200, body: work }),
        'POST /api/area-transfers/9/confirm': () => {
          work = myWork({
            ...instructed,
            dutyAreaId: 2,
            dutySource: 'transfer',
            transfer: { ...instructed.transfer!, ...confirmed },
            nextActions: [{ action: 'Break1In', requiredGaps: [] }],
          });
          return { status: 200, body: confirmed };
        },
      },
      myWork(instructed),
    );
    const { user } = await renderApp(SIGN);
    await hubCatchUp(server);

    expect(await screen.findByRole('button', { name: 'ยืนยันเข้าพื้นที่ใหม่' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ยืนยันเข้าพื้นที่ใหม่' }));

    expect(await screen.findByText('ยืนยันถึงพื้นที่ใหม่แล้ว')).toBeInTheDocument();
    expect(screen.getByText('ยืนยันถึงพื้นที่ใหม่แล้ว').closest('[role="status"]')).toHaveTextContent('12:30');
    expect(server.calls.find((call) => call.path === '/api/area-transfers/9/confirm')?.body).toEqual({
      expectedRevision: '7',
      qrToken: 'token-checkin-ar01',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.queryByText(/เข้างานใหม่/)).not.toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/attendance')).toHaveLength(0);
    expect(await screen.findByRole('button', { name: 'กลับจากพัก 1' })).toBeEnabled();
    expect(steps().slice(0, 3)).toEqual(['เข้างาน 06:52', 'ออกพัก 1 10:00', 'กลับจากพัก 1 –']);
    await waitFor(() => expect(server.calls.filter((call) => call.path === '/api/my-work')).toHaveLength(3));
  });

  it('a cancellation that won the race is shown as it is and the plan is read again', async () => {
    fakeGps(nearSign);
    const server = serve(
      {
        ...destinationSign,
        'POST /api/area-transfers/9/confirm': () => ({
          status: 409,
          body: { message: 'คำสั่งย้ายพื้นที่นี้ถูกยกเลิกแล้ว' },
        }),
      },
      myWork(instructed),
    );
    const { user } = await renderApp(SIGN);
    await hubCatchUp(server);
    await user.click(await screen.findByRole('button', { name: 'ยืนยันเข้าพื้นที่ใหม่' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('คำสั่งย้ายพื้นที่นี้ถูกยกเลิกแล้ว');
    await waitFor(() => expect(server.calls.filter((call) => call.path === '/api/my-work')).toHaveLength(3));
  });
});
