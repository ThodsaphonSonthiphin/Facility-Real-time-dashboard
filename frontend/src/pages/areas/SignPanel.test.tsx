import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminSession,
  buildings,
  cleaners,
  areaDetail,
  areaSummary,
  checkInSign,
  officeSummary,
  sign,
  unlocatedSign,
} from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/cleaners': () => ({ status: 200, body: cleaners }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    ...routes,
  });
}

describe('SignPanel (D4, facility-0045, 0038, 0021)', () => {
  it('the check-in sign: its location, the capture link, and the radius', async () => {
    const server = serve({
      'PUT /api/admin/signs/100/radius': ({ body }) => ({
        status: 200,
        body: checkInSign({ radiusM: (body as { radiusM: number }).radiusM }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const editor = screen.getByRole('region', { name: 'ป้ายลงเวลา · AR01 Area ทดลอง' });
    const place = within(editor).getByRole('region', { name: 'พิกัดของป้าย' });
    expect(within(place).getByText('วัดที่ป้ายจริง ±14 ม. · 13.756300, 100.501800')).toBeInTheDocument();
    expect(within(place).getByRole('link', { name: 'เก็บพิกัดที่ป้ายด้วยมือถือ (M17) ›' })).toHaveAttribute(
      'href',
      '/admin/areas/1/capture?sign=100',
    );
    // Confirmed on site: a map pick is not offered (the server would refuse it)
    expect(within(place).queryByLabelText('ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน')).not.toBeInTheDocument();

    const radius = within(editor).getByRole('region', { name: 'รัศมีของป้าย' });
    const field = within(radius).getByLabelText('รัศมี (เมตร)');
    expect(field).toHaveValue('50');
    await user.clear(field);
    await user.type(field, '5');
    await user.click(within(radius).getByRole('button', { name: 'บันทึกรัศมี' }));
    expect(within(radius).getByRole('alert')).toHaveTextContent('รัศมีต้องอยู่ระหว่าง 10–500 เมตร');
    expect(server.calls.some((call) => call.method === 'PUT')).toBe(false);

    await user.clear(field);
    await user.type(field, '80{Enter}');
    expect(server.calls.some((call) => call.method === 'PUT')).toBe(false);
    await user.click(within(radius).getByRole('button', { name: 'บันทึกรัศมี' }));
    expect(await within(radius).findByRole('status')).toHaveTextContent('บันทึกรัศมี 80 ม. แล้ว ใช้กับการสแกนครั้งต่อไป');
    expect(server.calls.find((call) => call.method === 'PUT')).toMatchObject({
      path: '/api/admin/signs/100/radius',
      body: { radiusM: 80 },
    });
  });

  it('the Area Drawer preserves the check-in map fallback and surfaces save errors', async () => {
    const unlocated = checkInSign({ latitude: undefined, longitude: undefined, locationSource: undefined, locationAccuracyM: undefined });
    const server = serve({
      'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail({ checkInSign: unlocated }) }),
      'PUT /api/admin/signs/100/location': () => ({ status: 409, body: { message: 'มีผู้แก้ไขพิกัดแล้ว เปิดพื้นที่ใหม่' } }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));
    const editor = screen.getByRole('region', { name: 'ป้ายลงเวลา · AR01 Area ทดลอง' });
    const place = within(editor).getByRole('region', { name: 'พิกัดของป้าย' });
    await user.type(within(place).getByLabelText('ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน'), '13.7563, 100.5018');
    await user.click(within(place).getByRole('button', { name: 'บันทึกพิกัดที่ปักจากแผนที่' }));
    expect(await within(place).findByRole('alert')).toHaveTextContent('มีผู้แก้ไขพิกัดแล้ว เปิดพื้นที่ใหม่');
    expect(server.calls.find((call) => call.method === 'PUT')).toMatchObject({
      path: '/api/admin/signs/100/location', body: { latitude: 13.7563, longitude: 100.5018, source: 'Map' },
    });
  });

  it('a sign with no location takes coordinates typed from a map, as not confirmed on site', async () => {
    const server = serve({
      'PUT /api/admin/signs/102/location': () => ({
        status: 200,
        body: unlocatedSign({ latitude: 13.7563, longitude: 100.5018, locationSource: 'Map' }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-02' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01-02' });
    const place = within(editor).getByRole('region', { name: 'พิกัดของป้าย' });
    expect(within(place).getByText('ยังไม่มีพิกัด')).toBeInTheDocument();
    const field = within(place).getByLabelText('ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน');
    await user.type(field, 'ตึก A');
    await user.click(within(place).getByRole('button', { name: 'บันทึกพิกัดที่ปักจากแผนที่' }));
    expect(within(place).getByRole('alert')).toHaveTextContent('ใส่พิกัดแบบ 13.756300, 100.501800');

    await user.clear(field);
    await user.type(field, '13.7563, 100.5018');
    await user.click(within(place).getByRole('button', { name: 'บันทึกพิกัดที่ปักจากแผนที่' }));
    expect(await within(place).findByRole('status')).toHaveTextContent(
      'บันทึกพิกัดที่ปักจากแผนที่แล้ว ป้ายนี้ยังไม่ได้วัดที่ป้ายจริง',
    );
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      latitude: 13.7563,
      longitude: 100.5018,
      source: 'Map',
    });
  });

  it('a new QR asks once, then offers to print the new sign (facility-0021 item 5)', async () => {
    const server = serve({
      'POST /api/admin/signs/101/regenerate-token': () => ({
        status: 200,
        body: sign({ qrToken: '7c6b5a49-3827-4615-a4b3-c2d1e0f9a8b7', qrIssuedAt: '2026-10-19T03:00:00Z' }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const qr = within(screen.getByRole('region', { name: 'แก้ไขจุด · AR01-01' })).getByRole('region', {
      name: 'QR ของป้าย',
    });
    expect(within(qr).getByText('สร้าง QR ล่าสุด 16/10/2026 09:00')).toBeInTheDocument();

    await user.click(within(qr).getByRole('button', { name: 'สร้าง QR ใหม่' }));
    const ask = within(qr).getByRole('alertdialog', { name: 'สร้าง QR ใหม่ของป้าย AR01-01 ห้องน้ำชาย?' });
    expect(ask).toHaveAccessibleDescription(
      'ป้ายที่ติดอยู่ตอนนี้จะสแกนไม่ได้ทันที ต้องพิมพ์ป้ายใหม่ไปติดแทน',
    );
    await user.click(within(ask).getByRole('button', { name: 'ยกเลิก' }));
    expect(server.calls.some((call) => call.path.endsWith('/regenerate-token'))).toBe(false);

    await user.click(within(qr).getByRole('button', { name: 'สร้าง QR ใหม่' }));
    expect(within(screen.getByRole('alertdialog')).getByRole('checkbox', { name: 'ไปหน้าพิมพ์ป้ายต่อเลย' })).toBeChecked();
    await user.click(within(screen.getByRole('alertdialog')).getByRole('checkbox', { name: 'ไปหน้าพิมพ์ป้ายต่อเลย' }));
    await user.click(within(qr).getByRole('button', { name: 'ยืนยัน สร้าง QR ใหม่' }));
    expect(await within(qr).findByText('สร้าง QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว')).toBeInTheDocument();
    expect(within(qr).getByRole('link', { name: 'พิมพ์ป้ายใหม่ ›' })).toHaveAttribute(
      'href',
      '/admin/print?area=1&sign=101',
    );
    expect(server.calls.filter((call) => call.path === '/api/admin/signs/101/regenerate-token')).toHaveLength(1);
  });

  it('a deactivated point gets no new QR (facility-0021 rule 4)', async () => {
    const detail = areaDetail();
    detail.points[0] = { ...detail.points[0], isActive: false };
    serve({ 'GET /api/admin/areas/1': () => ({ status: 200, body: detail }) });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const qr = screen.getByRole('region', { name: 'QR ของป้าย' });
    expect(within(qr).queryByRole('button', { name: 'สร้าง QR ใหม่' })).not.toBeInTheDocument();
    expect(within(qr).getByText('ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้')).toBeInTheDocument();
  });

  it('no new QR for any sign of a deactivated Area (facility-0021 rule 4)', async () => {
    const inactive = areaSummary({ isActive: false });
    serve({
      'GET /api/admin/areas': () => ({ status: 200, body: [inactive, officeSummary] }),
      'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail({ area: inactive }) }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const checkInQr = screen.getByRole('region', { name: 'QR ของป้าย' });
    expect(within(checkInQr).queryByRole('button', { name: 'สร้าง QR ใหม่' })).not.toBeInTheDocument();
    expect(within(checkInQr).getByText('ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้')).toBeInTheDocument();

    await user.click(within(screen.getByRole('dialog', { name: 'แก้ไขพื้นที่' })).getByRole('button', { name: 'ปิด' }));
    await user.click(screen.getByRole('button', { name: 'แก้ไข AR01-01' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));
    const pointQr = within(screen.getByRole('region', { name: 'แก้ไขจุด · AR01-01' })).getByRole('region', {
      name: 'QR ของป้าย',
    });
    expect(within(pointQr).queryByRole('button', { name: 'สร้าง QR ใหม่' })).not.toBeInTheDocument();
    expect(within(pointQr).getByText('ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้')).toBeInTheDocument();
  });

  it('the check-in sign gets a new QR on an active Area, then offers to print it', async () => {
    const server = serve({
      'POST /api/admin/signs/100/regenerate-token': () => ({ status: 200, body: checkInSign() }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));

    const qr = screen.getByRole('region', { name: 'QR ของป้าย' });
    await user.click(within(qr).getByRole('button', { name: 'สร้าง QR ใหม่' }));
    expect(within(screen.getByRole('alertdialog')).getByRole('checkbox', { name: 'ไปหน้าพิมพ์ป้ายต่อเลย' })).toBeChecked();
    await user.click(within(screen.getByRole('alertdialog')).getByRole('checkbox', { name: 'ไปหน้าพิมพ์ป้ายต่อเลย' }));
    await user.click(within(qr).getByRole('button', { name: 'ยืนยัน สร้าง QR ใหม่' }));
    expect(await within(qr).findByText('สร้าง QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว')).toBeInTheDocument();
    expect(within(qr).getByRole('link', { name: 'พิมพ์ป้ายใหม่ ›' })).toHaveAttribute(
      'href',
      '/admin/print?area=1&sign=100',
    );
    expect(server.calls.filter((call) => call.path === '/api/admin/signs/100/regenerate-token')).toHaveLength(1);
  });
  it('keeps a pending QR question locked, retains its error for retry, then follows the checked print route only on success', async () => {
    let release: (value: { status: number; body: unknown }) => void = () => {};
    let attempts = 0;
    const server = serve({
      'POST /api/admin/signs/101/regenerate-token': () => {
        attempts++;
        return attempts === 1 ? new Promise((resolve) => { release = resolve; }) : { status: 200, body: sign() };
      },
    });
    const { user, router } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));
    const parent = screen.getByRole('dialog', { name: 'แก้ไขจุด · AR01-01' });
    const opener = within(parent).getByRole('button', { name: 'สร้าง QR ใหม่' });
    await user.click(opener);
    const ask = screen.getByRole('alertdialog');
    expect(ask.tagName).toBe('DIALOG');
    expect(within(ask).getByRole('checkbox')).toBeChecked();
    expect(attempts).toBe(0);
    await user.click(within(ask).getByRole('button', { name: 'ยืนยัน สร้าง QR ใหม่' }));
    await waitFor(() => expect(attempts).toBe(1));
    expect(within(ask).getByRole('button', { name: 'ยกเลิก' })).toBeDisabled();
    await user.keyboard('{Escape}');
    expect(ask).toBeInTheDocument();
    expect(parent).toBeInTheDocument();
    release({ status: 409, body: { message: 'ลองสร้าง QR อีกครั้ง' } });
    expect(await within(ask).findByRole('alert')).toHaveTextContent('ลองสร้าง QR อีกครั้ง');
    expect(within(ask).getByRole('checkbox')).toBeChecked();
    expect(parent).toBeInTheDocument();
    await user.click(within(ask).getByRole('button', { name: 'ยืนยัน สร้าง QR ใหม่' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/print'));
    expect(router.state.location.search).toBe('?area=1&sign=101');
    expect(server.calls.filter((call) => call.path.endsWith('/regenerate-token'))).toHaveLength(2);
  });

});
