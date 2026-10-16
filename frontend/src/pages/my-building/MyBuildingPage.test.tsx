import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { point } from '../../test/cleanerFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { renderApp } from '../../test/renderApp';
import { lastScanAt, myBuilding, section, supervisorSession } from '../../test/supervisorFixtures';

function serve(answer: unknown = myBuilding(), routes: FakeRoutes = {}) {
  return fakeServer({ ...supervisorSession, 'GET /api/my-building': () => ({ status: 200, body: answer }), ...routes });
}

const LIVE = 'หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ';

/** Wireframe M15's points in the server's card order (not by what waits for the Supervisor). */
const fourPoints = [
  point({ id: 1, name: 'โถงทางเดิน', status: 'NotYetDone' }),
  point({
    id: 2,
    name: 'ห้องน้ำหญิง',
    areaId: 2,
    areaCode: 'AR02',
    areaName: 'Area ถัดไป',
    status: 'PendingInspection',
    currentRound: { id: 21, start: '09:00:00', end: '11:00:00' },
    lastScan: lastScanAt('2026-10-15T03:12:00Z'), // 10:12
  }),
  point({
    id: 3,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 31, start: '08:00:00', end: '10:00:00' },
    lastScan: lastScanAt('2026-10-15T02:20:00Z'),
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // asked 09:40
  }),
  point({ id: 4, name: 'ห้องน้ำชาย', status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:05:00Z') }), // 10:05
];

/** The hub's first start asks again (facility-0064): wait for that catch-up answer, or it could overwrite a patched card. */
async function settled(server: ReturnType<typeof serve>) {
  await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-building')).toHaveLength(2));
  await waitFor(() => expect(screen.getByRole('button', { name: 'อัปเดต' })).toBeEnabled());
}

const cardNames = (region: HTMLElement) => within(region).getAllByRole('heading', { level: 3 }).map((h) => h.textContent);
const tiles = () =>
  within(screen.getByRole('list', { name: 'สรุปการตรวจ' }))
    .getAllByRole('listitem')
    .map((li) => li.textContent);

describe('MyBuildingPage (M15, facility-0057)', () => {
  it('lists what waits for the Supervisor first, with the summary and the clock shift', async () => {
    serve(myBuilding([section(fourPoints)]));
    await renderApp('/my-building');

    const own = await screen.findByRole('region', { name: 'ตึกทดลอง' });
    expect(cardNames(own)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'จุดทิ้งขยะ', 'โถงทางเดิน']);
    expect(screen.getByRole('heading', { level: 1, name: 'ตึกของฉัน' })).toBeInTheDocument();
    expect(screen.getByText('ตอนนี้ กะเช้า 07:00–19:00')).toBeInTheDocument();
    expect(screen.getByText('2 Area · 4 จุด')).toBeInTheDocument();
    expect(tiles()).toEqual(['2 รอตรวจ', '1 รอแม่บ้านแก้', '0 ตรวจแล้ว']);
    expect(screen.getByText('ส่ง 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('Area ถัดไป · รอบ 09:00–11:00')).toBeInTheDocument();
    expect(screen.getByText('ให้แก้ 09:40: “รอบถังขยะยังมีคราบ” · รอแม่บ้านส่งใหม่')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่ง · ส่งได้ถึง 12:00')).toBeInTheDocument();
    expect(screen.getAllByText('ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ')).toHaveLength(2);
    // facility-0059: only the sign's QR opens the inspection
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('outside the own shift it says so and does not send anyone to scan (facility-0048)', async () => {
    serve(myBuilding([section(fourPoints, { canInspect: false })]));
    await renderApp('/my-building');

    expect(await screen.findByText('ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้')).toBeInTheDocument();
    expect(screen.queryByText('ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ')).not.toBeInTheDocument();
  });

  it('a building covered this shift has its own section after the own building (facility-0048)', async () => {
    serve(
      myBuilding([
        section([point()]),
        section([point({ id: 21, name: 'ห้องประชุม', areaId: 5, buildingCode: 'B' })], {
          buildingId: 2,
          buildingCode: 'B',
          buildingName: 'ตึกข้างเคียง',
          isCover: true,
        }),
      ]),
    );
    await renderApp('/my-building');

    const covered = await screen.findByRole('region', { name: 'ตึกข้างเคียง (ตรวจแทนกะนี้)' });
    expect(cardNames(covered)).toEqual(['ห้องประชุม']);
    expect(screen.getAllByRole('region').map((r) => r.getAttribute('aria-labelledby'))).toEqual(['building-1', 'building-2']);
  });

  it('an account with no building says to ask the Admin', async () => {
    serve(myBuilding([]));
    await renderApp('/my-building');

    expect(await screen.findByText('บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin')).toBeInTheDocument();
  });

  it('a building with no points says so', async () => {
    serve(myBuilding([section([])]));
    await renderApp('/my-building');

    expect(await screen.findByText('ตึกนี้ยังไม่มีจุด')).toBeInTheDocument();
  });

  it('a pushed submission or inspection changes its card at once and moves it ("อัปเดตเอง")', async () => {
    const server = serve(myBuilding([section(fourPoints)]));
    await renderApp('/my-building');
    const own = await screen.findByRole('region', { name: 'ตึกทดลอง' });
    expect(await screen.findByText(LIVE)).toBeInTheDocument();
    await settled(server);
    expect(hub.latest().tokens).toEqual(['token-supervisor']);

    act(() =>
      hub.latest().push('ScanRecorded', { ...fourPoints[0], status: 'PendingInspection', lastScan: lastScanAt('2026-10-15T03:20:00Z') }),
    );

    // RTK Query tells the page about a cache patch on the next frame, so the first look waits for it
    await waitFor(() => expect(cardNames(own)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'โถงทางเดิน', 'จุดทิ้งขยะ']));
    expect(screen.getByText('ส่ง 10:20 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(tiles()).toEqual(['3 รอตรวจ', '1 รอแม่บ้านแก้', '0 ตรวจแล้ว']);

    act(() => hub.latest().push('InspectionRecorded', { ...fourPoints[3], status: 'Passed' }));

    await waitFor(() => expect(cardNames(own)).toEqual(['ห้องน้ำหญิง', 'โถงทางเดิน', 'จุดทิ้งขยะ', 'ห้องน้ำชาย']));
    expect(screen.getByText('ตรวจผ่าน · ส่ง 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(tiles()).toEqual(['2 รอตรวจ', '1 รอแม่บ้านแก้', '1 ตรวจแล้ว']);
  });

  it('a dropped hub says so, and coming back asks the server again (facility-0064)', async () => {
    const server = serve();
    await renderApp('/my-building');
    await screen.findByText(LIVE);
    await settled(server);

    act(() => hub.latest().reconnecting());
    expect(screen.getByText('การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด')).toBeInTheDocument();

    act(() => hub.latest().reconnected());

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-building')).toHaveLength(3));
    expect(screen.getByText(LIVE)).toBeInTheDocument();
  });

  it('"อัปเดต" asks the server again', async () => {
    const server = serve();
    const { user } = await renderApp('/my-building');
    await screen.findByRole('region', { name: 'ตึกทดลอง' });
    await settled(server);

    await user.click(screen.getByRole('button', { name: 'อัปเดต' }));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-building')).toHaveLength(3));
  });

  it('logging out closes the live connection', async () => {
    serve(undefined, { 'POST /api/auth/logout': () => ({ status: 204 }) });
    const { user } = await renderApp('/my-building');
    await screen.findByText(LIVE);

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));

    await waitFor(() => expect(hub.latest().stops).toBe(1));
  });

  it('a failed load shows the error and keeps the heading', async () => {
    fakeServer({ ...supervisorSession, 'GET /api/my-building': () => ({ status: 503 }) });
    await renderApp('/my-building');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
    expect(screen.getByRole('heading', { level: 1, name: 'ตึกของฉัน' })).toBeInTheDocument();
  });
});
