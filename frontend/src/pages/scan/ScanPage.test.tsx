import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { checkedOut, cleanerSession, myWork, notCheckedIn, onDuty, otherAreaBlocked, outsideShift, point } from '../../test/cleanerFixtures';
import { fakeGps, nearSign } from '../../test/fakeGps';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { expectBehindMoreInfo } from '../../test/moreInfo';
import { renderApp } from '../../test/renderApp';
import type { ScanRecordCreated } from './scanApi';

const SIGN = '/scan/token-restroom-m1';

function serve(routes: FakeRoutes = {}, card = point(), work = myWork(onDuty)) {
  return fakeServer({
    ...cleanerSession,
    'GET /api/service-points/by-token/token-restroom-m1': () => ({ status: 200, body: card }),
    'GET /api/my-work': () => ({ status: 200, body: work }),
    ...routes,
  });
}

/** A 201 answer of POST /api/scan-records; 03:05Z is 10:05 Thai time. */
function saved(overrides: Partial<ScanRecordCreated> = {}): ScanRecordCreated {
  return {
    scanRecordId: 501,
    servicePointId: 11,
    placement: 'OnTime',
    lateMinutes: null,
    newPointStatus: 'PendingInspection',
    distanceM: 6,
    withinRadius: true,
    submittedAt: '2026-10-15T03:05:00Z',
    ...overrides,
  };
}

