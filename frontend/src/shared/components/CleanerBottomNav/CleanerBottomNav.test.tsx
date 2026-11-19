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

describe('CleanerBottomNav (facility-0227)', () => {
  it('renders exactly 2 real navigation tabs on /my-work without สแกน QR, ลงเวลา, or บัญชี', async () => {
    serveCleaner();
    await renderApp('/my-work');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const items = Array.from(nav.querySelectorAll('a, button'));
    expect(items.map((el) => el.textContent)).toEqual(['งานของฉัน', 'ตารางของฉัน']);
    expect(within(nav).queryByText('สแกน QR')).not.toBeInTheDocument();
    expect(within(nav).queryByText('ลงเวลา')).not.toBeInTheDocument();
    expect(within(nav).queryByText('บัญชี')).not.toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'ตารางของฉัน' })).toHaveAttribute('href', '/my-schedule');
  });

  it('marks ตารางของฉัน current on /my-schedule', async () => {
    serveCleaner();
    await renderApp('/my-schedule');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    expect(within(nav).getByRole('link', { name: 'ตารางของฉัน' })).toHaveAttribute('aria-current', 'page');
    expect(within(nav).getByRole('link', { name: 'งานของฉัน' })).not.toHaveAttribute('aria-current');
  });

  it('stays visible with 2 real links on tokenized routes without fabricating QR links', async () => {
    serveCleaner();
    await renderApp('/check-in/token-checkin');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักแม่บ้าน' });
    const links = within(nav).getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/my-work', '/my-schedule']);
    expect(within(nav).queryAllByRole('button')).toHaveLength(0);
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
