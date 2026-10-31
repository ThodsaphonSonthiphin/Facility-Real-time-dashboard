import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { accounts, adminSession, areaDetail, areaSummary, buildings, cleaners, managedBuildings, officeDetail, officeSummary } from '../../test/adminFixtures';
import { fakeServer, type FakeResponse } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
const cases = [
  { name: 'Area save', route: '/admin/areas/1', opener: 'แก้ไขพื้นที่', field: 'ชื่อพื้นที่', save: /บันทึก.*พิมพ์ป้าย/, path: 'PUT /api/admin/areas/1' },
  { name: 'Point save', route: '/admin/areas/1', opener: 'แก้ไข AR01-01', field: 'ชื่อจุด', save: /^บันทึกจุด$/, path: 'PUT /api/admin/points/11' },
  { name: 'Building save', route: '/admin/buildings', opener: 'แก้ไข ตึก A', field: 'ชื่อตึก', save: /^บันทึก$/, path: 'PUT /api/admin/buildings/1' },
  { name: 'Area independent radius', route: '/admin/areas/1', opener: 'แก้ไขพื้นที่', field: 'รัศมี (เมตร)', save: /^บันทึกรัศมี$/, path: 'PUT /api/admin/signs/100/radius' },
  { name: 'Point independent radius', route: '/admin/areas/1', opener: 'แก้ไข AR01-01', field: 'รัศมี (เมตร)', save: /^บันทึกรัศมี$/, path: 'PUT /api/admin/signs/101/radius' },
];
function setup(path: string, answer: () => Promise<FakeResponse>) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    'GET /api/admin/accounts': () => ({ status: 200, body: accounts }),
    'GET /api/admin/cleaners': () => ({ status: 200, body: cleaners }),
    'GET /api/admin/areas': () => ({ status: 200, body: [areaSummary(), officeSummary] }),
    'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }),
    'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail() }),
    [path]: answer,
  });
}
describe('setup Drawer pending dismissal', () => {
  it.each(cases)('$name preserves the draft through Escape/header dismissal and a failed request', async (test) => {
    let answer: (response: FakeResponse) => void = () => undefined;
    const response = new Promise<FakeResponse>((resolve) => { answer = resolve; });
    const server = setup(test.path, () => response);
    const { user } = await renderApp(test.route);
    await user.click(await screen.findByRole('button', { name: test.opener }));
    const drawer = screen.getByRole('dialog');
    const field = await within(drawer).findByLabelText(test.field);
    await user.clear(field);
    const draft = test.field === 'รัศมี (เมตร)' ? '75' : 'ชื่อที่ยังไม่บันทึก';
    await user.type(field, draft);
    await user.click(within(drawer).getByRole('button', { name: test.save }));
    await waitFor(() => expect(server.calls.some((call) => `${call.method} ${call.path}` === test.path)).toBe(true));
    fireEvent.keyDown(drawer, { key: 'Escape' });
    expect(drawer).toBeInTheDocument();
    await user.click(within(drawer).getByRole('button', { name: 'ปิด' }));
    expect(drawer).toBeInTheDocument();
    answer({ status: 409, body: { message: 'ลองใหม่หลังตรวจข้อมูล' } });
    expect(await within(drawer).findByText('ลองใหม่หลังตรวจข้อมูล')).toBeInTheDocument();
    expect(field).toHaveValue(draft);
    fireEvent.keyDown(drawer, { key: 'Escape' });
    expect(drawer).not.toBeInTheDocument();
  });
  it('standalone Area create keeps Cancel disabled until a failed save returns', async () => {
    let answer: (response: FakeResponse) => void = () => undefined;
    const response = new Promise<FakeResponse>((resolve) => { answer = resolve; });
    const server = setup('POST /api/admin/areas', () => response);
    const { user, router } = await renderApp('/admin/areas/new');
    await user.type(await screen.findByLabelText('รหัสพื้นที่'), 'AR03');
    await user.type(screen.getByLabelText('ชื่อพื้นที่'), 'พื้นที่รอส่ง');
    await user.selectOptions(screen.getByLabelText('ตึก'), '1');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));
    await waitFor(() => expect(server.calls.some((call) => call.method === 'POST' && call.path === '/api/admin/areas')).toBe(true));
    const cancel = screen.getByRole('button', { name: 'ยกเลิก' });
    expect(cancel).toBeDisabled();
    await user.click(cancel);
    expect(router.state.location.pathname).toBe('/admin/areas/new');
    answer({ status: 409, body: { message: 'ยังสร้างไม่ได้' } });
    expect(await screen.findByText('ยังสร้างไม่ได้')).toBeInTheDocument();
    expect(screen.getByLabelText('ชื่อพื้นที่')).toHaveValue('พื้นที่รอส่ง');
    await user.click(screen.getByRole('link', { name: 'ยกเลิก' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas'));
  });
  it('Enter in nested Area typed confirmation never submits the parent Area form', async () => {
    const server = setup('POST /api/admin/areas/2/deactivate', async () => ({ status: 409 }));
    const { user } = await renderApp('/admin/areas/2');
    await user.click(await screen.findByRole('button', { name: 'แก้ไขพื้นที่' }));
    await user.click(await screen.findByText('การใช้งานพื้นที่และประวัติ'));
    await user.click(screen.getByRole('button', { name: 'ปิดใช้งานพื้นที่นี้' }));
    const ask = screen.getByRole('alertdialog');
    const code = within(ask).getByRole('textbox', { name: 'รหัสยืนยัน' });
    await user.type(code, 'WRONG{Enter}');
    expect(server.calls.filter((call) => call.method !== 'GET' && !call.path.includes('/auth/'))).toHaveLength(0);
    await user.clear(code);
    await user.type(code, 'AR02{Enter}');
    expect(server.calls.filter((call) => call.method !== 'GET' && !call.path.includes('/auth/'))).toHaveLength(0);
    expect(ask).toBeInTheDocument();
  });
});
