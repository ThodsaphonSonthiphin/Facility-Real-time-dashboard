import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { cleanerSession, myWork, point } from '../../../test/cleanerFixtures';
import { fakeGps, nearSign } from '../../../test/fakeGps';
import { authAnswer, fakeServer } from '../../../test/fakeServer';
import { renderApp } from '../../../test/renderApp';
import { inspectionScan } from '../../../test/supervisorFixtures';

function serveCleaner(routes: Record<string, () => { status: number; body?: unknown }> = {}) {
  return fakeServer({
    ...cleanerSession,
    'GET /api/my-work': () => ({ status: 200, body: myWork() }),
    'GET /api/schedules/me': () => ({
      status: 200,
      body: {
        weekStart: '2026-11-16',
        revision: 'rev-1',
        readAt: '2026-11-17T02:00:00Z',
        availability: 'available',
        rows: [],
        relief: [],
      },
    }),
    'GET /api/attendance/signs/token-checkin': () => ({ status: 204 }),
    'GET /api/attendance/me': () => ({
      status: 200,
      body: { shift: 'Day', events: [], allowed: ['ShiftIn'], lifecycleVersion: 'Numbered6' },
    }),
    'GET /api/service-points/by-token/token-point': () => ({ status: 200, body: point() }),
    ...routes,
  });
}

describe('CleanerBottomNav (R-01 Option A)', () => {
  it('renders the 4 cleaner tabs with Thai labels on /my-work and marks งานของฉัน current', async () => {
    serveCleaner();
    await renderApp('/my-work');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const items = Array.from(nav.querySelectorAll('a, button'));
    expect(items.map((el) => el.textContent)).toEqual(['งานของฉัน', 'ลงเวลา', 'สแกน QR', 'ตารางเวร']);
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'ตารางเวร' })).toHaveAttribute('href', '/my-schedule');
  });

  it('marks ตารางเวร current on /my-schedule', async () => {
    serveCleaner();
    await renderApp('/my-schedule');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    expect(within(nav).getByRole('link', { name: 'ตารางเวร' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).not.toHaveAttribute('aria-current');
  });

  it('marks ลงเวลา current on /check-in/:token and สแกน QR current on /scan/:token', async () => {
    serveCleaner();
    await renderApp('/check-in/token-checkin');

    const checkInNav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    expect(within(checkInNav).getByRole('button', { name: 'ลงเวลา' })).toHaveAttribute('aria-current', 'page');

    serveCleaner();
    await renderApp('/scan/token-point');

    const scanNav = await screen.findAllByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const latestNav = scanNav[scanNav.length - 1];
    expect(within(latestNav).getByRole('button', { name: 'สแกน QR' })).toHaveAttribute('aria-current', 'page');
  });

  it('toggles inline QR camera guidance when tapping ลงเวลา or สแกน QR on /my-work without navigating to fake tokens', async () => {
    serveCleaner();
    const { user, router } = await renderApp('/my-work');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    await user.click(within(nav).getByRole('button', { name: 'ลงเวลา' }));

    expect(router.state.location.pathname).toBe('/my-work');
    expect(screen.getByRole('status')).toHaveTextContent(/สแกนป้ายลงเวลาด้วยกล้องมือถือ/);

    // Tapping สแกน QR switches the inline guidance to point QR instructions
    await user.click(within(nav).getByRole('button', { name: 'สแกน QR' }));
    expect(router.state.location.pathname).toBe('/my-work');
    expect(screen.getByRole('status')).toHaveTextContent(/สแกนป้าย QR ที่จุดทำความสะอาด/);

    // Tapping สแกน QR again closes the guidance
    await user.click(within(nav).getByRole('button', { name: 'สแกน QR' }));
    expect(screen.queryByText(/สแกนป้าย QR ที่จุดทำความสะอาด/)).not.toBeInTheDocument();

    // Never renders fake /check-in/ or /scan/ links
    for (const link of within(nav).getAllByRole('link')) {
      expect(link.getAttribute('href')).not.toMatch(/^\/(check-in|scan)/);
    }
  });

  it('never renders for supervisor or admin', async () => {
    fakeServer({
      'POST /api/auth/refresh': () => authAnswer('supervisor'),
      'GET /api/my-building': () => ({
        status: 200,
        body: { buildings: [], areas: [], shift: 'Day', asOf: '2026-10-15T03:00:00Z' },
      }),
    });
    await renderApp('/my-building');
    await screen.findByRole('heading', { name: 'ตึกของฉัน' });
    expect(screen.queryByRole('navigation', { name: 'เมนูหลักแม่บ้าน' })).not.toBeInTheDocument();

    fakeServer({
      'POST /api/auth/refresh': () => authAnswer('admin'),
      'GET /api/admin/areas/1': () => ({
        status: 200,
        body: {
          area: {
            id: 1,
            code: 'AR01',
            name: 'Area 1',
            buildingId: 1,
            buildingCode: 'A',
            buildingName: 'ตึก A',
            active: true,
          },
          checkInSign: { id: 10, qrToken: 'tok', hasLocation: false },
          points: [],
        },
      }),
    });
    await renderApp('/admin/areas/1/capture');
    await screen.findByText(/AR01/);
    expect(screen.queryByRole('navigation', { name: 'เมนูหลักแม่บ้าน' })).not.toBeInTheDocument();
  });

  it('does not open QR guidance when tapping the active ลงเวลา or สแกน QR tab on its tokenized route', async () => {
    serveCleaner();
    const checkInSession = await renderApp('/check-in/token-checkin');
    const checkInNav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    await checkInSession.user.click(within(checkInNav).getByRole('button', { name: 'ลงเวลา' }));
    expect(screen.queryByText(/สแกนป้ายลงเวลาด้วยกล้องมือถือ/)).not.toBeInTheDocument();

    serveCleaner();
    const scanSession = await renderApp('/scan/token-point');
    const scanNavs = await screen.findAllByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const latestNav = scanNavs[scanNavs.length - 1];
    await scanSession.user.click(within(latestNav).getByRole('button', { name: 'สแกน QR' }));
    expect(screen.queryByText(/สแกนป้าย QR ที่จุดทำความสะอาด/)).not.toBeInTheDocument();
  });

  it('never renders for a supervisor on /scan/:token', async () => {
    fakeGps(nearSign);
    fakeServer({
      'POST /api/auth/refresh': () => authAnswer('supervisor'),
      'POST /api/inspections/scan': () => ({ status: 200, body: inspectionScan() }),
    });
    await renderApp('/scan/token-point');
    await screen.findByRole('heading', { name: 'ห้องน้ำชาย' });
    expect(screen.queryByRole('navigation', { name: 'เมนูหลักแม่บ้าน' })).not.toBeInTheDocument();
  });
});
