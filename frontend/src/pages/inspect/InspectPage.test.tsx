import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { farFromSign, fakeGps, nearSign } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import {
  inspectionScan,
  nothingToInspect,
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
    expect(screen.queryByLabelText('ข้อบกพร่อง')).not.toBeInTheDocument();
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

    expect(screen.getByRole('alert')).toHaveTextContent('ใส่ข้อบกพร่องก่อนบันทึก');
    expect(server.calls.some((c) => c.path === '/api/inspections')).toBe(false);

    await user.type(screen.getByLabelText('ข้อบกพร่อง'), '  พื้นใต้อ่างล้างมือยังเปียก ');
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
      screen.getByText('แม่บ้านทดสอบเห็นข้อบกพร่องนี้ทันทีในหน้างานของฉัน และเมื่อสแกนจุดนี้ แก้แล้วส่งงานใหม่ จากนั้นหัวหน้าตรวจอีกรอบ'),
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
    await user.type(screen.getByLabelText('ข้อบกพร่อง'), 'คราบน้ำ');
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
      await screen.findByText('ห่างป้าย 80 ม. · ±40 ม. · นอกรัศมี ผลตรวจจากตรงนี้จะติดธงให้ Admin เห็น'),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('radio', { name: 'สะอาด' }));
    await user.click(screen.getByRole('button', { name: 'บันทึกผลตรวจ' }));

    expect(await screen.findByText('ติดธง: ห่างป้าย 80 ม. Admin จะเห็นรายการนี้')).toBeInTheDocument();
  });

  it('a covered building says the inspection is a cover (facility-0048)', async () => {
    fakeGps(nearSign);
    serve({}, inspectionScan({ isCover: true }));
    await renderApp(SIGN);

    expect(await screen.findByText('ตรวจแทนตึก A ในกะนี้ (Admin มอบหมาย)')).toBeInTheDocument();
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
});
