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
    expect(screen.getByRole('heading', { level: 1, name: 'จัดการพื้นที่ จุด และพิกัดป้าย' })).toBeInTheDocument();
    const list = screen.getByRole('region', { name: 'พื้นที่ทั้งหมด (2)' });
    expect(within(list).getByText('ตึก A · ตึกทดลอง · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByText('ตึก A · ตึกทดลอง · เฉพาะกะเช้า · 0 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByRole('link', { name: /^AR01 Area ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(within(list).getByRole('link', { name: '+ เพิ่มพื้นที่' })).toHaveAttribute('href', '/admin/areas/new');

    expect(screen.getByText('พื้นที่ที่เลือก · ตึก A · ตึกทดลอง')).toBeInTheDocument();
    expect(
      screen.getByText('ทำ 2 กะ (เช้า + ดึก) · แม่บ้านประจำ: เช้า แม่บ้านทดสอบ · ดึก แม่บ้านกะดึก · ป้าย 1 + 2'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'พิมพ์ป้าย' })).toHaveAttribute('href', '/admin/print?area=1');
    // Tab 1 is the point list, and the tab itself carries the count; the check-in sign has its own tab
    expect(screen.getByRole('tab', { name: 'จุดทำความสะอาด (2)' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'ป้ายลงเวลา' })).toHaveAttribute('aria-selected', 'false');

    const men = row(/AR01-01/);
    expect(within(men).getByText('เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00')).toBeInTheDocument();
    expect(within(men).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=101');
    const women = row(/AR01-02/);
    expect(within(women).getByText('เช้า 08:00–10:00')).toBeInTheDocument();
    expect(within(women).getByText('ยังไม่มีพิกัด · รัศมี 50 ม.')).toBeInTheDocument();

    // The check-in sign is not a point row: it sits in Tab 2 with the place it was captured at
    expect(screen.queryByRole('row', { name: /AR01-IN/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: 'ป้ายลงเวลา' }));
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

  it('is a tablist: the arrow keys move between the two views', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');

    const points = await screen.findByRole('tab', { name: 'จุดทำความสะอาด (2)' });
    points.focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'ป้ายลงเวลา' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'ป้ายลงเวลา' })).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(points).toHaveAttribute('aria-selected', 'true');
    expect(points).toHaveFocus();
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
    expect(await screen.findByText('ยังไม่มีพิกัด · รัศมี 50 ม.')).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ }));
    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    newer = true;
    await user.click(screen.getByRole('link', { name: /^AR01 Area ทดลอง/ }));

    expect(await within(row(/AR01-02/)).findByText('วัดที่ป้ายจริง ±9 ม. · รัศมี 50 ม.')).toBeInTheDocument();
    expect(server.calls.filter((call) => call.method === 'GET' && call.path === '/api/admin/areas/1')).toHaveLength(2);
  });

  it('opens the Area in the address: a day-only Area with no Cleaner and no point', async () => {
    serve();
    await renderApp('/admin/areas/2');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('ทำเฉพาะกะเช้า · แม่บ้านประจำ: เช้า ยังไม่มี · ป้าย 1 + 0')).toBeInTheDocument();
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
    expect(screen.queryByRole('link', { name: 'พิมพ์ป้าย' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    // the capture link lives in Tab 2, which offers nothing either
    await user.click(screen.getByRole('tab', { name: 'ป้ายลงเวลา' }));
    expect(screen.queryByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์ป้ายลงเวลา' })).not.toBeInTheDocument();
    expect(await screen.findByText('ตึก A · ตึกทดลอง · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1 · ปิดใช้งาน')).toBeInTheDocument();
  });

  it('a deactivated point stays in the table, marked, with no print link', async () => {
    const detail = areaDetail();
    detail.points[1] = { ...detail.points[1], isActive: false };
    serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    await renderApp('/admin/areas/1');

    const women = await screen.findByRole('row', { name: /AR01-02/ });
    expect(within(women).getByText('จุดทำความสะอาด · ปิดใช้งาน')).toBeInTheDocument();
    expect(within(women).queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    expect(
      screen.getByText('ทำ 2 กะ (เช้า + ดึก) · แม่บ้านประจำ: เช้า แม่บ้านทดสอบ · ดึก แม่บ้านกะดึก · ป้าย 1 + 1'),
    ).toBeInTheDocument();
  });

  it('with no Area yet it says how to start', async () => {
    serve({ 'GET /api/admin/areas': () => ({ status: 200, body: [] }) });
    await renderApp('/admin/areas');

    expect(await screen.findByText('ยังไม่มีพื้นที่ กด "+ เพิ่มพื้นที่" เพื่อเริ่ม')).toBeInTheDocument();
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
    await user.click(screen.getByRole('tab', { name: 'ป้ายลงเวลา' }));
    expectInView(/^เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย/);

    await user.click(screen.getByRole('button', { name: 'แก้ไขพื้นที่' }));
    const editor = await screen.findByRole('dialog', { name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' });
    await within(editor).findByLabelText('ชื่อพื้นที่');
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });

});
