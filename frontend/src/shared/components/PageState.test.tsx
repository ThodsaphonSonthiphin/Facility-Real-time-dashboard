import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, MemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { Splash } from './Boxes';
import { CrashScreen } from './CrashScreen';
import { PageDisabled, PageEmpty, PageLoading } from './PageState';
import { StatusScreen } from './StatusScreen';

describe('page states (DESIGN_SPEC §6, #122)', () => {
  it('a page waiting for its answer says so and is marked busy', () => {
    const { rerender } = render(<PageLoading />);
    expect(screen.getByText('กำลังโหลด…').closest('[aria-busy]')).toHaveAttribute('aria-busy', 'true');

    rerender(<Splash>กำลังเปิดป้าย...</Splash>);
    expect(screen.getByText('กำลังเปิดป้าย...')).toHaveAttribute('aria-busy', 'true');
  });

  it('an empty list and a turned-off thing say what that means', () => {
    render(
      <>
        <PageEmpty title="ยังไม่มีรายการที่ขึ้นเตือน">เมื่อมีการสแกนผิดพื้นที่ จะขึ้นที่นี่</PageEmpty>
        <PageDisabled>พื้นที่นี้ปิดใช้งานแล้ว</PageDisabled>
      </>,
    );

    expect(screen.getByText('ยังไม่มีรายการที่ขึ้นเตือน')).toBeInTheDocument();
    expect(screen.getByText('เมื่อมีการสแกนผิดพื้นที่ จะขึ้นที่นี่')).toBeInTheDocument();
    expect(screen.getByText('พื้นที่นี้ปิดใช้งานแล้ว')).toBeInTheDocument();
  });

  it('StatusScreen renders title, detail, optional link, and decorative Lucide icon', () => {
    const { container } = render(
      <MemoryRouter>
        <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: 'ไม่พบหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('ที่อยู่นี้ไม่มีในระบบ')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับหน้าแรก' })).toHaveAttribute('href', '/');
    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('CrashScreen logs error to console only and reloads on button click (facility-0068)', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });

    function Boom(): never {
      throw new Error('boom');
    }

    const router = createMemoryRouter(
      [{ path: '/', element: <Boom />, errorElement: <CrashScreen /> }],
      { initialEntries: ['/'] },
    );
    const user = userEvent.setup();
    const { container } = render(<RouterProvider router={router} />);

    expect(await screen.findByRole('heading', { name: 'เปิดหน้านี้ไม่ได้' })).toBeInTheDocument();
    expect(screen.getByText('กด “โหลดใหม่” เพื่อลองเปิดหน้านี้อีกครั้ง')).toBeInTheDocument();
    expect(consoleError).toHaveBeenCalled();
    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute('aria-hidden', 'true');

    await user.click(screen.getByRole('button', { name: 'โหลดใหม่' }));
    expect(reload).toHaveBeenCalledOnce();

    vi.unstubAllGlobals();
    consoleError.mockRestore();
  });
});
