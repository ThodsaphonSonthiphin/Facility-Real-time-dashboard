import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { fakeServer } from '../../../test/fakeServer';
import { renderApp } from '../../../test/renderApp';
import { myBuilding, supervisorSession } from '../../../test/supervisorFixtures';

function serveSupervisor() {
  return fakeServer({
    ...supervisorSession,
    'GET /api/my-building': () => ({ status: 200, body: myBuilding() }),
    'GET /api/schedules': () => ({
      status: 200,
      body: {
        weekStart: '2026-11-16',
        revision: 'digest-1',
        readAt: '2026-11-17T02:00:00Z',
        availability: 'available',
        rows: [],
        relief: [],
      },
    }),
  });
}

describe('SupervisorNav & SupervisorBottomNav', () => {
  it('renders the 3-tab mobile bottom nav consistently on /my-building, /schedule-management, and /my-account', async () => {
    serveSupervisor();
    const { user, router } = await renderApp('/my-building');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักหัวหน้างาน' });
    const myBuildingLink = within(nav).getByRole('link', { name: 'ตึกของฉัน' });
    const scheduleLink = within(nav).getByRole('link', { name: 'จัดตารางงาน' });
    const accountLink = within(nav).getByRole('link', { name: 'บัญชี' });
    expect(myBuildingLink).toHaveAttribute('href', '/my-building');
    expect(myBuildingLink).toHaveAttribute('aria-current', 'page');
    expect(scheduleLink).toHaveAttribute('href', '/schedule-management');
    expect(accountLink).toHaveAttribute('href', '/my-account');

    await user.click(scheduleLink);
    expect(router.state.location.pathname).toBe('/schedule-management');
    const desktopNav = await screen.findByRole('navigation', { name: 'เมนูหัวหน้างาน' });
    expect(within(desktopNav).getByRole('link', { name: 'จัดตารางงาน' })).toHaveAttribute('aria-current', 'page');

    await user.click(within(desktopNav).getByRole('link', { name: 'ตึกของฉัน' }));
    expect(router.state.location.pathname).toBe('/my-building');
    const mobileNavAgain = await screen.findByRole('navigation', { name: 'เมนูหลักหัวหน้างาน' });
    await user.click(within(mobileNavAgain).getByRole('link', { name: 'บัญชี' }));
    expect(router.state.location.pathname).toBe('/my-account');
    expect(await screen.findByRole('heading', { name: 'บัญชีของฉัน' })).toBeInTheDocument();
  });
});
