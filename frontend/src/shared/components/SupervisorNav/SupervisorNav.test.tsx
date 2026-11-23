import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
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
  const originalMatchMedia = window.matchMedia;

  afterEach(() => {
    Object.defineProperty(window, 'matchMedia', { writable: true, configurable: true, value: originalMatchMedia });
    vi.restoreAllMocks();
  });

  it('renders the unified 3-link top nav on tablet/desktop across /my-building, /schedule-management, and /my-account', async () => {
    serveSupervisor();
    const { user, router } = await renderApp('/my-building');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหัวหน้างาน' });
    const myBuildingLink = within(nav).getByRole('link', { name: 'ตึกของฉัน' });
    const scheduleLink = within(nav).getByRole('link', { name: 'จัดตารางงาน' });
    const accountLink = within(nav).getByRole('link', { name: 'บัญชี' });
    expect(myBuildingLink).toHaveAttribute('href', '/my-building');
    expect(myBuildingLink).toHaveAttribute('aria-current', 'page');
    expect(scheduleLink).toHaveAttribute('href', '/schedule-management');
    expect(accountLink).toHaveAttribute('href', '/my-account');

    await user.click(scheduleLink);
    expect(router.state.location.pathname).toBe('/schedule-management');
    const scheduleNav = await screen.findByRole('navigation', { name: 'เมนูหัวหน้างาน' });
    expect(within(scheduleNav).getByRole('link', { name: 'จัดตารางงาน' })).toHaveAttribute('aria-current', 'page');

    await user.click(within(scheduleNav).getByRole('link', { name: 'บัญชี' }));
    expect(router.state.location.pathname).toBe('/my-account');
    expect(await screen.findByRole('heading', { name: 'บัญชีของฉัน' })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'เมนูหัวหน้างาน' })).getByRole('link', { name: 'บัญชี' })).toHaveAttribute('aria-current', 'page');
  });

  it('renders the 3-tab mobile bottom nav consistently on narrow viewports (< 768px)', async () => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      configurable: true,
      value: vi.fn((query: string) => ({
        matches: query === '(max-width: 767px)',
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });
    serveSupervisor();
    const { user, router } = await renderApp('/my-building');

    const nav = await screen.findByRole('navigation', { name: 'เมนูหลักหัวหน้างาน' });
    expect(within(nav).getByRole('link', { name: 'ตึกของฉัน' })).toHaveAttribute('aria-current', 'page');

    await user.click(within(nav).getByRole('link', { name: 'จัดตารางงาน' }));
    expect(router.state.location.pathname).toBe('/schedule-management');
    const scheduleBottomNav = await screen.findByRole('navigation', { name: 'เมนูหลักหัวหน้างาน' });
    expect(within(scheduleBottomNav).getByRole('link', { name: 'จัดตารางงาน' })).toHaveAttribute('aria-current', 'page');

    await user.click(within(scheduleBottomNav).getByRole('link', { name: 'บัญชี' }));
    expect(router.state.location.pathname).toBe('/my-account');
    expect(await screen.findByRole('heading', { name: 'บัญชีของฉัน' })).toBeInTheDocument();
  });
});
