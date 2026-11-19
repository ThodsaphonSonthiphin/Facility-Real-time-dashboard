import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

describe('AccountPage (/my-account) and LogoutConfirmDialog', () => {
  it('shows profile details for Cleaner and cancels logout cleanly without ending session', async () => {
    const server = fakeServer({
      'POST /api/auth/refresh': () => authAnswer('cleaner'),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const { user, router } = await renderApp('/my-account');

    expect(await screen.findByRole('heading', { name: 'บัญชีของฉัน' })).toBeInTheDocument();
    expect(screen.getByText('แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getAllByText('แม่บ้าน').length).toBeGreaterThanOrEqual(1);

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'ยืนยันออกจากระบบ' });
    await user.click(within(dialog).getByRole('button', { name: 'ยกเลิก' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/my-account');
    expect(server.calls.some((call) => call.path === '/api/auth/logout')).toBe(false);
  });

  it('signs out Supervisor only after confirming in LogoutConfirmDialog', async () => {
    const server = fakeServer({
      'POST /api/auth/refresh': () => authAnswer('supervisor'),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const { user, router } = await renderApp('/my-account');

    expect(await screen.findByText('หัวหน้าทดสอบ')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'ยืนยันออกจากระบบ' });
    await user.click(within(dialog).getByRole('button', { name: 'ยืนยันออกจากระบบ' }));

    expect(router.state.location.pathname).toBe('/login');
    expect(server.calls.some((call) => call.path === '/api/auth/logout')).toBe(true);
  });
});
