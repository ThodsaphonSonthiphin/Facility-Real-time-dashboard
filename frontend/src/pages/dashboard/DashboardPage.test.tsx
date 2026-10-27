import { setupListeners } from '@reduxjs/toolkit/query';
import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { PointCard } from '../../shared/api/types';
import type { AppStore } from '../../store';
import { adminSession, buildings, managedBuildings } from '../../test/adminFixtures';
import { point } from '../../test/cleanerFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { apiIdle, boardPoints } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo } from '../../test/moreInfo';

function serve(answer: PointCard[] = boardPoints, routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/service-points': () => ({ status: 200, body: answer }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    ...routes,
  });
}

const pointGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/service-points');

/** The hub's first start asks again (facility-0064): wait for that answer, or it could land after a patch. */
async function settled(server: ReturnType<typeof serve>, store: AppStore) {
  await waitFor(() => expect(pointGets(server)).toHaveLength(2));
  await apiIdle(store);
}

const tileNames = (region: HTMLElement) => within(region).getAllByRole('heading', { level: 3 }).map((h) => h.textContent);

describe('DashboardPage (D1, facility-0058, 0061, 0064)', () => {
  it('shows every point by Area, with its status, round and what happened', async () => {
    serve();
    await renderApp('/dashboard');

    const ar01 = await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    expect(tileNames(ar01)).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'จุดทิ้งขยะ', 'โถงทางเดิน']);
    expect(within(ar01).getByText('ตึก A · ตึกทดลอง · 4 จุด')).toBeInTheDocument();
    const ar02 = screen.getByRole('region', { name: 'AR02 Office ทดลอง' });
    expect(tileNames(ar02)).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
    expect(within(ar02).getByText('ตึก B · ตึกข้างเคียง · 3 จุด')).toBeInTheDocument();

    expect(screen.getByText('ส่งงาน 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน · ส่งงาน 10:00 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getByText('แจ้งปัญหา: ก๊อกน้ำรั่ว · อ่างที่ 2')).toBeInTheDocument();
    expect(screen.getByText('ไม่สะอาด 09:40: “รอบถังขยะยังมีคราบ” · รอแม่บ้านแก้')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่งงาน · เลยเวลารอบแล้ว')).toBeInTheDocument();
    expect(within(ar01).getByText('รอบ 07:30–09:00')).toBeInTheDocument();
    expect(within(ar02).getByText('รอบถัดไป 11:00–12:00')).toBeInTheDocument();
    expect(screen.getByText('รอบแรกของกะเริ่ม 11:00')).toBeInTheDocument();
    expect(screen.getByText('ส่งงาน 09:20 · แม่บ้านทดสอบ · ส่งช้า')).toBeInTheDocument();
    expect(screen.getByText('จุดนี้ไม่มีรอบในกะนี้')).toBeInTheDocument();
    // facility-0059: only the sign's QR opens a point, so the one link on the board is the export (D8)
    expect(within(screen.getByRole('main')).getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual(['/admin/export']);
  });

  it('a pushed submission or inspection changes its card at once, without asking the server (#26)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    expect(await screen.findByText('SignalR Live')).toBeInTheDocument();
    await settled(server, store);
    expect(hub.latest().tokens).toEqual(['token-admin']);

    act(() => hub.latest().push('InspectionRecorded', { ...boardPoints[0], status: 'Passed' }));

    // RTK Query tells the page about a cache patch on the next frame, so the first look waits for it
    expect(await screen.findByText('ตรวจผ่าน · ส่งงาน 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(pointGets(server)).toHaveLength(2);
  });

  it('a card for a point the page does not have yet asks the server, for its place in the order', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await screen.findByText('SignalR Live');
    await settled(server, store);

    act(() => hub.latest().push('ScanRecorded', point({ id: 8, name: 'ห้องใหม่', status: 'PendingInspection' })));

    await waitFor(() => expect(pointGets(server)).toHaveLength(3));
  });

  it('a push that lands while an older answer is still on its way is not overwritten by it', async () => {
    const pushed = { ...boardPoints[0], status: 'Passed' as const };
    const afterPush = [pushed, ...boardPoints.slice(1)]; // what the server says once the push is committed
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => (release = resolve));
    let gets = 0;
    const server = serve(boardPoints, {
      'GET /api/service-points': async () => {
        gets += 1;
        if (gets === 2) {
          await held; // the catch-up answer, read from the database before the push below
        }
        return { status: 200, body: gets > 2 ? afterPush : boardPoints };
      },
    });
    const { store } = await renderApp('/dashboard');
    await screen.findByText('SignalR Live');
    await waitFor(() => expect(pointGets(server)).toHaveLength(2));

    act(() => hub.latest().push('InspectionRecorded', pushed));
    release();

    await waitFor(() => expect(pointGets(server)).toHaveLength(3));
    await apiIdle(store);
    expect(screen.getByText('ตรวจผ่าน · ส่งงาน 10:05 · แม่บ้านทดสอบ')).toBeInTheDocument();
  });

  it('a point not yet done shows the round it can still be sent in', async () => {
    serve([point({ id: 9, name: 'ห้องพัก', status: 'NotYetDone' })]);
    await renderApp('/dashboard');

    expect(await screen.findByText('ยังไม่ส่งงาน · ส่งได้ถึง 12:00')).toBeInTheDocument();
    expect(screen.getByText('รอบ 10:00–12:00')).toBeInTheDocument();
  });

  it('asks the server every 30 s, for the changes nobody pushes (facility-0061)', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const server = serve();
      const { store } = await renderApp('/dashboard');
      await settled(server, store);

      await act(() => vi.advanceTimersByTimeAsync(30_000));

      await waitFor(() => expect(pointGets(server)).toHaveLength(3));
    } finally {
      vi.useRealTimers();
    }
  });

  it('asks the server again when the window gets focus (facility-0061)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await settled(server, store);
    const stopListening = setupListeners(store.dispatch);
    try {
      act(() => {
        window.dispatchEvent(new Event('focus'));
      });

      await waitFor(() => expect(pointGets(server)).toHaveLength(3));
    } finally {
      stopListening();
    }
  });

  it('the badge says whether pushes arrive, and a reconnect asks the server again (facility-0064)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    expect(await screen.findByRole('status')).toHaveTextContent('SignalR Live');
    await settled(server, store);

    act(() => hub.latest().reconnecting());
    expect(screen.getByRole('status')).toHaveTextContent('Connecting...');
    act(() => hub.latest().reconnected());

    expect(screen.getByRole('status')).toHaveTextContent('SignalR Live');
    await waitFor(() => expect(pointGets(server)).toHaveLength(3));
  });

  it("shows the server's message when the list cannot be read", async () => {
    serve(boardPoints, {
      'GET /api/service-points': () => ({ status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง' } }),
    });
    await renderApp('/dashboard');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
  });

  it('an empty site says where to add points', async () => {
    serve([]);
    await renderApp('/dashboard');

    expect(await screen.findByText('ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการพื้นที่และป้าย')).toBeInTheDocument();
  });

  it('links to the Excel export (D8)', async () => {
    const server = serve();
    const { store } = await renderApp('/dashboard');
    await settled(server, store);

    expect(within(screen.getByRole('main')).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('href', '/admin/export');
  });

  it('the notes on reading a card wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/dashboard');

    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    expectBehindMoreInfo(/^รอบเป็นช่วงเวลา/, 'วิธีอ่านการ์ด');
  });
});
