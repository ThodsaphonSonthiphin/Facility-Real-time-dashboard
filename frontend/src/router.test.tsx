import { screen, within } from '@testing-library/react';
import type { ReactElement } from 'react';
import type { RouteObject } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { routes } from './router';
import type { Role } from './shared/api/types';
import { authAnswer, fakeServer } from './test/fakeServer';
import { renderApp } from './test/renderApp';

const noSession = { 'POST /api/auth/refresh': () => ({ status: 401 }) };

/** The page routes behind the guard of exactly this one role, through its layouts. */
function pagesOf(role: Role): RouteObject[] {
  const guard = routes[0].children?.find((route) => {
    const roles = (route.element as ReactElement<{ roles?: Role[] }> | undefined)?.props.roles;
    return roles?.length === 1 && roles[0] === role;
  });
  return (guard?.children ?? []).flatMap((layout) => layout.children ?? []);
}

describe('routing (facility-0026, 0058)', () => {
  it('a live session opens the role home without showing the login page', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    const { router } = await renderApp('/');

    expect(await screen.findByRole('heading', { name: 'ตึกของฉัน' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/my-building');
  });

  it('without a session, "/" goes to the login page', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/');

    expect(router.state.location.pathname).toBe('/login');
  });

  it('a page behind login remembers itself for after the login', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/my-work?from=sign');

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/my-work', search: '?from=sign' } });
  });

  it('an Admin page sends a logged-out visitor to the Admin login', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/dashboard');

    expect(router.state.location.pathname).toBe('/login/admin');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/dashboard' } });
  });

  it('a Cleaner opening the Dashboard sees the no-permission screen (facility-0058)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner') });
    await renderApp('/dashboard');

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('หน้านี้สำหรับแอดมิน บัญชีนี้เป็นแม่บ้าน')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับหน้าของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('an Admin page shows the Admin menu with the open page marked', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    await renderApp('/dashboard');

    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'สถานะจุด',
      'การเข้างาน',
      'รายการที่ขึ้นเตือน',
      'สรุปการตรวจ',
      'ส่งออก Excel',
      'พื้นที่และป้าย',
      'ตึก',
      'พิมพ์ป้าย',
      'บัญชีผู้ใช้',
      'ประวัติการแก้ไข',
    ]);
    // Two groups (DESIGN_SPEC §4, #121): following the work, then setting it up
    expect(within(menu).getAllByRole('list')).toHaveLength(2);
    expect(within(menu).getByRole('list', { name: 'ติดตามงาน' })).toBeInTheDocument();
    expect(within(menu).getByRole('list', { name: 'ตั้งค่า' })).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'สถานะจุด' })).toHaveAttribute('aria-current', 'page');
    expect(within(menu).getByRole('link', { name: 'พื้นที่และป้าย' })).toHaveAttribute('href', '/admin/areas');
    expect(within(menu).getByRole('link', { name: 'พิมพ์ป้าย' })).toHaveAttribute('href', '/admin/print');
  });

  it('a phone page has no Admin menu', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner') });
    await renderApp('/my-work');

    expect(screen.queryByRole('navigation', { name: 'เมนูแอดมิน' })).not.toBeInTheDocument();
  });

  it('every Admin page is a lazy route, so a phone never downloads Admin code (facility-0062 item 3)', () => {
    const pages = pagesOf('admin');

    expect(pages.map((page) => page.path)).toEqual([
      'dashboard',
      'admin/attendance',
      'admin/alerts',
      'admin/inspection-summary',
      'admin/export',
      'admin/areas/:areaId?',
      'admin/areas/new',
      'admin/areas/:areaId/edit',
      'admin/buildings',
      'admin/print',
      'admin/accounts',
      'admin/history',
      'admin/areas/:areaId/capture',
    ]);
    expect(pages.every((page) => typeof page.lazy === 'function' && page.element === undefined)).toBe(true);
  });

  it('phone pages stay in the main file, so a scanned sign opens without a second download', () => {
    const phonePages = [...pagesOf('cleaner'), ...pagesOf('supervisor')];

    expect(phonePages.length).toBeGreaterThan(0);
    expect(phonePages.every((page) => page.lazy === undefined)).toBe(true);
  });

  it('an Admin page opens from the menu once its file has loaded', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    const { user } = await renderApp('/dashboard');

    await user.click(screen.getByRole('link', { name: 'บัญชีผู้ใช้' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'บัญชีผู้ใช้' })).toBeInTheDocument();
  });

  it('an unknown address shows not-found', async () => {
    fakeServer(noSession);
    await renderApp('/no-such-page');

    expect(screen.getByRole('heading', { name: 'ไม่พบหน้านี้' })).toBeInTheDocument();
  });

  it('logout ends the session on the server and the next login starts at the own home', async () => {
    const server = fakeServer({
      'POST /api/auth/refresh': () => authAnswer('cleaner'),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const { user, router, store } = await renderApp('/my-work');
    expect(screen.getByText('แม่บ้านทดสอบ')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toBeNull();
    expect(store.getState().auth.accessToken).toBeNull();
    expect(server.calls.some((c) => c.path === '/api/auth/logout')).toBe(true);
  });

  it('a page that throws shows the crash screen with a reload button (facility-0068)', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    fakeServer(noSession);
    const Broken = () => {
      throw new Error('boom');
    };
    const [root] = routes;
    const withBrokenPage: RouteObject[] = [
      { path: root.path, errorElement: root.errorElement, children: [...(root.children ?? []), { path: 'broken', element: <Broken /> }] },
    ];

    await renderApp('/broken', withBrokenPage);

    expect(screen.getByRole('heading', { name: 'เกิดข้อผิดพลาด' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'โหลดใหม่' })).toBeInTheDocument();
  });
});