describe('ScanPage (M2, M4, M5, M11, M13)', () => {
  it('opens the point with เรียบร้อย chosen and sends in two taps (M4 → M5, facility-0007)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง · ตึก A')).toBeInTheDocument();
    expect(screen.getByText('รอบ 10:00–12:00 · ส่งหลัง 12:00 นับเป็นส่งช้า')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'เรียบร้อย' })).toBeChecked();

    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/scan-records')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      status: 'Normal',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.getByText('ห้องน้ำชาย · เรียบร้อย')).toBeInTheDocument();
    expect(screen.getByText('รอตรวจ')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 6 ม. · อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ดูงานของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('พบปัญหา sends the chosen tags and the note (facility-0046)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('checkbox', { name: 'น้ำยาหมด' }));
    await user.click(screen.getByRole('checkbox', { name: 'ของชำรุด' }));
    await user.type(screen.getByLabelText('หมายเหตุ'), ' ก๊อกรั่ว ');
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' });
    expect(server.calls.find((c) => c.path === '/api/scan-records')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      status: 'Issue',
      issueTags: ['น้ำยาหมด', 'ของชำรุด'],
      notes: 'ก๊อกรั่ว',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.getByText('ห้องน้ำชาย · พบปัญหา: น้ำยาหมด · ของชำรุด · ก๊อกรั่ว')).toBeInTheDocument();
  });

  it('the form is locked while sending, so the result shows what was sent', async () => {
    fakeGps('pending');
    serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('button', { name: 'กำลังส่ง...' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'เรียบร้อย' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'พบปัญหา' })).toBeDisabled();
  });

  it('tags and the note go only with พบปัญหา: switching back to เรียบร้อย sends neither', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    await user.type(screen.getByLabelText('หมายเหตุ'), 'ถังเต็ม');
    await user.click(screen.getByRole('radio', { name: 'เรียบร้อย' }));
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' });
    expect(server.calls.find((c) => c.path === '/api/scan-records')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      status: 'Normal',
      latitude: 13.7563,
      longitude: 100.5018,
      accuracyM: 12,
    });
    expect(screen.getByText('ห้องน้ำชาย · เรียบร้อย')).toBeInTheDocument();
  });

  it('พบปัญหา needs a tag or a note before anything is sent', async () => {
    fakeGps(nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(screen.getByRole('alert')).toHaveTextContent('เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ');
    expect(server.calls.some((c) => c.path === '/api/scan-records')).toBe(false);
  });

  it('the warning shows only after ส่งงาน, goes once a tag or a note is given, and comes back when emptied (facility-0076)', async () => {
    fakeGps(nearSign);
    serve();
    const { user } = await renderApp(SIGN);
    const warning = 'เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ';

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));
    expect(screen.getByRole('alert')).toHaveTextContent(warning);

    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    expect(screen.queryByText(warning)).not.toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    expect(screen.getByRole('alert')).toHaveTextContent(warning);

    await user.type(screen.getByLabelText('หมายเหตุ'), 'ก๊อกรั่ว');
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ส่งงาน' })).toBeEnabled();
  });

  it('the result shows the point status as one line under the heading, with no card (facility-0079)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' });

    expect(screen.getByText('สถานะของจุดนี้')).toHaveTextContent('สถานะของจุดนี้ รอตรวจ');
    expect(screen.queryByRole('heading', { name: 'สถานะของจุดนี้' })).not.toBeInTheDocument();
  });

  it('a point the Supervisor sent back shows the defect, and the fix counts as rework (M4, facility-0029)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/scan-records': () => ({ status: 201, body: saved({ placement: 'Rework', submittedAt: '2026-10-15T03:30:00Z' }) }) },
      point({ status: 'Rework', rework: { defect: 'พื้นใต้อ่างล้างมือยังเปียก', requestedAt: '2026-10-15T03:15:00Z' } }),
    );
    const { user } = await renderApp(SIGN);

    expect(await screen.findByText('หัวหน้าให้แก้ไข · 10:15')).toBeInTheDocument();
    expect(screen.getByText('“พื้นใต้อ่างล้างมือยังเปียก”')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแก้แล้ว 10:30' })).toBeInTheDocument();
    expect(screen.getByText('นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง')).toBeInTheDocument();
  });

  it('a late submission says how late (M13, facility-0047)', async () => {
    fakeGps(nearSign);
    serve(
      {
        'POST /api/scan-records': () => ({
          status: 201,
          body: saved({ placement: 'Late', lateMinutes: 25, submittedAt: '2026-10-15T05:25:00Z' }),
        }),
      },
      point({ status: 'Overdue' }),
    );
    const { user } = await renderApp(SIGN);

    expect(await screen.findByText('รอบ 10:00–12:00 · เลยเวลารอบแล้ว ส่งตอนนี้นับเป็นส่งช้า')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 12:25' })).toBeInTheDocument();
    expect(screen.getByText('ส่งช้า 25 นาที')).toBeInTheDocument();
  });

  it('an off-round submission is kept but changes no card (M13, facility-0047)', async () => {
    fakeGps(nearSign);
    serve({
      'POST /api/scan-records': () => ({
        status: 201,
        body: saved({ placement: 'OffRound', newPointStatus: 'Passed', submittedAt: '2026-10-15T06:20:00Z' }),
      }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ส่งเพิ่มแล้ว (ไม่นับรอบ) 13:20' })).toBeInTheDocument();
    expect(
      screen.getByText('ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน'),
    ).toBeInTheDocument();
  });

  it('far from the sign: saved but flagged, with the distance (facility-0037)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved({ distanceM: 85, withinRadius: false }) }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(
      await screen.findByText('ไกลป้าย: ห่างป้าย 85 ม. แอดมินจะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้งแอดมินได้เลย'),
    ).toBeInTheDocument();
  });

  it('before Shift-In the point cannot be submitted, and nothing is sent (M11)', async () => {
    const server = serve({}, point(), myWork(notCheckedIn));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้ลงเวลาเข้างาน' })).toBeInTheDocument();
    expect(screen.getByText('ป้ายจุดทำความสะอาด · ห้องน้ำชาย · Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกงาน')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ส่งงาน' })).not.toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/scan-records')).toBe(false);
  });

  it('after Shift-Out the point cannot be submitted', async () => {
    serve({}, point(), myWork(checkedOut));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ลงเวลาเลิกงานแล้ว' })).toBeInTheDocument();
  });

  it('outside the own shift the point cannot be submitted (facility-0069)', async () => {
    serve({}, point(), myWork(outsideShift));
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ตอนนี้ไม่อยู่ในกะของคุณ' })).toBeInTheDocument();
  });

  it('an unknown or closed sign shows the server message', async () => {
    serve({
      'GET /api/service-points/by-token/token-restroom-m1': () => ({
        status: 404,
        body: { message: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' },
      }),
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' })).toBeInTheDocument();
  });

  it('Location off shows how to turn it on, then sends (M2, facility-0036)', async () => {
    fakeGps('denied', nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    expect(await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนส่งงาน' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
  });

  it('a logged-out Cleaner who scans the sign returns to it after login (facility-0017, 0026)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const { router } = await renderApp(SIGN);

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: SIGN } });
  });

  it('an Admin opening a point sign sees no-permission; Cleaners and Supervisors use it (plan 9 decision 2)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    await renderApp(SIGN);

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('หน้านี้สำหรับแม่บ้าน หรือ หัวหน้างาน บัญชีนี้เป็นแอดมิน')).toBeInTheDocument();
  });
});

