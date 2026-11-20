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
      body: { shift: 'Day', events: [], allowed: ['ShiftIn'],  },
    }),
    'GET /api/service-points/by-token/token-point': () => ({ status: 200, body: point() }),
    ...routes,
  });
}

describe('CleanerBottomNav (3-tab mobile navigation + Focused Task Mode)', () => {
  it('renders 3 real navigation tabs on /my-work (งานของฉัน, ตารางของฉัน, บัญชี) without QR fabrication', async () => {
    serveCleaner();
    await renderApp('/my-work');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const items = Array.from(nav.querySelectorAll('a, button'));
    expect(items.map((el) => el.textContent)).toEqual(['งานของฉัน', 'ตารางของฉัน', 'บัญชี']);
    expect(within(nav).queryByText('สแกน QR')).not.toBeInTheDocument();
    expect(within(nav).queryByText('ลงเวลา')).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'ตารางของฉัน' })).toHaveAttribute('href', '/my-schedule');
    expect(within(nav).getByRole('link', { name: 'บัญชี' })).toHaveAttribute('href', '/my-account');
  });

  it('marks ตารางของฉัน current on /my-schedule and บัญชี current on /my-account', async () => {
    serveCleaner();
    const { user } = await renderApp('/my-schedule');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    expect(within(nav).getByRole('link', { name: 'ตารางของฉัน' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).not.toHaveAttribute('aria-current');

    await user.click(within(nav).getByRole('link', { name: 'บัญชี' }));
    expect(await screen.findByRole('heading', { name: 'บัญชีของฉัน' })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'เมนูหลักแม่บ้าน' })).getByRole('link', { name: 'บัญชี' }))
      .toHaveAttribute('aria-current', 'page');
  });

  it('hides bottom navigation on focused QR check-in flow so bottom CTAs never double-stack', async () => {
    serveCleaner();
    await renderApp('/check-in/token-checkin');

    expect(await screen.findByRole('heading', { name: 'ลงเวลา' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'เมนูหลักแม่บ้าน' })).not.toBeInTheDocument();
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
