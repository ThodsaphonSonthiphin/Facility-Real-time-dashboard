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

describe('SupervisorNav (facility-0228)', () => {
  it('renders both links with ตึกของฉัน active on /my-building and switches to /schedule-management', async () => {
    serveSupervisor();
    const { user, router } = await renderApp('/my-building');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหัวหน้างาน' });
    const myBuildingLink = within(nav).getByRole('link', { name: 'ตึกของฉัน' });
    const scheduleLink = within(nav).getByRole('link', { name: 'จัดตารางงาน' });
    expect(myBuildingLink).toHaveAttribute('href', '/my-building');
    expect(myBuildingLink).toHaveAttribute('aria-current', 'page');
    expect(scheduleLink).toHaveAttribute('href', '/schedule-management');
    expect(scheduleLink).not.toHaveAttribute('aria-current');

    await user.click(scheduleLink);
    expect(router.state.location.pathname).toBe('/schedule-management');
    const scheduleNav = await screen.findByRole('navigation', { name: 'เมนูหัวหน้างาน' });
    expect(within(scheduleNav).getByRole('link', { name: 'จัดตารางงาน' })).toHaveAttribute('aria-current', 'page');
    expect(within(scheduleNav).getByRole('link', { name: 'ตึกของฉัน' })).not.toHaveAttribute('aria-current');
  });
});
