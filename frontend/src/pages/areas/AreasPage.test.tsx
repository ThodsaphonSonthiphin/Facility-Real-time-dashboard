import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminPoint,
  adminSession,
  areaDetail,
  areaSummary,
  buildings,
  cleaners,
  managedBuildings,
  officeDetail,
  officeSummary,
  sign,
} from '../../test/adminFixtures';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo, expectInView } from '../../test/moreInfo';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    'GET /api/admin/cleaners': () => ({ status: 200, body: cleaners }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail() }),
    ...routes,
  });
}

const row = (name: RegExp) => screen.getByRole('row', { name });

describe('AreasPage (D4, facility-0045)', () => {
  it('lists every Area and opens the first one with its signs', async () => {
    serve();
    const { user } = await renderApp('/admin/areas');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'พื้นที่และป้าย' })).toBeInTheDocument();
    expect(screen.getByText('2 พื้นที่ · 2 จุด · 1 ตึก')).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'เพิ่มพื้นที่' })).toHaveAttribute(
      'href',
      '/admin/areas/new',
    );
    const list = screen.getByRole('region', { name: 'พื้นที่ทั้งหมด (2)' });
    expect(within(list).getByText('ตึก A · 2 จุด')).toBeInTheDocument();
    expect(within(list).getByText('ตึก A · 0 จุด')).toBeInTheDocument();
    expect(within(list).getByRole('link', { name: /^AR01 Area ทดลอง/ })).toHaveAttribute('aria-current', 'true');

    expect(screen.getByText('ตึก A · ตึกทดลอง · 2 กะ · 2 จุด')).toBeInTheDocument();
    expect(screen.getByText('แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(screen.getAllByText('แม่บ้านกะดึก')).toHaveLength(2);
    expect(screen.getByText('AR01-IN · รัศมี 50 ม.')).toBeInTheDocument();
    expect(screen.getByText('พิกัดป้ายลงเวลา')).toBeInTheDocument();
    // within main: the Admin menu has its own พิมพ์ป้าย (#121), to the page with no Area chosen
    expect(within(screen.getByRole('main')).getByRole('link', { name: 'พิมพ์ป้าย' })).toHaveAttribute('href', '/admin/print?area=1');
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'จุดในพื้นที่ · 2' })).toBeInTheDocument();

    const men = row(/AR01-01/);
    expect(within(men).getByText('07:00–09:00 · 16:00–18:00')).toBeInTheDocument();
    expect(within(men).getByText('20:00–22:00 · 03:00–05:00')).toBeInTheDocument();
    expect(within(men).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=101');
    const women = row(/AR01-02/);
    expect(within(women).getByText('08:00–10:00')).toBeInTheDocument();
    expect(within(women).getByText('ยังไม่มีพิกัด')).toBeInTheDocument();
    expect(within(women).getByText('50 ม.')).toBeInTheDocument();

    // Check-in facts stay visible; its management controls live in the Area Drawer.
    expect(screen.queryByRole('row', { name: /AR01-IN/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'แก้ไขพื้นที่' }));
    const checkIn = screen.getByRole('region', { name: 'ป้ายลงเวลา · AR01 Area ทดลอง' });
    expect(within(checkIn).getByText(/AR01-IN/)).toBeInTheDocument();
    expect(within(checkIn).getByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).toHaveAttribute(
      'href',
      '/admin/areas/1/capture',
    );
    expect(within(checkIn).getByText(/วัดที่ป้ายจริง ±14 ม\. · รัศมี 50 ม\./)).toBeInTheDocument();
    expect(within(checkIn).getByRole('link', { name: 'พิมพ์ป้ายลงเวลา' })).toHaveAttribute(
      'href',
      '/admin/print?area=1&sign=100',
    );
  });

  it('searches code, name and building, clears no matches, and preserves a deep-linked selection', async () => {
    const { calls } = serve();
    const { user } = await renderApp('/admin/areas/2');
    await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' });
    const list = screen.getByRole('region', { name: 'พื้นที่ทั้งหมด (2)' });
    const search = within(list).getByRole('searchbox', { name: 'ค้นหาพื้นที่' });
    for (const text of ['AR01', 'Area', 'ตึกทดลอง', 'A']) {
      await user.clear(search);
      await user.type(search, text);
      expect(within(list).getByRole('link', { name: /^AR01 Area ทดลอง/ })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    }
    await user.clear(search);
    await user.type(search, 'no match');
    expect(within(list).getByText('ไม่พบพื้นที่ที่ตรงกับตัวกรอง')).toBeInTheDocument();
    expect(within(list).getByRole('status')).toHaveTextContent('แสดง 0 จาก 2 พื้นที่');
    expect(within(list).queryByRole('link')).not.toBeInTheDocument();
    await user.click(within(list).getByRole('button', { name: 'ล้างตัวกรอง' }));
    expect(search).toHaveValue('');
    expect(within(list).getByRole('status')).toHaveTextContent('แสดง 2 จาก 2 พื้นที่');
    expect(calls.some((call) => call.method !== 'GET' && call.path.startsWith('/api/admin'))).toBe(false);
  });

  it('filters buildings including an inactive Area’s building without replacing selected detail', async () => {
    const inactive = areaSummary({ id: 3, code: 'AR03', name: 'Closed area', buildingId: 2, buildingCode: 'B', isActive: false });
    serve({ 'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary, inactive] }) });
    const { user } = await renderApp('/admin/areas/1');
    await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' });
    const list = screen.getByRole('region', { name: 'พื้นที่ทั้งหมด (3)' });
    const filter = within(list).getByRole('combobox', { name: 'ตึก' });
    await user.selectOptions(filter, '2');
    expect(within(list).getByRole('link', { name: /AR03 Closed area/ })).toBeInTheDocument();
    expect(within(list).queryByRole('link', { name: /AR01 Area ทดลอง/ })).not.toBeInTheDocument();
    expect(within(list).getByRole('status')).toHaveTextContent('แสดง 1 จาก 3 พื้นที่');
    expect(screen.getByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' })).toBeInTheDocument();
    await user.type(within(list).getByRole('searchbox'), 'AR01');
    expect(within(list).getByText('ไม่พบพื้นที่ที่ตรงกับตัวกรอง')).toBeInTheDocument();
    await user.click(within(list).getByRole('button', { name: 'ล้างตัวกรอง' }));
    expect(filter).toHaveValue('');
    expect(within(list).getByRole('status')).toHaveTextContent('แสดง 3 จาก 3 พื้นที่');
  });

  it('opens the Area form in the right-hand Drawer, without leaving the page (owner, 2026-10-23)', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');

    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));

    const editor = await screen.findByRole('dialog', { name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' });
    expect(await within(editor).findByLabelText('ชื่อพื้นที่')).toHaveValue('Area ทดลอง');
    // the table is still there behind the Drawer
    expect(screen.getByRole('row', { name: /AR01-01/ })).toBeInTheDocument();
    await user.click(within(editor).getByRole('button', { name: 'ยกเลิก' }));
    expect(screen.queryByRole('dialog', { name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' })).not.toBeInTheDocument();
  });

  it('asks the server again each time an Area is opened (another device may have changed it)', async () => {
    let newer = false;
    const server = serve({
      'GET /api/admin/areas/1': () => {
        const detail = areaDetail();
        if (newer) {
          detail.points[1] = adminPoint({
            id: 12,
            name: 'ห้องน้ำหญิง',
            sortOrder: 2,
            sign: sign({ id: 102, code: 'AR01-02', locationAccuracyM: 9 }),
            roundWindows: [{ id: 5, shift: 'Day', start: '08:00', end: '10:00' }],
          });
        }
        return { status: 200, body: detail };
      },
    });
    const { user } = await renderApp('/admin/areas/1');
    expect(await screen.findByText('ยังไม่มีพิกัด')).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ }));
    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    newer = true;
    await user.click(screen.getByRole('link', { name: /^AR01 Area ทดลอง/ }));

    expect(await within(row(/AR01-02/)).findByText('วัดที่ป้ายจริง ±9 ม.')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.method === 'GET' && call.path === '/api/admin/areas/1')).toHaveLength(2);
  });

  it('opens the Area in the address: a day-only Area with no Cleaner and no point', async () => {
    serve();
    await renderApp('/admin/areas/2');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('ตึก A · ตึกทดลอง · เฉพาะกะเช้า · 0 จุด')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่มี')).toBeInTheDocument();
    expect(screen.getByText('พื้นที่นี้ยังไม่มีจุดทำความสะอาด')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('link', { name: /^AR01 Area ทดลอง/ })).not.toHaveAttribute('aria-current');
  });

  it('a deactivated Area says so and offers nothing to print or capture', async () => {
    const detail = areaDetail({ area: areaSummary({ isActive: false }) });
    serve({
      'GET /api/admin/areas': () => ({ status: 200, body: [detail.area] }),
      'GET /api/admin/areas/1': () => ({ status: 200, body: detail }),
    });
    const { user } = await renderApp('/admin/areas/1');

    expect(
      await screen.findByText('พื้นที่นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไขพื้นที่"'),
    ).toBeInTheDocument();
    expect(within(screen.getByRole('main')).queryByRole('link', { name: 'พิมพ์ป้าย' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    // The Drawer keeps the active-state print/capture restrictions.
    await user.click(screen.getByRole('button', { name: 'แก้ไขพื้นที่' }));
    expect(screen.queryByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์ป้ายลงเวลา' })).not.toBeInTheDocument();
    expect(screen.getByText('ตึก A · 2 จุด')).toBeInTheDocument();
    const list = screen.getByRole('region', { name: 'พื้นที่ทั้งหมด (1)' });
    expect(within(list).getByText('ปิดใช้งาน')).toBeInTheDocument();
  });

  it('a deactivated point stays in the table, marked, with no print link', async () => {
    const detail = areaDetail();
    detail.points[1] = { ...detail.points[1], isActive: false };
    serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    await renderApp('/admin/areas/1');

    const women = await screen.findByRole('row', { name: /AR01-02/ });
    expect(within(women).getByText('ปิดใช้งาน')).toBeInTheDocument();
    expect(within(women).queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    expect(screen.getByText('ตึก A · ตึกทดลอง · 2 กะ · 1 จุด')).toBeInTheDocument();
  });

  it('with no Area yet it says how to start', async () => {
    serve({ 'GET /api/admin/areas': () => ({ status: 200, body: [] }) });
    await renderApp('/admin/areas');

    expect(await screen.findByText('ยังไม่มีพื้นที่')).toBeInTheDocument();
    expect(screen.getByText('เริ่มจากปุ่ม "เพิ่มพื้นที่" ด้านบน')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'พื้นที่ทั้งหมด (0)' })).toBeInTheDocument();
  });

  it('an Area that does not exist shows the server’s message', async () => {
    serve({ 'GET /api/admin/areas/9': () => ({ status: 404, body: { message: 'ไม่พบพื้นที่นี้' } }) });
    await renderApp('/admin/areas/9');

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบพื้นที่นี้');
  });

  it('is for Admins only (facility-0010)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    await renderApp('/admin/areas');

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
  });

  it('switching Areas shows the new Area only, never the previous one’s signs', async () => {
    let answerAreaTwo: () => void = () => {};
    const areaTwoAnswer = new Promise<void>((resolve) => {
      answerAreaTwo = resolve;
    });
    serve({
      'GET /api/admin/areas/2': async () => {
        await areaTwoAnswer;
        return { status: 200, body: officeDetail() };
      },
    });
    const { user } = await renderApp('/admin/areas/1');

    expect(await screen.findByRole('row', { name: /AR01-01/ })).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ }));

    // While AR02 is still loading, AR01's signs must not be shown under AR02's address.
    expect(await screen.findByText('กำลังโหลดพื้นที่...')).toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /AR01-01/ })).not.toBeInTheDocument();

    answerAreaTwo();
    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /AR01-01/ })).not.toBeInTheDocument();
  });

  it('an id that is not a number says so and asks the server for no Area', async () => {
    const { calls } = serve();
    await renderApp('/admin/areas/abc');

    const alerts = await screen.findAllByRole('alert');
    expect(alerts).toHaveLength(1);
    expect(alerts[0]).toHaveTextContent('ไม่พบพื้นที่นี้');
    expect(calls.some((call) => /^\/api\/admin\/areas\/./.test(call.path))).toBe(false);
  });

  it('the page note and the save note wait behind ℹ; the sign help stays (owner, 2026-10-27)', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');

    await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' });
    expectBehindMoreInfo(
      'แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ · ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน',
      'เกี่ยวกับหน้านี้',
    );
    await user.click(screen.getByRole('button', { name: 'แก้ไขพื้นที่' }));
    expectInView(/^เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย/);
    const editor = await screen.findByRole('dialog', { name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' });
    await within(editor).findByLabelText('ชื่อพื้นที่');
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });

});
