import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { account, accounts, adminSession, areaSummary, buildings, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    ...routes,
  });
}

/** Opens the page and presses a row's action button, e.g. "แก้ไข แม่บ้านว่าง". */
async function openAndPress(action: string) {
  const app = await renderApp('/admin/accounts');
  await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
  await app.user.click(screen.getByRole('button', { name: action }));
  return app;
}

const sent = (server: ReturnType<typeof serve>, method: string, path: string) =>
  server.calls.find((call) => call.method === method && call.path === path)?.body;

describe('account actions (D6, facility-0020, 0054)', () => {
  it('edits a Cleaner with no Area: name, Area and shift', async () => {
    const server = serve({
      'PUT /api/admin/accounts/23': () => ({
        status: 200,
        body: account({ id: 23, loginName: '650003', displayName: 'แม่บ้านว่าง ใหม่', areaId: 2, areaCode: 'AR02' }),
      }),
    });
    const { user } = await openAndPress('แก้ไข แม่บ้านว่าง');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านว่าง' });
    expect(within(form).getByText('แม่บ้าน · 650003 (บทบาทและรหัสแก้ไม่ได้)')).toBeInTheDocument();
    await user.clear(within(form).getByLabelText('ชื่อที่แสดง'));
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านว่าง ใหม่');
    await user.selectOptions(within(form).getByLabelText('พื้นที่ประจำ'), '2');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeChecked();
    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกบัญชี แม่บ้านว่าง ใหม่ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/23')).toEqual({
      displayName: 'แม่บ้านว่าง ใหม่',
      shift: 'Day',
      areaId: 2,
    });
    // The panel goes back to "เพิ่มบัญชี"
    expect(screen.getByRole('form', { name: 'เพิ่มบัญชี' })).toBeInTheDocument();
  });

  it('editing a Cleaner keeps their Area: their own slot is not "held"', async () => {
    const server = serve({ 'PUT /api/admin/accounts/21': () => ({ status: 200, body: account() }) });
    const { user } = await openAndPress('แก้ไข แม่บ้านทดสอบ');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านทดสอบ' });
    expect(within(form).getByLabelText('พื้นที่ประจำ')).toHaveValue('1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeEnabled();
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกบัญชี แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/21')).toEqual({ displayName: 'แม่บ้านทดสอบ', shift: 'Day', areaId: 1 });
  });

  it('a Supervisor’s edit sends building and shift; an Admin’s only the name', async () => {
    const server = serve({
      'PUT /api/admin/accounts/31': () => ({ status: 200, body: accounts[2] }),
      'PUT /api/admin/accounts/8': () => ({ status: 200, body: accounts[1] }),
    });
    const { user } = await openAndPress('แก้ไข หัวหน้าทดสอบ');

    const supervisor = screen.getByRole('form', { name: 'แก้ไขบัญชี · หัวหน้าทดสอบ' });
    expect(within(supervisor).getByLabelText('ตึกที่ประจำ')).toHaveValue('1');
    await user.click(within(supervisor).getByRole('button', { name: 'บันทึก' }));
    expect(await screen.findByText('บันทึกบัญชี หัวหน้าทดสอบ แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/31')).toEqual({ displayName: 'หัวหน้าทดสอบ', buildingId: 1, shift: 'Day' });

    await user.click(screen.getByRole('button', { name: 'แก้ไข ผู้ดูแลคนที่สอง' }));
    const admin = screen.getByRole('form', { name: 'แก้ไขบัญชี · ผู้ดูแลคนที่สอง' });
    expect(within(admin).queryByRole('group', { name: 'กะ' })).not.toBeInTheDocument();
    await user.click(within(admin).getByRole('button', { name: 'บันทึก' }));
    expect(await screen.findByText('บันทึกบัญชี ผู้ดูแลคนที่สอง แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/8')).toEqual({ displayName: 'ผู้ดูแลคนที่สอง' });
  });

  it('a deactivated Cleaner gets no Area until activated (plan 5 decision 4)', async () => {
    serve();
    await openAndPress('แก้ไข แม่บ้านที่ลาออก');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านที่ลาออก' });
    expect(within(form).getByLabelText('พื้นที่ประจำ')).toBeDisabled();
    expect(within(form).getByText('เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่')).toBeInTheDocument();
  });

  it('changes a phone: checked first, sent once, and never kept on the page (facility-0054)', async () => {
    const server = serve({ 'PUT /api/admin/accounts/21/phone': () => ({ status: 204 }) });
    const { user } = await openAndPress('แก้เบอร์โทร แม่บ้านทดสอบ');
    expect(screen.queryByRole('button', { name: 'แก้เบอร์โทร ผู้ดูแลคนที่สอง' })).not.toBeInTheDocument();

    const form = screen.getByRole('form', { name: 'แก้เบอร์โทร · แม่บ้านทดสอบ' });
    // facility-0081: who is being edited, and that the old number is not needed (it is stored as a hash)
    expect(within(form).getByText('650001')).toBeInTheDocument();
    expect(within(form).getByText('AR01 · กะเช้า')).toBeInTheDocument();
    expect(within(form).getByText('แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(within(form).getByText('แม่บ้าน')).toBeInTheDocument();
    expect(within(form).getByText(/^ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก/)).toBeInTheDocument();
    await user.type(within(form).getByLabelText('เบอร์โทรที่คนนี้ใช้ตอนนี้'), '123');
    await user.click(within(form).getByRole('button', { name: 'บันทึกเบอร์ใหม่' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0');

    await user.clear(within(form).getByLabelText('เบอร์โทรที่คนนี้ใช้ตอนนี้'));
    await user.type(within(form).getByLabelText('เบอร์โทรที่คนนี้ใช้ตอนนี้'), '0810000099');
    await user.click(within(form).getByRole('button', { name: 'บันทึกเบอร์ใหม่' }));

    expect(
      await screen.findByText('เปลี่ยนเบอร์โทรของ แม่บ้านทดสอบ แล้ว ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่'),
    ).toBeInTheDocument();
    const puts = server.calls.filter((call) => call.method === 'PUT' && call.path === '/api/admin/accounts/21/phone');
    expect(puts).toHaveLength(1);
    expect(puts[0].body).toEqual({ phone: '0810000099' });
    expect(screen.queryByDisplayValue('0810000099')).not.toBeInTheDocument();
  });

  it('resets an Admin’s password, and says when it was the Admin’s own', async () => {
    const server = serve({
      'PUT /api/admin/accounts/8/password': () => ({ status: 204 }),
      'PUT /api/admin/accounts/7/password': () => ({ status: 204 }),
    });
    const { user } = await openAndPress('รีเซ็ตรหัสผ่าน ผู้ดูแลคนที่สอง');
    expect(screen.queryByRole('button', { name: 'รีเซ็ตรหัสผ่าน แม่บ้านทดสอบ' })).not.toBeInTheDocument();

    const form = screen.getByRole('form', { name: 'รีเซ็ตรหัสผ่าน · ผู้ดูแลคนที่สอง' });
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราวใหม่'), 'temporary-pass-456');
    await user.click(within(form).getByRole('button', { name: 'บันทึกรหัสผ่านใหม่' }));
    expect(
      await screen.findByText(
        'รีเซ็ตรหัสผ่านของ ผู้ดูแลคนที่สอง แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่',
      ),
    ).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/accounts/8/password')).toEqual({ password: 'temporary-pass-456' });

    await user.click(screen.getByRole('button', { name: 'รีเซ็ตรหัสผ่าน ผู้ดูแลทดสอบ' }));
    const own = screen.getByRole('form', { name: 'รีเซ็ตรหัสผ่าน · ผู้ดูแลทดสอบ' });
    await user.type(within(own).getByLabelText('รหัสผ่านชั่วคราวใหม่'), 'temporary-pass-789');
    await user.click(within(own).getByRole('button', { name: 'บันทึกรหัสผ่านใหม่' }));
    expect(
      await screen.findByText(
        'รีเซ็ตรหัสผ่านของ ผู้ดูแลทดสอบ แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ รวมเครื่องนี้ด้วย',
      ),
    ).toBeInTheDocument();
  });

  it('deactivating asks first and says the Cleaner’s Area slot goes; the Admin cannot deactivate themselves', async () => {
    const server = serve({
      'POST /api/admin/accounts/21/deactivate': () => ({
        status: 200,
        body: account({ isActive: false, areaId: undefined, areaCode: undefined }),
      }),
    });
    const { user } = await openAndPress('ปิดใช้งาน แม่บ้านทดสอบ');
    expect(screen.queryByRole('button', { name: 'ปิดใช้งาน ผู้ดูแลทดสอบ' })).not.toBeInTheDocument();

    const ask = screen.getByRole('alertdialog', { name: 'ปิดใช้งานบัญชี แม่บ้านทดสอบ?' });
    expect(ask).toHaveTextContent('แม่บ้านทดสอบ เข้าสู่ระบบไม่ได้ และทุกเครื่องหลุดภายใน 5 นาที ประวัติยังอยู่ เปิดใช้งานกลับได้');
    expect(ask).toHaveTextContent('AR01 กะเช้า จะว่าง เปิดใช้งานกลับแล้วต้องเลือกพื้นที่ใหม่');
    await user.click(within(ask).getByRole('button', { name: 'ยกเลิก' }));
    expect(server.calls.some((call) => call.path.endsWith('/deactivate'))).toBe(false);
    expect(screen.getByRole('form', { name: 'เพิ่มบัญชี' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ปิดใช้งาน แม่บ้านทดสอบ' }));
    await user.click(screen.getByRole('button', { name: 'ยืนยัน ปิดใช้งาน' }));
    expect(await screen.findByText('ปิดใช้งาน แม่บ้านทดสอบ แล้ว')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/admin/accounts/21/deactivate')).toHaveLength(1);
  });

  it('activating is one press, and says a Cleaner comes back with no Area', async () => {
    const server = serve({
      'POST /api/admin/accounts/26/activate': () => ({
        status: 200,
        body: account({ id: 26, loginName: '650006', displayName: 'แม่บ้านที่ลาออก', areaId: undefined, areaCode: undefined }),
      }),
    });
    await openAndPress('เปิดใช้งาน แม่บ้านที่ลาออก');

    expect(
      await screen.findByText('เปิดใช้งาน แม่บ้านที่ลาออก แล้ว ยังไม่มีพื้นที่ เลือกพื้นที่ได้ที่ "แก้ไข"'),
    ).toBeInTheDocument();
    expect(server.calls.filter((call) => call.path === '/api/admin/accounts/26/activate')).toHaveLength(1);
  });

  it('an activation the server refuses shows its message', async () => {
    serve({
      'POST /api/admin/accounts/26/activate': () => ({
        status: 409,
        body: { message: 'กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (แม่บ้านทดสอบ)' },
      }),
    });
    await openAndPress('เปิดใช้งาน แม่บ้านที่ลาออก');

    expect(await screen.findByRole('alert')).toHaveTextContent('กะเช้าของ AR01 มีแม่บ้านประจำแล้ว (แม่บ้านทดสอบ)');
  });

  it('one send at a time: while the edit is in flight its save is disabled and a second press sends nothing', async () => {
    const server = serve({
      'PUT /api/admin/accounts/23': () => new Promise<never>(() => {}),
    });
    const { user } = await openAndPress('แก้ไข แม่บ้านว่าง');

    const form = screen.getByRole('form', { name: 'แก้ไขบัญชี · แม่บ้านว่าง' });
    await user.click(within(form).getByRole('button', { name: 'บันทึก' }));

    const saving = within(form).getByRole('button', { name: 'กำลังบันทึก...' });
    expect(saving).toBeDisabled();
    await user.click(saving);
    expect(server.calls.filter((call) => call.method === 'PUT' && call.path === '/api/admin/accounts/23')).toHaveLength(1);
  });
});
