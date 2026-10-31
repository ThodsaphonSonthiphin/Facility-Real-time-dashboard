import { fireEvent, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { account, accounts, adminSession, areaSummary, buildings, managedBuildings, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    ...routes,
  });
}

/** A row by its name cell; its data cells are login name, place, active, actions. */
const rowOf = (name: string) => screen.getByRole('rowheader', { name }).closest('tr') as HTMLElement;
const cellsOf = (name: string) =>
  within(rowOf(name))
    .getAllByRole('cell')
    .slice(0, 3)
    .map((cell) => cell.textContent);
const names = () => screen.getAllByRole('rowheader').map((header) => header.textContent);
const created = (server: ReturnType<typeof serve>) =>
  server.calls.find((call) => call.method === 'POST' && call.path === '/api/admin/accounts')?.body;

/** The header's "สร้างบัญชี" opens the form in the right-hand Drawer (owner, 2026-10-30). */
async function openCreate(user: Awaited<ReturnType<typeof renderApp>>['user']) {
  await user.click(await screen.findByRole('button', { name: 'สร้างบัญชี' }));
  return screen.getByRole('form', { name: 'สร้างบัญชี' });
}

describe('AccountsPage (D6, facility-0020, 0056)', () => {
  it('starts at counted Cleaner tabs, with truthful places and no phone column', async () => {
    serve();
    const { user } = await renderApp('/admin/accounts');
    const table = await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
    const tabs = screen.getByRole('tablist', { name: 'บทบาทบัญชี' });
    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['แม่บ้าน 4', 'หัวหน้า 1', 'แอดมิน 2']);
    expect(within(tabs).getByRole('tab', { name: 'แม่บ้าน 4' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tabpanel', { name: 'แม่บ้าน 4' })).toContainElement(table);
    expect(names()).toEqual(['แม่บ้านทดสอบ', 'แม่บ้านกะดึก', 'แม่บ้านว่าง', 'แม่บ้านที่ลาออก']);
    expect(cellsOf('แม่บ้านทดสอบ')).toEqual(['650001', 'AR01 · กะเช้า', 'ใช้งานอยู่']);
    expect(cellsOf('แม่บ้านว่าง')).toEqual(['650003', 'ยังไม่มีพื้นที่ · กะเช้า', 'ใช้งานอยู่']);
    expect(cellsOf('แม่บ้านที่ลาออก')).toEqual(['650006', 'ยังไม่มีพื้นที่ · กะเช้า', 'ปิดใช้งาน']);
    expect(within(table).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual([
      'ชื่อ', 'รหัสพนักงาน', 'พื้นที่ · กะ', 'สถานะ', 'การกระทำ',
    ]);
    expect(screen.getByText('แสดง 1–4 จาก 4 บัญชี')).toBeInTheDocument();
    expect(within(table).queryByRole('columnheader', { name: /เบอร์โทร/ })).not.toBeInTheDocument();
    expect(within(rowOf('แม่บ้านทดสอบ')).getByRole('button', { name: 'แก้เบอร์โทร แม่บ้านทดสอบ' })).toBeInTheDocument();
    expect(within(rowOf('แม่บ้านทดสอบ')).queryByRole('button', { name: /รีเซ็ตรหัสผ่าน/ })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'หัวหน้า 1' }));
    expect(await screen.findByText('ตึก A · ตึกทดลอง · กะเช้า')).toBeInTheDocument();
    expect(names()).toEqual(['หัวหน้าทดสอบ']);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['660001', 'ตึก A · ตึกทดลอง · กะเช้า', 'ใช้งานอยู่']);
    expect(screen.getByRole('columnheader', { name: 'รหัสพนักงาน' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'ตึก · กะ' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'แก้เบอร์โทร หัวหน้าทดสอบ' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: /เบอร์โทร/ })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'แอดมิน 2' }));
    expect(names()).toEqual(['ผู้ดูแลทดสอบ', 'ผู้ดูแลคนที่สอง']);
    expect(cellsOf('ผู้ดูแลคนที่สอง')).toEqual(['admin2', '—', 'ใช้งานอยู่']);
    expect(screen.getByRole('button', { name: 'รีเซ็ตรหัสผ่าน ผู้ดูแลคนที่สอง' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'แก้เบอร์โทร ผู้ดูแลคนที่สอง' })).not.toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'ชื่อผู้ใช้' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'รหัสพนักงาน' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: /เบอร์โทร/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'บทบาท' })).not.toBeInTheDocument();
  });

  it('keeps role, active state and search independent, with full role counts and true shown counts', async () => {
    serve();
    const { user } = await renderApp('/admin/accounts');
    await screen.findByRole('table', { name: 'บัญชีผู้ใช้' });
    await user.selectOptions(screen.getByLabelText('สถานะบัญชี'), 'inactive');
    expect(names()).toEqual(['แม่บ้านที่ลาออก']);
    expect(screen.getByText('แสดง 1–1 จาก 4 บัญชี')).toBeInTheDocument();
    await user.type(screen.getByLabelText('ค้นหา'), '650006');
    expect(names()).toEqual(['แม่บ้านที่ลาออก']);
    await user.click(screen.getByRole('tab', { name: 'แอดมิน 2' }));
    expect(screen.getByLabelText('สถานะบัญชี')).toHaveValue('inactive');
    expect(screen.getByLabelText('ค้นหา')).toHaveValue('650006');
    expect(screen.getByText('ไม่พบบัญชีที่ตรงกับที่ค้นหา')).toBeInTheDocument();
    expect(screen.getByText('แสดง 0 จาก 2 บัญชี')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('สถานะบัญชี'), 'active');
    await user.clear(screen.getByLabelText('ค้นหา'));
    await user.type(screen.getByLabelText('ค้นหา'), 'ADMIN2');
    expect(names()).toEqual(['ผู้ดูแลคนที่สอง']);
    expect(screen.getByRole('tab', { name: 'แม่บ้าน 4' })).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'แม่บ้าน 4' }));
    expect(screen.getByText('ไม่พบบัญชีที่ตรงกับที่ค้นหา')).toBeInTheDocument();
    await user.clear(screen.getByLabelText('ค้นหา'));
    expect(names()).toEqual(['แม่บ้านทดสอบ', 'แม่บ้านกะดึก', 'แม่บ้านว่าง']);
    await user.type(screen.getByLabelText('ค้นหา'), 'แม่บ้านว่าง');
    expect(names()).toEqual(['แม่บ้านว่าง']);
    await user.selectOptions(screen.getByLabelText('สถานะบัญชี'), 'all');
    expect(names()).toEqual(['แม่บ้านว่าง']);
  });

  it('switches tabs with Arrow keys, Home and End, linking the active panel and keeping focus on the chosen tab', async () => {
    serve();
    const { user } = await renderApp('/admin/accounts');
    const cleaner = await screen.findByRole('tab', { name: 'แม่บ้าน 4' });
    await user.click(cleaner);
    await user.keyboard('{ArrowRight}');
    const supervisor = screen.getByRole('tab', { name: 'หัวหน้า 1' });
    expect(supervisor).toHaveFocus();
    expect(supervisor).toHaveAttribute('aria-selected', 'true');
    expect(supervisor).toHaveAttribute('aria-controls', screen.getByRole('tabpanel', { name: 'หัวหน้า 1' }).id);
    expect(names()).toEqual(['หัวหน้าทดสอบ']);
    await user.keyboard('{End}');
    expect(screen.getByRole('tab', { name: 'แอดมิน 2' })).toHaveFocus();
    expect(names()).toEqual(['ผู้ดูแลทดสอบ', 'ผู้ดูแลคนที่สอง']);
    await user.keyboard('{ArrowRight}');
    expect(cleaner).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'แอดมิน 2' })).toHaveFocus();
    await user.keyboard('{Home}');
    expect(cleaner).toHaveFocus();
    expect(cleaner).toHaveAttribute('aria-selected', 'true');
    expect(screen.getAllByRole('tab').filter((tab) => tab.tabIndex === 0)).toEqual([cleaner]);
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

    const form = await openCreate(user);
    expect(within(form).getByRole('radio', { name: 'แม่บ้าน' })).toBeChecked();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '081-000-0009');

    const areaSelect = await within(form).findByLabelText('พื้นที่ประจำ');
    await within(areaSelect).findByRole('option', { name: /^AR02/ });
    expect(within(areaSelect).getAllByRole('option').map((option) => option.textContent)).toEqual([
      '— ยังไม่กำหนดพื้นที่ —',
      'AR01 Area ทดลอง · ตึก A · ตึกทดลอง · 2 กะ · กะเช้ามีคนแล้ว (แม่บ้านทดสอบ) · กะดึกมีคนแล้ว (แม่บ้านกะดึก)',
      'AR02 Office ทดลอง · ตึก A · ตึกทดลอง · กะเช้าอย่างเดียว',
    ]);
    await user.selectOptions(areaSelect, '1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(มีคนประจำแล้ว (แม่บ้านทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(areaSelect, '2');
    expect(within(form).getByRole('radio', { name: /^กะดึก/ })).toBeDisabled();
    expect(within(form).getByText('(พื้นที่นี้ทำเฉพาะกะเช้า)')).toBeInTheDocument();
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

    const form = await openCreate(user);
    await user.click(within(form).getByRole('radio', { name: 'หัวหน้า' }));
    expect(within(form).queryByLabelText('พื้นที่ประจำ')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'หัวหน้าใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '660002');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0820000002');
    await within(form).findByRole('option', { name: 'B · ตึกข้างเคียง' });
    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'A · ตึกทดลอง');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).toBeDisabled();
    expect(within(form).getByText('(มีหัวหน้าแล้ว (หัวหน้าทดสอบ))')).toBeInTheDocument();

    await user.selectOptions(within(form).getByLabelText('ตึกที่ประจำ'), 'B · ตึกข้างเคียง');
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

    const form = await openCreate(user);
    await user.click(within(form).getByRole('radio', { name: 'แอดมิน' }));
    expect(within(form).queryByLabelText('เบอร์โทรศัพท์')).not.toBeInTheDocument();
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'ผู้ดูแลคนที่สาม');
    await user.type(within(form).getByLabelText('ชื่อผู้ใช้'), 'admin3');
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-1234');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));
    expect(within(form).getByRole('alert')).toHaveTextContent('รหัสผ่านชั่วคราวต้องยาว 15–100 ตัวอักษร');

    await user.clear(within(form).getByLabelText('รหัสผ่านชั่วคราว'));
    await user.type(within(form).getByLabelText('รหัสผ่านชั่วคราว'), 'temporary-pass-123');
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี ผู้ดูแลคนที่สาม แล้ว');
    expect(created(server)).toEqual({ role: 'Admin', displayName: 'ผู้ดูแลคนที่สาม', username: 'admin3', password: 'temporary-pass-123' });
    expect(within(form).getByLabelText('รหัสผ่านชั่วคราว')).toHaveValue('');
  });

  it('says what is missing first, and shows the server’s refusal with the form kept', async () => {
    const server = serve({
      'POST /api/admin/accounts': () => ({ status: 409, body: { message: 'รหัสพนักงานนี้มีแล้ว' } }),
    });
    const { user } = await renderApp('/admin/accounts');
    const form = await openCreate(user);
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
    const form = await openCreate(user);
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    const sending = within(form).getByRole('button', { name: 'กำลังสร้าง...' });
    expect(sending).toBeDisabled();
    await user.click(sending);
    expect(server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/accounts')).toHaveLength(1);

    fireEvent.keyDown(screen.getByRole('dialog', { name: 'สร้างบัญชี' }), { key: 'Escape' });
    expect(form).toBeInTheDocument();
    expect(within(form).getByLabelText('รหัสพนักงาน')).toHaveValue('650010');
    answer();
    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี แม่บ้านใหม่ แล้ว');
  });

  it('drops a chosen shift that the newly chosen Area does not allow', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/accounts');
    const form = await openCreate(user);
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');

    // Day is ticked with no Area; AR01's Day is held, so choosing AR01 must drop it
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.selectOptions(await within(form).findByLabelText('พื้นที่ประจำ'), '1');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).not.toBeChecked();
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(within(form).getByRole('alert')).toHaveTextContent('เลือกกะก่อน');
    expect(created(server)).toBeUndefined();
  });

  it('drops a chosen shift that the newly chosen building does not allow', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/accounts');
    const form = await openCreate(user);
    await user.click(within(form).getByRole('radio', { name: 'หัวหน้า' }));
    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'หัวหน้าใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '660002');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0820000002');

    // Day is ticked with no building; ตึกทดลอง's Day is held by หัวหน้าทดสอบ, so choosing it must drop Day
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.selectOptions(await within(form).findByLabelText('ตึกที่ประจำ'), 'A · ตึกทดลอง');
    expect(within(form).getByRole('radio', { name: /^กะเช้า/ })).not.toBeChecked();
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(within(form).getByRole('alert')).toHaveTextContent('เลือกกะก่อน');
    expect(created(server)).toBeUndefined();
  });

  it('shows the new account in the table after it is created', async () => {
    let afterCreate = false;
    const newcomer = account({ id: 40, loginName: '650010', displayName: 'แม่บ้านใหม่', areaId: undefined, areaCode: undefined });
    serve({
      'GET /api/admin/accounts': () => ({ status: 200, body: afterCreate ? [...accounts, newcomer] : accounts }),
      'POST /api/admin/accounts': () => {
        afterCreate = true;
        return { status: 201, body: newcomer };
      },
    });
    const { user } = await renderApp('/admin/accounts');
    const form = await openCreate(user);
    expect(screen.queryByRole('rowheader', { name: 'แม่บ้านใหม่' })).not.toBeInTheDocument();

    await user.type(within(form).getByLabelText('ชื่อที่แสดง'), 'แม่บ้านใหม่');
    await user.type(within(form).getByLabelText('รหัสพนักงาน'), '650010');
    await user.type(within(form).getByLabelText('เบอร์โทรศัพท์'), '0810000009');
    await user.click(within(form).getByRole('radio', { name: /^กะเช้า/ }));
    await user.click(within(form).getByRole('button', { name: 'สร้างบัญชี' }));

    expect(await within(form).findByRole('status')).toHaveTextContent('สร้างบัญชี แม่บ้านใหม่ แล้ว');
    expect(await screen.findByRole('rowheader', { name: 'แม่บ้านใหม่' })).toBeInTheDocument();
  });
});
