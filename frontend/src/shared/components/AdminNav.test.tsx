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
        <AdminNav mobileDisclosure userName="แอดมินทดสอบ" />
        <main id="main-content" tabIndex={-1}>
          <Routes>
            <Route path="/dashboard" element={<h1>Dashboard test page</h1>} />
            <Route path="/admin/attendance" element={<h1>Attendance test page</h1>} />
          </Routes>
        </main>
      </MemoryRouter>
    </Provider>,
  );
  return user;
}

describe('AdminNav (3-group IA + mobile slide-over Drawer)', () => {
  it.each([0, 9, 99, 999, 1000, 10000])('keeps the exact accessible count for %i open alerts', async (count) => {
    let respond!: (answer: FakeResponse) => void;
    fakeServer({ 'GET /api/admin/alerts': () => new Promise((resolve) => { respond = resolve; }) });
    render(<Provider store={makeStore()}><MemoryRouter><AdminNav /></MemoryRouter></Provider>);
    const link = screen.getByRole('link', { name: 'รายการที่ขึ้นเตือน' });
    expect(within(link).queryByTitle(String(count))).not.toBeInTheDocument();
    await screen.findByRole('navigation', { name: 'เมนูแอดมิน' });
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

  it('opens a modal slide-over drawer on mobile and closes via Escape returning focus to trigger', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    const dialog = screen.getByRole('dialog', { name: 'เมนูแอดมิน' });
    expect(within(dialog).getByText('แอดมินทดสอบ')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog', { name: 'เมนูแอดมิน' })).not.toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveFocus();
  });

  it('returns focus to the menu button when the current page is selected inside the drawer', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    const dialog = screen.getByRole('dialog', { name: 'เมนูแอดมิน' });
    await user.click(within(dialog).getByRole('link', { name: 'สถานะจุด' }));

    expect(screen.queryByRole('dialog', { name: 'เมนูแอดมิน' })).not.toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(trigger).toHaveFocus();
  });

  it('moves focus to main content after navigating to another page from the mobile drawer', async () => {
    const user = renderMobileNav();
    const trigger = screen.getByRole('button', { name: 'เมนูแอดมิน' });
    await user.click(trigger);
    const dialog = screen.getByRole('dialog', { name: 'เมนูแอดมิน' });
    await user.click(within(dialog).getByRole('link', { name: 'การเข้างาน' }));

    expect(await screen.findByRole('heading', { name: 'Attendance test page' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
  });

  it('organizes all 12 Admin links into 3 clear groups (หน้างานวันนี้, ตารางและรายงาน, ตั้งค่าระบบ)', () => {
    fakeServer({ 'GET /api/admin/alerts': () => ({ status: 200, body: [] }) });
    render(
      <Provider store={makeStore()}>
        <MemoryRouter initialEntries={['/admin/overview']}>
          <AdminNav />
        </MemoryRouter>
      </Provider>,
    );

    const liveGroup = screen.getByRole('list', { name: 'หน้างานวันนี้' });
    expect(within(liveGroup).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'ภาพรวม',
      'สถานะจุด',
      'การเข้างาน',
      'รายการที่ขึ้นเตือน',
    ]);
    expect(within(liveGroup).getByRole('link', { name: 'ภาพรวม' })).toHaveAttribute('aria-current', 'page');

    const planGroup = screen.getByRole('list', { name: 'ตารางและรายงาน' });
    expect(within(planGroup).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'ตารางงาน',
      'สรุปการตรวจ',
      'ส่งออก Excel',
    ]);

    const setupGroup = screen.getByRole('list', { name: 'ตั้งค่าระบบ' });
    expect(within(setupGroup).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'พื้นที่และป้าย',
      'ตึก',
      'พิมพ์ป้าย',
      'บัญชีผู้ใช้',
      'ประวัติการแก้ไข',
    ]);
  });
});
