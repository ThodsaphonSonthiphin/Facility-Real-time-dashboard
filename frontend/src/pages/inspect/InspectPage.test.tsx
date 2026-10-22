import { act, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { point } from '../../test/cleanerFixtures';
import { farFromSign, fakeGps, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import {
  inspectionScan,
  myBuilding,
  nothingToInspect,
  otherBuildingBlocked,
  outsideShiftBlocked,
  recorded,
  submission,
  supervisorSession,
} from '../../test/supervisorFixtures';
import type { InspectionScan } from './inspectApi';

const SIGN = '/scan/token-restroom-m1';

function serve(routes: FakeRoutes = {}, scan: InspectionScan = inspectionScan()) {
  return fakeServer({
    ...supervisorSession,
    'POST /api/inspections/scan': () => ({ status: 200, body: scan }),
    ...routes,
  });
}

const nearBody = { latitude: 13.7563, longitude: 100.5018, accuracyM: 12 };

describe('InspectPage (M6, M7, M16, facility-0029)', () => {
  it('opening the sign reads a fresh location, scans, and shows the latest submission with no verdict chosen (M6)', async () => {
    const gps = fakeGps(nearSign);
    const server = serve();
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections/scan')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      ...nearBody,
    });
    expect(gps.lastOptions).toMatchObject({ enableHighAccuracy: true, maximumAge: 0 });
    expect(screen.getByText('ตรวจงาน · Area ทดลอง · ตึก A')).toBeInTheDocument();
    expect(screen.getByText('10:05 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · เรียบร้อย')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 4 ม. · ±12 ม.')).toBeInTheDocument();
    expect(screen.getByText('อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).not.toBeChecked();
    expect(screen.getByRole('radio', { name: 'ไม่สะอาด' })).not.toBeChecked();
    expect(screen.queryByLabelText('สิ่งที่ต้องแก้')).not.toBeInTheDocument();
    // The Supervisor's page never touches the Cleaner's endpoints
    expect(server.calls.some((c) => c.path.startsWith('/api/service-points') || c.path === '/api/scan-records')).toBe(false);
  });

  it('สะอาด records Passed with a fresh location and shows the saved result (M7)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Passed',
      ...nearBody,
    });
    expect(screen.getByText('ผล: สะอาด')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน')).toBeInTheDocument();
    expect(screen.getByText('ห้องน้ำชาย · รอบ 10:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 4 ม. · อยู่ในรัศมี')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('ไม่สะอาด needs a defect, then sends it trimmed and shows what the Cleaner will see (M7)', async () => {
    fakeGps(nearSign);
    const server = serve({
      'POST /api/inspections': () => ({ status: 201, body: recorded({ result: 'Rework', newPointStatus: 'Rework' }) }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('ใส่สิ่งที่ต้องแก้ก่อนบันทึก');
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);

    await user.type(screen.getByLabelText('สิ่งที่ต้องแก้'), '  พื้นใต้อ่างล้างมือยังเปียก ');
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Rework',
      defect: 'พื้นใต้อ่างล้างมือยังเปียก',
      ...nearBody,
    });
    expect(screen.getByText('ผล: ไม่สะอาด')).toBeInTheDocument();
    expect(screen.getByText('ต้องแก้ไข')).toBeInTheDocument();
    expect(screen.getByText('“พื้นใต้อ่างล้างมือยังเปียก”')).toBeInTheDocument();
    expect(
      screen.getByText('แม่บ้านทดสอบเห็นสิ่งที่ต้องแก้นี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('ถ้าไม่ได้แก้จนรอบ 13:00–15:00 เริ่ม การ์ดขึ้นรอบใหม่ และรอบนี้บันทึกว่า "ไม่ได้แก้"'),
    ).toBeInTheDocument();
  });

  it('with no verdict chosen it asks for one and sends nothing (plan 9 decision 4)', async () => {
    fakeGps(nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(screen.getByRole('alert')).toHaveTextContent('เลือกผลการตรวจก่อน');
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);
  });

  it('the defect goes only with ไม่สะอาด: switching back to สะอาด sends none', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.type(screen.getByLabelText('สิ่งที่ต้องแก้'), 'คราบน้ำ');
    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' });
    expect(server.calls.find((c) => c.path === '/api/inspections')?.body).toEqual({
      qrToken: 'token-restroom-m1',
      scanRecordId: 501,
      result: 'Passed',
      ...nearBody,
    });
  });

  it('the form is locked while recording, so a second tap cannot write a second inspection', async () => {
    fakeGps(nearSign, 'pending');
    serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'ไม่สะอาด' })).toBeDisabled();
  });

  it('a late submission that reported an issue says both', async () => {
    fakeGps(nearSign);
    serve(
      {},
      inspectionScan({
        submission: submission({ status: 'Issue', issueTags: ['น้ำยาหมด'], note: 'ก๊อกรั่ว', placement: 'Late' }),
      }),
    );
    await renderApp(SIGN);

    expect(
      await screen.findByText('10:05 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · พบปัญหา: น้ำยาหมด · ก๊อกรั่ว · ส่งช้า'),
    ).toBeInTheDocument();
  });

  it('from outside the radius the page warns, and the saved result shows the flag (facility-0037)', async () => {
    fakeGps(farFromSign);
    serve(
      { 'POST /api/inspections': () => ({ status: 201, body: recorded({ distanceM: 80, withinRadius: false }) }) },
      inspectionScan({ distanceM: 80, withinRadius: false }),
    );
    const { user } = await renderApp(SIGN);

    expect(
      await screen.findByText('ห่างป้าย 80 ม. · ±40 ม. · นอกรัศมี ผลตรวจจากตรงนี้จะขึ้นว่าไกลป้ายให้แอดมินเห็น'),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('ไกลป้าย: ห่างป้าย 80 ม. แอดมินจะเห็นรายการนี้')).toBeInTheDocument();
  });

  it('a covered building says the inspection is a cover (facility-0048)', async () => {
    fakeGps(nearSign);
    serve({}, inspectionScan({ isCover: true }));
    await renderApp(SIGN);

    expect(await screen.findByText('ตรวจแทนตึก A ในกะนี้ (แอดมินมอบหมาย)')).toBeInTheDocument();
  });

  it('no submission in the current round: the reason, the point, and no inspect button (M16, facility-0047 rule 7)', async () => {
    fakeGps(nearSign);
    serve({}, nothingToInspect());
    await renderApp(SIGN);

    expect(
      await screen.findByRole('heading', { name: 'ยังไม่มีงานให้ตรวจ รอบนี้แม่บ้านยังไม่ส่งงาน' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(screen.getByText('รอบปัจจุบัน 10:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('ห่างป้าย 5 ม. · ±12 ม.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'บันทึกผลตรวจ' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('a rework still owed: the reason and the defect that was asked for (facility-0029)', async () => {
    fakeGps(nearSign);
    serve(
      {},
      nothingToInspect('รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ', {
        point: point({ status: 'Rework', rework: { defect: 'พื้นยังเปียก', requestedAt: '2026-10-15T02:40:00Z' } }),
      }),
    );
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'รอแม่บ้านส่งงานแก้ ยังไม่มีงานใหม่ให้ตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('ให้แก้ 09:40: “พื้นยังเปียก”')).toBeInTheDocument();
  });

  it('new work since the scan (409): the page scans again, clears the verdict and says why (decision 5)', async () => {
    fakeGps(nearSign);
    let scans = 0;
    const newer = inspectionScan({ submission: submission({ id: 502, submittedAt: '2026-10-15T03:12:00Z' }) });
    const server = serve({
      'POST /api/inspections/scan': () => {
        scans += 1;
        return { status: 200, body: scans === 1 ? inspectionScan() : newer };
      },
      'POST /api/inspections': (request) =>
        (request.body as { scanRecordId: number }).scanRecordId === 501
          ? { status: 409, body: { message: 'มีการส่งงานใหม่หลังจากที่คุณสแกน ให้สแกนป้ายอีกครั้ง' } }
          : { status: 201, body: recorded({ scanRecordId: 502 }) },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('10:12 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · เรียบร้อย')).toBeInTheDocument();
    expect(
      screen.getByText('งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ ผลยังไม่ได้บันทึก หน้านี้เปิดงานล่าสุดให้แล้ว'),
    ).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'สะอาด' })).not.toBeChecked();

    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(
      server.calls
        .filter((c) => c.path === '/api/inspections')
        .map((c) => (c.body as { scanRecordId: number }).scanRecordId),
    ).toEqual([501, 502]);
  });

  it('a refusal (400) stays on the form with the server message', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 400, body: { message: 'ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด' } }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ต้องเลือกผลการตรวจ: สะอาด หรือ ไม่สะอาด');
    expect(screen.getByRole('button', { name: 'บันทึกผลตรวจ' })).toBeEnabled();
  });

  it('an unknown or closed sign shows the server message (404)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections/scan': () => ({ status: 404, body: { message: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' } }) });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('every visit to the sign scans again with a fresh location (decision 3)', async () => {
    const gps = fakeGps(nearSign);
    const server = serve({ 'GET /api/my-building': () => ({ status: 200, body: myBuilding() }) });
    const { router } = await renderApp(SIGN);
    await screen.findByRole('heading', { name: 'ห้องน้ำชาย' });

    await act(() => router.navigate('/my-building'));
    await screen.findByRole('heading', { name: 'ตึกของฉัน' });
    await act(() => router.navigate(SIGN));
    await screen.findByRole('heading', { name: 'ห้องน้ำชาย' });

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/inspections/scan')).toHaveLength(2));
    expect(gps.calls).toBe(2);
  });

  it('after a 409 the old submission is not shown while the page scans again (decision 5)', async () => {
    fakeGps(nearSign, nearSign, 'pending');
    serve({
      'POST /api/inspections': () => ({ status: 409, body: { message: 'มีการส่งงานใหม่หลังจากที่คุณสแกน ให้สแกนป้ายอีกครั้ง' } }),
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('กำลังอ่านตำแหน่งและเปิดงานตรวจ...')).toBeInTheDocument();
    expect(screen.queryByText('10:05 · แม่บ้านทดสอบ · รอบ 10:00–12:00 · เรียบร้อย')).not.toBeInTheDocument();
  });
});

describe('InspectPage when something stands in the way (M2, M8, M12)', () => {
  it('Location off when the sign opens: the help, and the retry scans with a new fix (M2, facility-0036)', async () => {
    const gps = fakeGps('denied', nearSign);
    const server = serve();
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนตรวจงาน' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/inspections/scan')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(gps.calls).toBe(2);
    expect(server.calls.filter((c) => c.path === '/api/inspections/scan')).toHaveLength(1);
  });

  it('a page on plain http cannot read a location, and there is nothing to retry (facility-0049)', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    serve();
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'อ่านตำแหน่งไม่ได้' })).toBeInTheDocument();
    expect(
      screen.getByText('หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้ตรวจงาน แจ้งแอดมิน'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();
  });

  it('another building is blocked as the sign opens, with the cover advice (M8, facility-0048)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections/scan': () => ({ status: 403, body: otherBuildingBlocked }) });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'จุดนี้ไม่ได้อยู่ในตึกของคุณ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้ตรวจงาน')).toBeInTheDocument();
    expect(screen.getByText('Area ตึกอื่น (BR01) · ตึก B')).toBeInTheDocument();
    expect(
      screen.getByText(
        'แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณตรวจแทนตึก B ในกะนี้ เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ',
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับไปตึกของฉัน' })).toHaveAttribute('href', '/my-building');
  });

  it('the own shift ending before the press blocks the press (M8)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 403, body: outsideShiftBlocked }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(
      await screen.findByRole('heading', { name: 'ตอนนี้ไม่ใช่กะของคุณ ตรวจได้เฉพาะในกะของตัวเอง' }),
    ).toBeInTheDocument();
    expect(screen.getByText('ไม่ได้ตรวจงาน')).toBeInTheDocument();
  });

  it('no signal when the sign opens: ลองอีกครั้ง scans again', async () => {
    fakeGps(nearSign);
    let scans = 0;
    serve({
      'POST /api/inspections/scan': () => {
        scans += 1;
        if (scans === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 200, body: inspectionScan() };
      },
    });
    const { user } = await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'เปิดงานตรวจไม่ได้' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(scans).toBe(2);
  });

  it('no signal on บันทึกผลตรวจ keeps the verdict, and ส่งใหม่ sends it again with a fresh location (facility-0052)', async () => {
    const gps = fakeGps(nearSign);
    let tries = 0;
    const server = serve({
      'POST /api/inspections': () => {
        tries += 1;
        if (tries === 1) {
          throw new TypeError('Failed to fetch');
        }
        return { status: 201, body: recorded({ result: 'Rework', newPointStatus: 'Rework' }) };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'ไม่สะอาด' }));
    await user.type(screen.getByLabelText('สิ่งที่ต้องแก้'), 'พื้นเปียก');
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('ไม่สะอาด: พื้นเปียก')).toBeInTheDocument();
    expect(gps.calls).toBe(2);

    await user.click(screen.getByRole('button', { name: 'ส่งใหม่' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(gps.calls).toBe(3);
    const sends = server.calls.filter((c) => c.path === '/api/inspections');
    expect(sends).toHaveLength(2);
    expect(sends[1].body).toEqual(sends[0].body);
  });

  it('a server fault (503) is not saved either', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/inspections': () => ({ status: 503 }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('สะอาด')).toBeInTheDocument();
  });

  it('a press with no answer ends at 20 s as not saved (facility-0052)', async () => {
    fakeGps(nearSign);
    const server = serve({ 'POST /api/inspections': () => new Promise<never>(() => {}) });
    const { user } = await renderApp(SIGN);
    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    const send = screen.getByRole('button', { name: 'บันทึกผลตรวจ' });

    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      await user.click(send);
      await vi.waitFor(() => expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(true));
      await vi.advanceTimersByTimeAsync(SEND_TIMEOUT_MS - 1_000);
      expect(screen.getByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();

      await vi.advanceTimersByTimeAsync(1_000);

      expect(await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' })).toBeInTheDocument();
    } finally {
      vi.useRealTimers();
    }
  });

  it('Location off on บันทึกผลตรวจ: the help, and the retry records the same verdict (M2)', async () => {
    fakeGps(nearSign, 'denied', nearSign);
    const server = serve({ 'POST /api/inspections': () => ({ status: 201, body: recorded() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนบันทึกผลตรวจ' })).toBeInTheDocument();
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);

    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect((server.calls.find((c) => c.path === '/api/inspections')?.body as { result: string }).result).toBe('Passed');
  });

  it('ส่งใหม่ cannot be pressed again while the resend is in flight (one send at a time)', async () => {
    fakeGps(nearSign);
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    let tries = 0;
    const server = serve({
      'POST /api/inspections': async () => {
        tries += 1;
        if (tries === 1) {
          throw new TypeError('Failed to fetch');
        }
        await gate;
        return { status: 201, body: recorded() };
      },
    });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));
    await screen.findByRole('heading', { name: 'ยังไม่ได้บันทึกผลตรวจ' });
    await user.click(screen.getByRole('button', { name: 'ส่งใหม่' }));

    const busy = await screen.findByRole('button', { name: 'กำลังส่ง...' });
    expect(busy).toBeDisabled();
    await user.click(busy);
    release();

    expect(await screen.findByRole('heading', { name: 'บันทึกผลตรวจแล้ว 10:15' })).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/inspections')).toHaveLength(2);
  });

  it('the Location retry after a press is disabled while the new reading is pending (one send at a time)', async () => {
    fakeGps(nearSign, 'denied', 'pending');
    const server = serve();
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));
    await screen.findByRole('heading', { name: 'เปิดตำแหน่งก่อนบันทึกผลตรวจ' });
    await user.click(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'เปิดแล้ว ลองอีกครั้ง' })).toBeDisabled());
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);
  });
});
