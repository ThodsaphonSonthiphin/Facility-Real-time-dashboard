import { act, render, screen, waitFor, within } from '@testing-library/react';
import { Provider } from 'react-redux';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { fakeServer, type FakeResponse } from '../../test/fakeServer';
import { attendanceGpsAlert } from '../../test/liveFixtures';
import { AdminNav } from './AdminNav';

function renderMobileNav(path = '/dashboard') {
  fakeServer({ 'GET /api/admin/alerts': () => ({ status: 200, body: [] }) });
  const user = userEvent.setup();
  render(
    <Provider store={makeStore()}>
      <MemoryRouter initialEntries={[path]}>
        <AdminNav mobileDisclosure />
        <main id="main-content" tabIndex={-1}>
          <Routes>
            <Route path="/dashboard" element={<h1>Dashboard test page</h1>} />
            <Route path="/admin/attendance" element={<h1>Attendance test page</h1>} />
          </Routes>
        </main>
        <button type="button">Outside navigation</button>
      </MemoryRouter>
    </Provider>,
  );
  return user;
}

describe('AdminNav alert count', () => {
  it.each([0, 9, 99, 999, 1000, 10000])('keeps the exact accessible count for %i open alerts', async (count) => {
    let respond!: (answer: FakeResponse) => void;
    fakeServer({ 'GET /api/admin/alerts': () => new Promise((resolve) => { respond = resolve; }) });
    render(<Provider store={makeStore()}><MemoryRouter><AdminNav /></MemoryRouter></Provider>);
    const link = screen.getByRole('link', { name: 'รายการที่ขึ้นเตือน' });
    expect(within(link).queryByTitle(String(count))).not.toBeInTheDocument();
    // The request remains pending until the test releases it, so the unknown state is exercised too.
    await screen.findByRole('navigation', { name: 'เมนูแอดมิน' });
    // RTK Query starts the request asynchronously.
    await waitFor(() => expect(respond).toBeDefined());
    await act(async () => {
      respond({ status: 200, body: Array.from({ length: count }, (_, i) => ({ ...attendanceGpsAlert, recordId: i + 1 })) });
    });
    if (count === 0) {
      await waitFor(() => expect(screen.queryByTitle('0')).not.toBeInTheDocument());
      expect(link).toHaveAccessibleName('รายการที่ขึ้นเตือน');
    } else {
      await screen.findByRole('link', { name: `รายการที่ขึ้นเตือน (${count})` });
      expect(within(link).getByTitle(String(count))).toHaveTextContent(count > 999 ? '999+' : String(count));
    }
  });

  it('closes the mobile menu when keyboard focus leaves the navigation', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');

    screen.getByRole('link', { name: 'ประวัติการแก้ไข' }).focus();
    screen.getByRole('button', { name: 'Outside navigation' }).focus();

    await waitFor(() => expect(trigger).toHaveAttribute('aria-expanded', 'false'));
    expect(screen.getByRole('button', { name: 'Outside navigation' })).toHaveFocus();
  });

  it('returns focus to the menu button when the current page is selected', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    await user.click(screen.getByRole('link', { name: 'สถานะจุด' }));

    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveFocus();
  });

  it('moves focus to the main content after mobile navigation and supports Escape in the menu', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    await user.click(screen.getByRole('link', { name: 'การเข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'Attendance test page' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());

    await user.click(trigger);
    screen.getByRole('link', { name: 'ประวัติการแก้ไข' }).focus();
    await user.keyboard('{Escape}');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveFocus();
  });
});
