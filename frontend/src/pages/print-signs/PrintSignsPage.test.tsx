import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession, areaDetail, checkInSign, areaSummary, officeDetail, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import { expectBehindMoreInfo, expectInView } from '../../test/moreInfo';
import { qrPath } from './qr';

const secondBuildingArea = { ...officeSummary, buildingId: 2, buildingCode: 'B' };
const closedArea = areaSummary({ id: 3, code: 'AR03', name: 'Area ปิด', isActive: false });

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), secondBuildingArea, closedArea] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail({ area: secondBuildingArea }) }),
    ...routes,
  });
}

const cardNames = () => screen.getAllByRole('article').map((card) => card.getAttribute('aria-label'));

/** The path data drawn inside one card's QR, found by the QR's own label. */
const qrDrawing = (card: HTMLElement, label: string) =>
  within(card).getByRole('img', { name: label }).querySelector('path')?.getAttribute('d');

describe('PrintSignsPage (D5, facility-0021 item 6)', () => {

  it('selects only active Areas and signs in the chosen building and keeps the group in the address', async () => {
    const detail = areaDetail();
    detail.points[1].isActive = false;
    const server = serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    const { user, router } = await renderApp('/admin/print');
    const group = await screen.findByRole('option', { name: 'ทุกพื้นที่ในตึก A (1)' });
    await user.selectOptions(screen.getByLabelText('พื้นที่'), group);
    expect(router.state.location.search).toBe('?area=building%3A1');
    await screen.findByRole('article', { name: 'ป้าย AR01-IN' });
    expect(cardNames()).toEqual(['ป้าย AR01-IN', 'ป้าย AR01-01']);
    expect(server.calls.some((call) => ['/api/admin/areas/2', '/api/admin/areas/3'].includes(call.path))).toBe(false);
  });

  it('opens a building group URL directly and offers both sizes as pressed buttons', async () => {
    serve();
    const { user } = await renderApp('/admin/print?area=building:2');
    expect(await screen.findByRole('article', { name: 'ป้าย AR02-IN' })).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR02-IN']);
    const sizes = screen.getByRole('group', { name: 'ขนาดป้าย' });
    const small = within(sizes).getByRole('button', { name: 'เล็ก (6 ต่อแผ่น)' });
    const large = within(sizes).getByRole('button', { name: 'A5 (2 ต่อแผ่น)' });
    expect(small).toHaveAttribute('aria-pressed', 'true');
    expect(large).toHaveAttribute('aria-pressed', 'false');
    await user.click(large);
    expect(large).toHaveAttribute('aria-pressed', 'true');
    await user.click(small);
    expect(small).toHaveAttribute('aria-pressed', 'true');
  });

  it('does not print cached tokens when refreshing fails', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    let failed = false;
    serve({ 'GET /api/admin/areas/1': () => failed
      ? ({ status: 500, body: { message: 'โหลดป้ายล่าสุดไม่ได้' } })
      : ({ status: 200, body: areaDetail() }) });
    const { user } = await renderApp('/admin/print?area=1');
    await screen.findByRole('article', { name: 'ป้าย AR01-IN' });
    failed = true;
    await user.click(screen.getByRole('button', { name: 'พิมพ์' }));
    expect(await screen.findByText('โหลดป้ายล่าสุดไม่ได้')).toBeInTheDocument();
    expect(print).not.toHaveBeenCalled();
  });

  it('prints one Area: the check-in sign and the active points, each with its QR and URL', async () => {
    serve();
    await renderApp('/admin/print?area=1');

    expect(await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย' })).toBeInTheDocument();
    expect(
      await screen.findByText(
        'ป้ายลงเวลา 1 + จุดทำความสะอาด 2 = 3 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ',
      ),
    ).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR01-IN', 'ป้าย AR01-01', 'ป้าย AR01-02']);

    const checkIn = screen.getByRole('article', { name: 'ป้าย AR01-IN' });
    expect(within(checkIn).getByText('ป้ายลงเวลา')).toBeInTheDocument();
    expect(within(checkIn).getByRole('heading', { name: 'Area ทดลอง' })).toBeInTheDocument();
    expect(within(checkIn).getByText('สแกนตอนเข้างาน ออกพัก กลับจากพัก และเลิกงาน')).toBeInTheDocument();
    expect(
      within(checkIn).getByText(`${window.location.origin}/check-in/9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d`),
    ).toBeInTheDocument();
    expect(within(checkIn).getByRole('img', { name: 'QR AR01-IN' })).toBeInTheDocument();
    // The QR encodes this sign's own URL, not another card's or the code
    expect(qrDrawing(checkIn, 'QR AR01-IN')).toBe(
      qrPath(`${window.location.origin}/check-in/9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d`).d,
    );

    const men = screen.getByRole('article', { name: 'ป้าย AR01-01' });
    expect(within(men).getByText('จุดทำความสะอาด')).toBeInTheDocument();
    expect(within(men).getByRole('heading', { name: 'ห้องน้ำชาย' })).toBeInTheDocument();
    expect(within(men).getByText('พื้นที่ AR01 · ตึก A')).toBeInTheDocument();
    expect(within(men).getByText('สแกนด้วยกล้องมือถือ')).toBeInTheDocument();
    expect(
      within(men).getByText('รอบ เช้า 07:00–09:00, 16:00–18:00 · ดึก 20:00–22:00, 03:00–05:00'),
    ).toBeInTheDocument();
    expect(within(men).getByText('รหัสป้าย AR01-01 · สร้าง QR 16/10/2026')).toBeInTheDocument();
    expect(
      within(men).getByText(`${window.location.origin}/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f`),
    ).toBeInTheDocument();
    expect(qrDrawing(men, 'QR AR01-01')).toBe(
      qrPath(`${window.location.origin}/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f`).d,
    );
    // facility-0060: the token is shown only inside the sign's URL
    expect(screen.queryByText('0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f')).not.toBeInTheDocument();
    // The test page is plain http, where a phone at the sign cannot read its location
    expect(screen.getByText(`ที่อยู่บนป้าย: ${window.location.origin}`)).toBeInTheDocument();
    expect(
      screen.getByText('ที่อยู่นี้ไม่ใช่ https มือถือจะอ่านตำแหน่งไม่ได้ตอนสแกน พิมพ์จากที่อยู่ https ของเซิร์ฟเวอร์'),
    ).toBeInTheDocument();
  });

  it('prints the check-in sign alone after "พิมพ์" on its new QR', async () => {
    serve();
    await renderApp('/admin/print?area=1&sign=100');

    expect(await screen.findByRole('article', { name: 'ป้าย AR01-IN' })).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR01-IN']);
  });

  it('A5 puts 2 signs on a sheet', async () => {
    serve();
    const { user } = await renderApp('/admin/print?area=1');
    await screen.findByRole('article', { name: 'ป้าย AR01-IN' });

    await user.click(screen.getByRole('button', { name: 'A5 (2 ต่อแผ่น)' }));

    expect(
      screen.getByText('ป้ายลงเวลา 1 + จุดทำความสะอาด 2 = 3 ป้าย · กระดาษ A4 แผ่นละ 2 ป้าย · 2 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'แผ่นที่ 1' })).getAllByRole('article')).toHaveLength(2);
    expect(within(screen.getByRole('region', { name: 'แผ่นที่ 2' })).getAllByRole('article')).toHaveLength(1);
  });

  it('prints a single sign after "พิมพ์" on a row or a new QR', async () => {
    serve();
    await renderApp('/admin/print?area=1&sign=101');

    expect(await screen.findByRole('article', { name: 'ป้าย AR01-01' })).toBeInTheDocument();
    expect(cardNames()).toEqual(['ป้าย AR01-01']);
    expect(
      screen.getByText('ป้ายลงเวลา 0 + จุดทำความสะอาด 1 = 1 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'พิมพ์ทั้งพื้นที่ ›' })).toHaveAttribute('href', '/admin/print?area=1');
  });

  it('prints every active Area, leaving out deactivated Areas and points', async () => {
    const detail = areaDetail();
    detail.points[1] = { ...detail.points[1], isActive: false };
    const server = serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    await renderApp('/admin/print?area=all');

    expect(await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย' })).toBeInTheDocument();
    await waitFor(() => expect(cardNames()).toEqual(['ป้าย AR01-IN', 'ป้าย AR01-01', 'ป้าย AR02-IN']));
    expect(
      screen.getByText('ป้ายลงเวลา 2 + จุดทำความสะอาด 1 = 3 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ'),
    ).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/admin/areas/3')).toBe(false);
    expect(screen.getByRole('option', { name: 'ทุกพื้นที่ (2)' })).toBeInTheDocument();
  });

  it('choosing an Area changes the address, and "พิมพ์" opens the browser’s print', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    serve();
    const { user, router } = await renderApp('/admin/print');

    expect(await screen.findByText('เลือกพื้นที่ที่จะพิมพ์')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'พิมพ์' })).toBeDisabled();
    await screen.findByRole('option', { name: 'AR02 Office ทดลอง · ตึก B' });
    await user.selectOptions(screen.getByLabelText('พื้นที่'), 'AR02 Office ทดลอง · ตึก B');

    expect(router.state.location.search).toBe('?area=2');
    expect(await screen.findByRole('article', { name: 'ป้าย AR02-IN' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'พิมพ์' }));
    await waitFor(() => expect(print).toHaveBeenCalledOnce());
  });

  it('"พิมพ์" asks the server first, so a QR renewed meanwhile is the one printed', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => {
      expect(screen.getByText(`${window.location.origin}/check-in/11111111-2222-4333-8444-555555555555`)).toBeInTheDocument();
    });
    let renewed = false;
    serve({
      'GET /api/admin/areas/1': () => ({
        status: 200,
        body: areaDetail({
          checkInSign: checkInSign(renewed ? { qrToken: '11111111-2222-4333-8444-555555555555' } : {}),
        }),
      }),
    });
    const { user } = await renderApp('/admin/print?area=1&sign=100');
    expect(
      await screen.findByText(`${window.location.origin}/check-in/9a8b7c6d-5e4f-4a3b-9c2d-1e0f9a8b7c6d`),
    ).toBeInTheDocument();

    renewed = true;
    await user.click(screen.getByRole('button', { name: 'พิมพ์' }));

    await waitFor(() => expect(print).toHaveBeenCalledOnce());
    expect(
      await screen.findByText(`${window.location.origin}/check-in/11111111-2222-4333-8444-555555555555`),
    ).toBeInTheDocument();
  });

  it('a deactivated Area has nothing to print', async () => {
    serve({
      'GET /api/admin/areas/3': () => ({ status: 200, body: areaDetail({ area: closedArea }) }),
    });
    await renderApp('/admin/print?area=3');

    expect(await screen.findByText('พื้นที่นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์')).toBeInTheDocument();
    expect(screen.queryByRole('article')).not.toBeInTheDocument();
  });

  it('how the QR is made waits behind ℹ; the test scan after hanging stays (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/print?area=1');

    await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย' });
    expectBehindMoreInfo('ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก', 'เกี่ยวกับ QR');
    expectInView('ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย');
  });

});