describe('ScanPage when the send does not go through (M8, M12)', () => {
  it('no signal: ยังไม่ได้บันทึก keeps the choices, and ส่งใหม่ sends them again with a fresh GPS reading (M12, facility-0052)', async () => {
    const gps = fakeGps(nearSign);
    let tries = 0;
    const server = serve({
      'POST /api/scan-records': () => {
        tries += 1;
        if (tries === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 201, body: saved() };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeInTheDocument();
    expect(screen.getByText('พบปัญหา: ขยะล้น')).toBeInTheDocument();
    expect(screen.getByText(/^ส่งงานไม่สำเร็จ .* ลองล่าสุด \d\d:\d\d$/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ส่งใหม่' }));

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    const sends = server.calls.filter((c) => c.path === '/api/scan-records');
    expect(sends).toHaveLength(2);
    expect(sends[1].body).toEqual(sends[0].body);
    expect(gps.calls).toBe(2);
  });

  it('a server fault is also ยังไม่ได้บันทึก', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 503 }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ส่งใหม่' })).toBeEnabled();
  });

  it('no answer in 20 s is ยังไม่ได้บันทึก too (plan 8 decision 8)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/scan-records': () => new Promise<never>(() => {}) });
    const { user } = await renderApp(SIGN);
    const send = await screen.findByRole('button', { name: 'ส่งงาน' });

    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      await user.click(send);
      await vi.waitFor(() => expect(server.calls.some((c) => c.path === '/api/scan-records')).toBe(true));
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS - 1_000);
      expect(screen.getByRole('button', { name: 'กำลังส่ง...' })).toBeDisabled();

      await vi.advanceTimersByTimeAsync(1_000);

      expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึก' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'ส่งใหม่' })).toBeEnabled();
    } finally {
      vi.useRealTimers();
    }
  });

  it('a point of another Area is blocked, and the Admin sees it (M8, facility-0041)', async () => {
    fakeGps(nearSign);
    serve(
      { 'POST /api/scan-records': () => ({ status: 403, body: otherAreaBlocked }) },
      point({ id: 21, name: 'ห้องประชุม', areaId: 2, areaCode: 'AR02', areaName: 'Area ถัดไป', buildingCode: 'B' }),
    );
    const { user } = await renderApp(SIGN);

    await screen.findByRole('heading', { name: 'ห้องประชุม' });
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ไม่ใช่พื้นที่ของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้บันทึกงาน')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป (AR02) · ตึก B')).toBeInTheDocument();
    expect(screen.getByText('Area ทดลอง')).toBeInTheDocument();
    expect(
      screen.getByText(
        'แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณทำแทน Area ถัดไป เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ',
      ),
    ).toBeInTheDocument();
  });

  it('a refusal from the server stays on the form, word for word', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 409, body: { message: 'ลงเวลาเข้างานที่ป้ายของพื้นที่คุณก่อน' } }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ลงเวลาเข้างานที่ป้ายของพื้นที่คุณก่อน');
    expect(screen.getByRole('button', { name: 'ส่งงาน' })).toBeEnabled();
  });

  it('ส่งงาน cannot be pressed again while a send is in flight', async () => {
    fakeGps(nearSign);
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const server = serve({
      'POST /api/scan-records': async () => {
        await gate;
        return { status: 201, body: saved() };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    const busy = await screen.findByRole('button', { name: 'กำลังส่ง...' });
    expect(busy).toBeDisabled();
    await user.click(busy);
    release();

    expect(await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' })).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/scan-records')).toHaveLength(1);
  });

  it('what happens with no signal waits behind a phone-size ℹ (owner, 2026-10-27)', async () => {
    fakeGps(nearSign);
    serve();
    await renderApp(SIGN);

    await screen.findByRole('heading', { name: 'ห้องน้ำชาย' });
    expectBehindMoreInfo(
      'ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ',
      'เกี่ยวกับการส่งงาน',
    );
    expect(screen.getByText('เกี่ยวกับการส่งงาน').closest('summary')).toHaveClass('min-h-12');
  });
});
