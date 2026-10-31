import { act, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const noSession = { 'POST /api/auth/refresh': () => ({ status: 401 }) };

describe('LoginPage (M0)', () => {
  it('a Cleaner logs in with employee ID and phone and lands on My Work', async () => {
    const server = fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('cleaner') });
    const { user, router } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), ' 650001 ');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '000-000-0001');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'งานของฉัน' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/my-work');
    expect(server.calls.find((c) => c.path === '/api/auth/login')?.body).toEqual({
      employeeId: '650001',
      phone: '000-000-0001',
    });
  });

  it('a Supervisor lands on My Building', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('supervisor') });
    const { user } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650002');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0000000002');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'ตึกของฉัน' })).toBeInTheDocument();
  });

  it('a wrong ID or phone shows the M0 message and stays on the page', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 401 }) });
    const { user, router } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0999999999');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง');
    expect(router.state.location.pathname).toBe('/login');
  });

  it('the login throttle (429) asks the person to wait', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 429 }) });
    const { user } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0999999999');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่');
  });

  it('shows the build commit (facility-0068)', async () => {
    fakeServer(noSession);
    await renderApp('/login');

    expect(screen.getByText(/^เวอร์ชัน /)).toBeInTheDocument();
  });

  it('after login, returns to the page that was opened before it (facility-0026)', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('cleaner') });
    const { user, router } = await renderApp('/my-work?from=sign');

    expect(screen.getByText('เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที')).toBeInTheDocument();
    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0000000001');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    await screen.findByRole('heading', { name: 'งานของฉัน' });
    expect(router.state.location.pathname + router.state.location.search).toBe('/my-work?from=sign');
  });

  it('after an on-purpose logout the login page is the plain one', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner'), 'POST /api/auth/logout': () => ({ status: 204 }) });
    const { user } = await renderApp('/my-work');

    // A Cleaner's ออกจากระบบ is at the end of My Work, shown once the work has loaded (the design file's Main)
    await user.click(await screen.findByRole('button', { name: 'ออกจากระบบ' }));

    expect(await screen.findByRole('heading', { name: 'เข้าสู่ระบบ' })).toBeInTheDocument();
    expect(screen.queryByText('เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที')).not.toBeInTheDocument();
  });
});

describe('AdminLoginPage (D0)', () => {
  it('disables the form while login is pending and shows the real throttle error', async () => {
    let finish!: (response: { status: number }) => void;
    fakeServer({ ...noSession, 'POST /api/auth/login': () => new Promise((resolve) => { finish = resolve; }) });
    const { user } = await renderApp('/login/admin');
    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), 'admin');
    await user.type(screen.getByLabelText('รหัสผ่าน'), 'example-password-for-test');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('button', { name: 'กำลังเข้าสู่ระบบ...' })).toBeDisabled();
    expect(screen.getByLabelText('ชื่อผู้ใช้')).toBeDisabled();
    expect(screen.getByLabelText('รหัสผ่าน')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'แสดง' })).toBeDisabled();

    await act(async () => { finish({ status: 429 }); });
    expect(await screen.findByRole('alert')).toHaveTextContent('กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่');
    expect(screen.getByRole('button', { name: 'เข้าสู่ระบบ' })).toBeEnabled();
    expect(screen.getByLabelText('รหัสผ่าน')).not.toHaveAttribute('aria-invalid');
  });

  it('reveals and hides the password without submitting or changing its value', async () => {
    const server = fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('admin') });
    const { user } = await renderApp('/login/admin');
    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), ' admin ');
    const password = screen.getByLabelText('รหัสผ่าน');
    await user.type(password, 'example-password-for-test');
    expect(password).toHaveAttribute('type', 'password');

    await user.click(screen.getByRole('button', { name: 'แสดง' }));
    expect(password).toHaveAttribute('type', 'text');
    expect(password).toHaveValue('example-password-for-test');
    expect(server.calls.filter((call) => call.path === '/api/auth/login')).toHaveLength(0);

    await user.click(screen.getByRole('button', { name: 'ซ่อน' }));
    expect(password).toHaveAttribute('type', 'password');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));
    await screen.findByRole('heading', { name: 'สถานะจุดทำความสะอาด' });
    expect(server.calls.find((call) => call.path === '/api/auth/login')?.body).toEqual({
      username: 'admin', password: 'example-password-for-test',
    });
  });

  it('the Admin logs in with username and password and lands on the Dashboard', async () => {
    const server = fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('admin') });
    const { user, router } = await renderApp('/login/admin');

    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), 'admin');
    await user.type(screen.getByLabelText('รหัสผ่าน'), 'not-a-real-password');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'สถานะจุดทำความสะอาด' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/dashboard');
    expect(server.calls.find((c) => c.path === '/api/auth/login')?.body).toEqual({
      username: 'admin',
      password: 'not-a-real-password',
    });
  });

  it('a wrong password shows the D0 message', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 401 }) });
    const { user } = await renderApp('/login/admin');

    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), 'admin');
    await user.type(screen.getByLabelText('รหัสผ่าน'), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
    expect(screen.getByLabelText('รหัสผ่าน')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('รหัสผ่าน')).toHaveAccessibleDescription('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  });

  it('the link between the two login pages keeps the page to return to', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('admin') });
    const { user, router } = await renderApp('/my-work');

    await user.click(screen.getByRole('link', { name: 'หน้าแอดมิน' }));

    expect(router.state.location.pathname).toBe('/login/admin');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/my-work' } });
  });
});
