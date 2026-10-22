import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
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
import { fakeGps, farFromSign, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
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

describe('CheckInPage (M1, M2, M3, M8, M10)', () => {
  it('shows the shift, the own Area and the Shift-In button (M1)', async () => {
    serve({});
    await renderApp(SIGN);

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ลงเวลา' })).toBeInTheDocument();
    expect(screen.getByText('กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(screen.getByText('Area ของคุณ: Area ทดลอง · 1 จุด')).toBeInTheDocument();
    expect(steps()).toEqual(['เข้างาน –', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
  });

  it('เข้างาน sends the sign token, the event and the GPS, shows the time and offers the next events (M10)', async () => {
    fakeGps(nearSign);
    const server = serve({
      'POST /api/attendance': () => ({ status: 200, body: attendance([shiftIn0652], ['BreakOut', 'ShiftOut'], 'Recorded') }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

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
    // the answer updated My Work's cached copy; nothing was asked again
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(1);
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

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52 แต่อยู่ไกลจากป้าย · ติดธง')).toBeInTheDocument();
    expect(screen.getByText('ความแม่นยำของตำแหน่ง ±40 ม.')).toBeInTheDocument();
    expect(steps()[0]).toBe('เข้างาน 06:52 ติดธง');
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
      await screen.findByText('ยังไม่บันทึกออกพัก เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ'),
    ).toBeInTheDocument();
  });

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

    expect(await screen.findByRole('heading', { name: 'เปิด Location ก่อนลงเวลา' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Android' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'iPhone' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByText('บันทึกเวลาเข้างานแล้ว 06:52')).toBeInTheDocument();
    expect(gps.calls).toBe(2);
  });

  it('on plain http the page says it needs https and offers no retry (facility-0049)', async () => {
    fakeGps(nearSign);
    serve({});
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    expect(screen.getByText(/ต้องเปิดผ่าน https/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();
  });

  it('the check-in sign of another Area is blocked and nothing is recorded (M8, facility-0041)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/attendance': () => ({ status: 403, body: otherAreaBlocked }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'เข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ไม่ใช่ Area ของคุณ' })).toBeInTheDocument();
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

    await user.click(await screen.findByRole('button', { name: 'ออกพัก' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ลงเวลาไม่ตรงลำดับ');
    await screen.findByRole('button', { name: 'ออกพัก' });
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2);
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
      'ยังไม่ได้บันทึกเวลา เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่',
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
        'ยังไม่ได้บันทึกเวลา เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่',
      );
      expect(screen.getByRole('button', { name: 'เข้างาน' })).toBeEnabled();
    } finally {
      vi.useRealTimers();
    }
  });

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

  it('a sign that no longer works is told on opening, with no button, and nothing is sent (facility-0082)', async () => {
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 404, body: { message: 'ไม่พบป้ายลงเวลานี้' } }),
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ใช้ไม่ได้แล้ว' })).toBeInTheDocument();
    expect(
      screen.getByText('ยังไม่ได้ลงเวลา ป้ายนี้อาจถูกสร้าง QR ใหม่แล้ว หรือไม่มีในระบบ แจ้งแอดมิน'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ดูงานของฉัน' })).toHaveAttribute('href', '/my-work');
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);
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

    answer({ status: 204 });

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(1);
  });

  it('if the sign cannot be checked, the page says why and offers no button', async () => {
    serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('alert')).toHaveTextContent('เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่');
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });
});
