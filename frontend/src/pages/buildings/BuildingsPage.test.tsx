import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { account, accounts, adminSession, managedBuildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo } from '../../test/moreInfo';

const [a, b, c] = managedBuildings;

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    ...routes,
  });
}

const sent = (server: ReturnType<typeof serve>, method: string, path: string) =>
  server.calls.filter((call) => call.method === method && call.path === path);

/** The cells after the code, in a row found by its code. */
function cellsOf(code: string): (string | null)[] {
  const row = screen.getByRole('row', { name: new RegExp(`^${code} `) });
  return within(row).getAllByRole('cell').map((cell) => cell.textContent);
}

async function openEditor(user: Awaited<ReturnType<typeof renderApp>>['user'], building: ManagedBuilding) {
  await user.click(await screen.findByRole('button', { name: `แก้ไข ตึก ${building.code}` }));
  return screen.getByRole('dialog', { name: `แก้ไขตึก · ${building.code} ${building.name}` });
}

describe('BuildingsPage (facility-0090)', () => {

  it('groups actual active Supervisor names by shift/building and excludes other roles and inactive accounts', async () => {
    serve({ 'GET /api/admin/accounts': () => ({ status: 200, body: [
      ...accounts,
      account({ id: 40, role: 'Supervisor', buildingId: 1, shift: 'Night', displayName: 'หัวหน้าดึกตัวอย่าง' }),
      account({ id: 41, role: 'Supervisor', buildingId: 2, shift: 'Day', displayName: 'หัวหน้าตึกข้างเคียง' }),
      account({ id: 42, role: 'Supervisor', buildingId: 1, isActive: false, displayName: 'หัวหน้าปิดบัญชี' }),
      account({ id: 43, role: 'Cleaner', buildingId: 1, displayName: 'แม่บ้านไม่นับเป็นหัวหน้า' }),
    ] }) });
    await renderApp('/admin/buildings');
    const row = await screen.findByRole('row', { name: /^A / });
    expect(await within(row).findByText('เช้า · หัวหน้าทดสอบ')).toBeInTheDocument();
    expect(within(row).getByText('ดึก · หัวหน้าดึกตัวอย่าง')).toBeInTheDocument();
    expect(within(row).queryByText(/หัวหน้าตึกข้างเคียง|หัวหน้าปิดบัญชี|แม่บ้านไม่นับเป็นหัวหน้า/)).not.toBeInTheDocument();
    expect(within(screen.getByRole('row', { name: /^B / })).getByText('เช้า · หัวหน้าตึกข้างเคียง')).toBeInTheDocument();
  });

  it('distinguishes a pending names request from an empty assignment', async () => {
    let resolve: () => void = () => undefined;
    const pending = new Promise<void>((done) => { resolve = done; });
    serve({ 'GET /api/admin/accounts': async () => {
      await pending;
      return { status: 200, body: [] };
    } });
    await renderApp('/admin/buildings');
    const row = await screen.findByRole('row', { name: /^A / });
    expect(within(row).getByText('กำลังโหลดหัวหน้า...')).toBeInTheDocument();
    expect(within(row).queryByText('ยังไม่มีหัวหน้า')).not.toBeInTheDocument();
    resolve();
    expect(await within(row).findByText('ยังไม่มีหัวหน้า')).toBeInTheDocument();
  });

  it('shows unavailable names and supports retry without preventing building management', async () => {
    let failed = true;
    serve({ 'GET /api/admin/accounts': () => failed
      ? ({ status: 500, body: { message: 'โหลดไม่ได้' } })
      : ({ status: 200, body: accounts }) });
    const { user } = await renderApp('/admin/buildings');
    expect(await screen.findByText('โหลดรายชื่อหัวหน้าไม่ได้')).toBeInTheDocument();
    const row = screen.getByRole('row', { name: /^A / });
    expect(within(row).getByText('โหลดรายชื่อไม่ได้')).toBeInTheDocument();
    expect(within(row).queryByText('ยังไม่มีหัวหน้า')).not.toBeInTheDocument();
    failed = false;
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
    expect(await within(row).findByText('เช้า · หัวหน้าทดสอบ')).toBeInTheDocument();
    await openEditor(user, a);
    expect(screen.getByRole('button', { name: 'ปิดใช้งานตึกนี้' })).toBeDisabled();
  });

  it('lists every building, off ones too, with its open Areas and its status in words', async () => {
    serve();
    await renderApp('/admin/buildings');

    expect(await screen.findByRole('heading', { level: 1, name: 'ตึก' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 2, name: 'ตึกทั้งหมด (3)' })).toBeInTheDocument();
    await waitFor(() => expect(cellsOf('A')).toEqual(['ตึกทดลอง', '2', 'เช้า · หัวหน้าทดสอบ', 'ใช้งาน', 'แก้ไข']));
    expect(cellsOf('B')).toEqual(['ตึกข้างเคียง', '0', 'ยังไม่มีหัวหน้า', 'ใช้งาน', 'แก้ไข']);
    expect(cellsOf('C')).toEqual(['ตึกเก่า', '0', 'ยังไม่มีหัวหน้า', 'ปิดใช้งาน', 'แก้ไข']);
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getByRole('link', { name: 'ตึก' })).toHaveAttribute('aria-current', 'page');
  });

  it('adds a building in the Drawer; the save closes it and floats its answer', async () => {
    const server = serve({
      'POST /api/admin/buildings': () => ({
        status: 201,
        body: { ...b, id: 4, code: 'B2', name: 'ตึกผลิต' },
      }),
    });
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: 'เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    expect(within(drawer).getByText('ตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2 · แก้ทีหลังไม่ได้')).toBeInTheDocument();
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), ' b2 ');
    await user.type(within(drawer).getByRole('textbox', { name: 'ชื่อตึก' }), 'ตึกผลิต');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('เพิ่มตึก B2 ตึกผลิต แล้ว')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'เพิ่มตึก' })).not.toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings').map((call) => call.body)).toEqual([{ code: 'b2', name: 'ตึกผลิต' }]);
  });

  it('checks the code and the name before sending', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: 'เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));
    expect(within(drawer).getByText('ใส่รหัสตึกก่อน')).toBeInTheDocument();
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), 'B2');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(within(drawer).getByText('ใส่ชื่อตึกก่อน')).toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings')).toEqual([]);
  });

  it("shows the server's answer when the code is taken, and keeps the Drawer open", async () => {
    serve({ 'POST /api/admin/buildings': () => ({ status: 409, body: { message: 'รหัสตึกนี้มีแล้ว' } }) });
    const { user } = await renderApp('/admin/buildings');

    await user.click(await screen.findByRole('button', { name: 'เพิ่มตึก' }));
    const drawer = screen.getByRole('dialog', { name: 'เพิ่มตึก' });
    await user.type(within(drawer).getByRole('textbox', { name: 'รหัสตึก' }), 'C');
    await user.type(within(drawer).getByRole('textbox', { name: 'ชื่อตึก' }), 'ตึกใหม่');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await within(drawer).findByText('รหัสตึกนี้มีแล้ว')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'เพิ่มตึก' })).toBeInTheDocument();
  });

  it('renames a building; its code is shown, not offered', async () => {
    const server = serve({ 'PUT /api/admin/buildings/1': () => ({ status: 200, body: { ...a, name: 'อาคารผลิต 1' } }) });
    const { user } = await renderApp('/admin/buildings');

    const drawer = await openEditor(user, a);
    expect(within(drawer).getByText('A')).toBeInTheDocument();
    expect(within(drawer).getByText('แก้ไม่ได้')).toBeInTheDocument();
    expect(within(drawer).queryByRole('textbox', { name: 'รหัสตึก' })).not.toBeInTheDocument();
    const name = within(drawer).getByRole('textbox', { name: 'ชื่อตึก' });
    expect(name).toHaveValue('ตึกทดลอง');
    await user.clear(name);
    await user.type(name, 'อาคารผลิต 1');
    await user.click(within(drawer).getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByText('บันทึกตึก A อาคารผลิต 1 แล้ว')).toBeInTheDocument();
    expect(sent(server, 'PUT', '/api/admin/buildings/1').map((call) => call.body)).toEqual([{ name: 'อาคารผลิต 1' }]);
  });

  it('a building with open Areas or Supervisors cannot be turned off, and says what is left', async () => {
    serve();
    const { user } = await renderApp('/admin/buildings');

    const drawer = await openEditor(user, a);

    expect(
      within(drawer).getByText(
        'ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน ปิดพื้นที่ และย้ายหรือปิดบัญชีหัวหน้าก่อน จึงจะปิดตึกได้',
      ),
    ).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'ปิดใช้งานตึกนี้' })).toBeDisabled();
  });

  it('turns an empty building off, and an off one back on', async () => {
    const server = serve({
      'POST /api/admin/buildings/2/deactivate': () => ({ status: 200, body: { ...b, isActive: false } }),
      'POST /api/admin/buildings/3/activate': () => ({ status: 200, body: { ...c, isActive: true } }),
    });
    const { user } = await renderApp('/admin/buildings');

    await user.click(within(await openEditor(user, b)).getByRole('button', { name: 'ปิดใช้งานตึกนี้' }));
    expect(await screen.findByText('ปิดใช้งานตึก B แล้ว')).toBeInTheDocument();

    const offDrawer = await openEditor(user, c);
    expect(within(offDrawer).getByText('ตึกนี้ปิดใช้งานอยู่ เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้')).toBeInTheDocument();
    await user.click(within(offDrawer).getByRole('button', { name: 'เปิดใช้งานตึกนี้' }));

    expect(await screen.findByText('เปิดใช้งานตึก C แล้ว')).toBeInTheDocument();
    expect(sent(server, 'POST', '/api/admin/buildings/2/deactivate')).toHaveLength(1);
    expect(sent(server, 'POST', '/api/admin/buildings/3/activate')).toHaveLength(1);
  });

  it("shows the server's refusal to turn a building off, and keeps the Drawer open", async () => {
    const message = 'ปิดตึก B ไม่ได้ ยังมีพื้นที่ที่เปิดอยู่ 1 แห่ง ปิดหรือย้ายออกก่อน';
    serve({ 'POST /api/admin/buildings/2/deactivate': () => ({ status: 409, body: { message } }) });
    const { user } = await renderApp('/admin/buildings');

    const drawer = await openEditor(user, b);
    await user.click(within(drawer).getByRole('button', { name: 'ปิดใช้งานตึกนี้' }));

    expect(await within(drawer).findByText(message)).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: `แก้ไขตึก · ${b.code} ${b.name}` })).toBeInTheDocument();
    expect(screen.queryByText('ปิดใช้งานตึก B แล้ว')).not.toBeInTheDocument();
  });

  it('says so when there are no buildings yet', async () => {
    serve({ 'GET /api/admin/buildings/all': () => ({ status: 200, body: [] }) });
    await renderApp('/admin/buildings');

    expect(await screen.findByText('ยังไม่มีตึก')).toBeInTheDocument();
    expect(screen.getByText('เริ่มจากปุ่ม "เพิ่มตึก" ด้านบน')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'ตึกทั้งหมด (0)' })).toBeInTheDocument();
  });

  it("shows the server's answer when the list cannot be loaded", async () => {
    serve({ 'GET /api/admin/buildings/all': () => ({ status: 500, body: { message: 'โหลดรายการตึกไม่ได้' } }) });
    await renderApp('/admin/buildings');

    expect(await screen.findByText('โหลดรายการตึกไม่ได้')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('says it is loading until the first answer comes', async () => {
    let answer: () => void = () => undefined;
    const answered = new Promise<void>((resolve) => {
      answer = resolve;
    });
    serve({
      'GET /api/admin/buildings/all': async () => {
        await answered;
        return { status: 200, body: managedBuildings };
      },
    });
    await renderApp('/admin/buildings');

    expect(await screen.findByText('กำลังโหลดตึก...')).toBeInTheDocument();
    answer();
    expect(await screen.findByRole('row', { name: /^A / })).toBeInTheDocument();
    expect(screen.queryByText('กำลังโหลดตึก...')).not.toBeInTheDocument();
  });

  it('the page note and the save note wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    const { user } = await renderApp('/admin/buildings');

    await screen.findByRole('heading', { level: 2, name: 'ตึกทั้งหมด (3)' });
    expectBehindMoreInfo(/^ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่/, 'เกี่ยวกับหน้านี้');

    await openEditor(user, a);
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });

});
