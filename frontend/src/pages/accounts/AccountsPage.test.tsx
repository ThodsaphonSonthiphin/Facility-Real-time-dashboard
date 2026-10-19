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

/** A row by its name cell; the first four cells are login name, role, place, active. */
const rowOf = (name: string) => screen.getByRole('rowheader', { name }).closest('tr') as HTMLElement;
const cellsOf = (name: string) =>
  within(rowOf(name))
    .getAllByRole('cell')
    .slice(0, 4)
    .map((cell) => cell.textContent);
const names = () => screen.getAllByRole('rowheader').map((header) => header.textContent);
const created = (server: ReturnType<typeof serve>) =>
  server.calls.find((call) => call.method === 'POST' && call.path === '/api/admin/accounts')?.body;

describe('AccountsPage (D6, facility-0020, 0056)', () => {
  it('lists every account with role filters, counts and where each one works', async () => {
    serve();
    await renderApp('/admin/accounts');

    await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
    const filters = screen.getByRole('group', { name: 'กรองบัญชี' });
    expect(within(filters).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'ทั้งหมด (7)',
      'แม่บ้าน (4)',
      'หัวหน้า (1)',
      'Admin (2)',
      'ปิดใช้งาน (1)',
    ]);
    expect(within(filters).getByRole('button', { name: 'ทั้งหมด (7)' })).toHaveAttribute('aria-pressed', 'true');
    expect(cellsOf('แม่บ้านทดสอบ')).toEqual(['650001', 'แม่บ้าน', 'AR01 · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('แม่บ้านว่าง')).toEqual(['650003', 'แม่บ้าน', 'ยังไม่มี Area · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['660001', 'หัวหน้า', 'ตึก A · กะเช้า', 'ใช้งาน']);
    expect(cellsOf('ผู้ดูแลคนที่สอง')).toEqual(['admin2', 'Admin', '—', 'ใช้งาน']);
    expect(cellsOf('แม่บ้านที่ลาออก')).toEqual(['650006', 'แม่บ้าน', 'ยังไม่มี Area · กะเช้า', 'ปิดใช้งาน']);
  });

  it('filters by role or by deactivated, and searches names and login names', async () => {
    serve();
    const { user } = await renderApp('/admin/accounts');
    await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });

    await user.click(screen.getByRole('button', { name: 'แม่บ้าน (4)' }));
    expect(names()).toEqual(['แม่บ้านทดสอบ', 'แม่บ้านกะดึก', 'แม่บ้านว่าง', 'แม่บ้านที่ลาออก']);
    await user.click(screen.getByRole('button', { name: 'ปิดใช้งาน (1)' }));
    expect(names()).toEqual(['แม่บ้านที่ลาออก']);

    await user.click(screen.getByRole('button', { name: 'ทั้งหมด (7)' }));
    await user.type(screen.getByLabelText('ค้นหา'), 'ADMIN2');
    expect(names()).toEqual(['ผู้ดูแลคนที่สอง']);
    await user.clear(screen.getByLabelText('ค้นหา'));
    await user.type(screen.getByLabelText('ค้นหา'), 'ไม่มีคนนี้');
    expect(screen.getByText('ไม่พบบัญชีที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('adds a Cleaner: only the Cleaner’s fields go, and a held shift or a night in a day-only Area cannot be picked', async () => {
    const server = serve({
      'POST /api/admin/accounts': ({ body }) => ({
        status: 201,
        body: account({
          id: 40,
          loginName: '650010',
          displayName: (body as { displayName: string }).displayName,
          areaId: 2,
          areaCode: 'AR02',
        }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    expect(within(form).getByRole('radio', { name: 'แม่บ้าน' })).toBeChecked();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '081-000-0009');

    const areaSelect = await within(form).findByLabelText('Area ประจำ');
    await within(areaSelect).findByRole('option', { name: /^AR02/ });
    expect(within(areaSelect).getAllByRole('option').map((option) => option.textContent)).toEqual([
      '— ยังไม่กำหนด Area —',
      'AR01 Area ทดลอง · ตึก A · 2 กะ · กะเช้ามีคนแล้ว (แม่บ้านทดสอบ) · กะดึกมีคนแล้ว (แม่บ้านกะดึก)',
      'AR02 Office ทดลอง · ตึก A · กะเช้าอย่างเดียว',
    ]);
    await user.selectOptions(areaSelect, '1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(มีคนประจำแล้ว (แม่บ้านทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(areaSelect, '2');
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(Area นี้ทำเฉพาะกะเช้า)')).toBeInTheDocument();
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี แม่บ้านใหม่ แล้ว');
    expect(created(server)).toEqual({
      role: 'Cleaner',
      displayName: 'แม่บ้านใหม่',
      employeeId: '650010',
      phone: '0810000009',
      shift: 'Day',
      areaId: 2,
    });
    // The phone leaves the form once it is sent
    expect(within(form).getByLabelText('เบอร์โทรศัพท์')).toHaveValue('');
  });

  it('adds a Supervisor with building and shift; a building-shift that has one cannot be picked', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({
        status: 201,
        body: account({
          id: 41,
          role: 'Supervisor',
          loginName: '660002',
          displayName: 'หัวหน้าใหม่',
          areaId: undefined,
          areaCode: undefined,
          buildingId: 2,
          buildingCode: 'B',
          shift: 'Night',
        }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    await user.click(within(form).getByRole('radio', { name: 'หัวหน้า' }));
    expect(within(form).queryByLabelText('Area ประจำ')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'หัวหน้าใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '660002');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0820000002');
    await within(form).findByRole('option', { name: 'ตึกข้างเคียง' });
    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'ตึกทดลอง');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByText('(มีหัวหน้าแล้ว (หัวหน้าทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'ตึกข้างเคียง');
    await user.click(within(form).getByRole('radio', { name: /^กะดึก/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี หัวหน้าใหม่ แล้ว');
    expect(created(server)).toEqual({
      role: 'Supervisor',
      displayName: 'หัวหน้าใหม่',
      employeeId: '660002',
      phone: '0820000002',
      buildingId: 2,
      shift: 'Night',
    });
  });

  it('adds an Admin with a username and a temporary password, and no phone', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({
        status: 201,
        body: account({ id: 9, role: 'Admin', loginName: 'admin3', displayName: 'ผู้ดูแลคนที่สาม', areaId: undefined, areaCode: undefined, shift: undefined }),
      }),
    });
    const { user } = await renderApp('/admin/accounts');

    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    await user.click(within(form).getByRole('radio', { name: 'Admin' }));
    expect(within(form).queryByLabelText('เบอร์โทรศัพท์')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'ผู้ดูแลคนที่สาม');
    await user.type(within(form).getByLabelText('ชื่อผู้ใช้'), 'admin3');
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'short');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('รหัสผ่านชั่วคราวต้องยาว 8–100 ตัวอักษร');

    await user.clear(within(form).getByLabelText('รหัสผ่านชั่วคราว'));
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-123');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี ผู้ดูแลคนที่สาม แล้ว');
    expect(created(server)).toEqual({ role: 'Admin', displayName: 'ผู้ดูแลคนที่สาม', username: 'admin3', password: 'temporary-123' });
    expect(within(form).getByLabelText('รหัสผ่านชั่วคราว')).toHaveValue('');
  });

  it('says what is missing first, and shows the server’s refusal with the form kept', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({ status: 409, body: { message: 'รหัสพนักงานนี้มีแล้ว' } }),
    });
    const { user } = await renderApp('/admin/accounts');
    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    const send = () => user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('ใส่ชื่อที่แสดงก่อน');
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านซ้ำ');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('ใส่รหัสพนักงานก่อน');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650001');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('เบอร์โทรต้องเป็นเลข 10 หลัก ขึ้นต้นด้วย 0');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');
    await send();
    expect(within(form).getByRole('alert')).toHaveTextContent('เลือกกะก่อน');
    expect(created(server)).toBeUndefined();

    // No Area yet is allowed: the Admin chooses it later with "แก้ไข"
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await send();
    expect(await within(form).findByRole('alert')).toHaveTextContent('รหัสพนักงานนี้มีแล้ว');
    expect(within(form).getByLabelText('รหัสพนักงาน')).toHaveValue('650001');
  });

  it('sends nothing more while an account is being created: the button says กำลังสร้าง and is disabled', async () => {
    let answer: () => void = () => {};
    const server = serve({
      'POST /api/admin/accounts': () =>
        new Promise((resolve) => {
          answer = () => resolve({ status: 201, body: account({ id: 40, loginName: '650010', displayName: 'แม่บ้านใหม่' }) });
        }),
    });
    const { user } = await renderApp('/admin/accounts');
    const form = await screen.findByRole('form', { name: 'เพิ่มบัญชี' });
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    const sending = within(form).getByRole('button', { name: 'กำลังสร้าง...' });
    expect(sending).toBeDisabled();
    await user.click(sending);
    expect(server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/accounts')).toHaveLength(1);

    answer();
    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี แม่บ้านใหม่ แล้ว');
  });
});
