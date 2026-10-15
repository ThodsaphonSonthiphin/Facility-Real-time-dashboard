import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  attendance,
  cleanerSession,
  entry,
  myWork,
  notCheckedIn,
  onDuty,
  otherAreaBlocked,
  outsideShift,
} from '../../test/cleanerFixtures';
import { fakeGps, farFromSign, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const SIGN = '/check-in/token-checkin-ar01';
const shiftIn0652 = entry('ShiftIn', '2026-10-14T23:52:00Z');

function serve(routes: FakeRoutes, work = myWork(notCheckedIn)) {
  return fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status: 200, body: work }), ...routes });
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
    expect(steps()).toEqual(['เข้างาน ถัดไป', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
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
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก ถัดไป', 'กลับจากพัก –', 'เลิกงาน ถัดไป']);
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

  it('outside the attendance window there is no button (facility-0069)', async () => {
    serve({}, myWork(outsideShift));
    await renderApp(SIGN);

    expect(
      await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ ลงเวลาได้ตั้งแต่ 60 นาทีก่อนกะเริ่มจนถึง 60 นาทีหลังกะจบ'),
    ).toBeInTheDocument();
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

  it('a logged-out Cleaner who scans the sign returns to it after login (facility-0026)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const { router } = await renderApp(SIGN);

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: SIGN } });
  });
});
