import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession, areaDetail, areaSummary, officeDetail, officeSummary } from '../../test/adminFixtures';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
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
    await renderApp('/admin/areas');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'จัดการ Area จุด และพิกัดป้าย' })).toBeInTheDocument();
    const list = screen.getByRole('region', { name: 'Area ทั้งหมด (2)' });
    expect(within(list).getByText('ตึก A · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByText('ตึก A · เฉพาะกะเช้า · 0 จุด · ป้ายยังไม่ยืนยัน 1')).toBeInTheDocument();
    expect(within(list).getByRole('link', { name: /^AR01 Area ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(within(list).getByRole('link', { name: '+ เพิ่ม Area' })).toHaveAttribute('href', '/admin/areas/new');

    expect(screen.getByText('Area ที่เลือก · ตึก A')).toBeInTheDocument();
    expect(
      screen.getByText('ทำ 2 กะ (เช้า + ดึก) · แม่บ้านประจำ: เช้า แม่บ้านทดสอบ · ดึก แม่บ้านกะดึก · ป้าย 1 + 2'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'แก้ไข Area' })).toHaveAttribute('href', '/admin/areas/1/edit');
    expect(screen.getByRole('link', { name: 'พิมพ์ป้ายของ Area นี้ ›' })).toHaveAttribute('href', '/admin/print?area=1');
    expect(screen.getByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).toHaveAttribute('href', '/admin/areas/1/capture');

    const checkIn = row(/AR01-IN/);
    expect(within(checkIn).getByText('หน้างาน ±14 ม. · รัศมี 50 ม.')).toBeInTheDocument();
    expect(within(checkIn).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=100');
    const men = row(/AR01-01/);
    expect(within(men).getByText('เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00')).toBeInTheDocument();
    expect(within(men).getByRole('link', { name: 'พิมพ์' })).toHaveAttribute('href', '/admin/print?area=1&sign=101');
    const women = row(/AR01-02/);
    expect(within(women).getByText('เช้า 08:00–10:00')).toBeInTheDocument();
    expect(within(women).getByText('ยังไม่มีพิกัด · รัศมี 50 ม.')).toBeInTheDocument();
  });

  it('opens the Area in the address: a day-only Area with no Cleaner and no point', async () => {
    serve();
    await renderApp('/admin/areas/2');

    expect(await screen.findByRole('heading', { level: 2, name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('ทำเฉพาะกะเช้า · แม่บ้านประจำ: เช้า ยังไม่มี · ป้าย 1 + 0')).toBeInTheDocument();
    expect(screen.getByText('Area นี้ยังไม่มีจุดทำความสะอาด')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /^AR02 Office ทดลอง/ })).toHaveAttribute('aria-current', 'true');
    expect(screen.getByRole('link', { name: /^AR01 Area ทดลอง/ })).not.toHaveAttribute('aria-current');
  });

  it('a deactivated Area says so and offers nothing to print or capture', async () => {
    const detail = areaDetail({ area: areaSummary({ isActive: false }) });
    serve({
      'GET /api/admin/areas': () => ({ status: 200, body: [detail.area] }),
      'GET /api/admin/areas/1': () => ({ status: 200, body: detail }),
    });
    await renderApp('/admin/areas/1');

    expect(
      await screen.findByText('Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์ป้ายของ Area นี้ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'เก็บพิกัดบนมือถือ ›' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'พิมพ์' })).not.toBeInTheDocument();
    expect(screen.getByText('ตึก A · 2 กะ · 2 จุด · ป้ายยังไม่ยืนยัน 1 · ปิดใช้งาน')).toBeInTheDocument();
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

    expect(await screen.findByText('ยังไม่มี Area กด "+ เพิ่ม Area" เพื่อเริ่ม')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Area ทั้งหมด (0)' })).toBeInTheDocument();
  });

  it('an Area that does not exist shows the server’s message', async () => {
    serve({ 'GET /api/admin/areas/9': () => ({ status: 404, body: { message: 'ไม่พบ Area นี้' } }) });
    await renderApp('/admin/areas/9');

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบ Area นี้');
  });

  it('is for Admins only (facility-0010)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    await renderApp('/admin/areas');

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
  });
});
