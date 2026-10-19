import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminPoint,
  adminSession,
  areaDetail,
  areaSummary,
  officeDetail,
  officeSummary,
  unlocatedSign,
} from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
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

describe('PointEditor (D4, facility-0045, 0047)', () => {
  it('refuses to save while times are typed but not added, and sends nothing', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });

    await user.type(within(day).getByLabelText('เริ่ม'), '08:00');
    await user.type(within(day).getByLabelText('ถึง'), '10:00');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent(
      'มีเวลาที่พิมพ์ไว้แต่ยังไม่ได้กด "เพิ่ม" กด "เพิ่ม" หรือลบเวลานั้นก่อนบันทึก',
    );
    expect(server.calls.some((call) => call.path.startsWith('/api/admin') && call.method !== 'GET')).toBe(false);
  });

  it('edits a point’s name and windows and sends the full list in shift order', async () => {
    let detailCalls = 0;
    const server = serve({
      'GET /api/admin/areas/1': () => {
        detailCalls += 1;
        return { status: 200, body: areaDetail() };
      },
      'PUT /api/admin/points/11': ({ body }) => ({
        status: 200,
        body: adminPoint({ name: (body as { name: string }).name }),
      }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    expect(within(editor).getByLabelText('ชื่อจุด')).toHaveValue('ห้องน้ำชาย');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    const night = within(editor).getByRole('group', { name: 'กะดึก' });
    expect(within(day).getByText('07:00–09:00')).toBeInTheDocument();
    expect(within(night).getByText('03:00–05:00')).toBeInTheDocument();

    await user.click(within(day).getByRole('button', { name: 'ลบช่วง 16:00–18:00' }));
    await user.type(within(night).getByLabelText('เริ่ม'), '23:00');
    await user.type(within(night).getByLabelText('ถึง'), '01:00');
    await user.click(within(night).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(night).getByText('23:00–01:00')).toBeInTheDocument();
    await user.clear(within(editor).getByLabelText('ชื่อจุด'));
    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'ห้องน้ำชาย ชั้น 1');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await screen.findByText('บันทึกจุด ห้องน้ำชาย ชั้น 1 แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      name: 'ห้องน้ำชาย ชั้น 1',
      roundWindows: [
        { shift: 'Day', start: '07:00', end: '09:00' },
        { shift: 'Night', start: '20:00', end: '22:00' },
        { shift: 'Night', start: '23:00', end: '01:00' },
        { shift: 'Night', start: '03:00', end: '05:00' },
      ],
    });
    // The save invalidated AdminSetup, so the open Area was asked again
    await waitFor(() => expect(detailCalls).toBe(2));
  });

  it('adds a point to a day-only Area: no night shift, and a name is required', async () => {
    let created = false;
    const newPoint = adminPoint({
      id: 31,
      name: 'โถงทางเดิน',
      sign: unlocatedSign({ id: 301, code: 'AR02-01' }),
      roundWindows: [{ id: 9, shift: 'Day', start: '08:00', end: '10:00' }],
    });
    const server = serve({
      'GET /api/admin/areas/2': () => ({ status: 200, body: officeDetail(created ? { points: [newPoint] } : {}) }),
      'POST /api/admin/areas/2/points': () => {
        created = true;
        return { status: 201, body: newPoint };
      },
    });
    const { user } = await renderApp('/admin/areas/2');
    await user.click(await screen.findByRole('button', { name: '+ เพิ่มจุดทำความสะอาดใน Area นี้' }));

    const editor = screen.getByRole('region', { name: 'เพิ่มจุด · AR02 Office ทดลอง' });
    expect(within(editor).queryByRole('group', { name: 'กะดึก' })).not.toBeInTheDocument();
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));
    expect(within(editor).getByRole('alert')).toHaveTextContent('ใส่ชื่อจุดก่อน');
    expect(server.calls.some((call) => call.path === '/api/admin/areas/2/points')).toBe(false);

    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'โถงทางเดิน');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '08:00');
    await user.type(within(day).getByLabelText('ถึง'), '10:00{Enter}');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await screen.findByText('บันทึกจุด โถงทางเดิน แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/areas/2/points')?.body).toEqual({
      name: 'โถงทางเดิน',
      roundWindows: [{ shift: 'Day', start: '08:00', end: '10:00' }],
    });
    // The new point is now edited in place, and its row is in the table
    expect(await screen.findByRole('region', { name: 'แก้ไขจุด · AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.getByRole('row', { name: /AR02-01/ })).toBeInTheDocument();
  });

  it('checks the time format before adding a window, and refuses the same window twice', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const day = screen.getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '7:00');
    await user.type(within(day).getByLabelText('ถึง'), '09:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('เวลาต้องเขียนแบบ 07:00');

    await user.clear(within(day).getByLabelText('เริ่ม'));
    await user.type(within(day).getByLabelText('เริ่ม'), '07:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('มีช่วงนี้แล้ว');
  });

  it('shows the server’s refusal and keeps the draft', async () => {
    serve({
      'PUT /api/admin/points/11': () => ({ status: 400, body: { message: 'ช่วง 18:00–20:00 ต้องอยู่ในกะเช้า' } }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01 Area ทดลอง' });
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await user.type(within(day).getByLabelText('เริ่ม'), '18:00');
    await user.type(within(day).getByLabelText('ถึง'), '20:00');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent('ช่วง 18:00–20:00 ต้องอยู่ในกะเช้า');
    expect(within(day).getByText('18:00–20:00')).toBeInTheDocument();
  });

  it('deactivates a point and can bring it back (no delete, facility-0021)', async () => {
    let active = true;
    const detail = () => {
      const answer = areaDetail();
      answer.points[1] = { ...answer.points[1], isActive: active };
      return answer;
    };
    const server = serve({
      'GET /api/admin/areas/1': () => ({ status: 200, body: detail() }),
      'POST /api/admin/points/12/deactivate': () => {
        active = false;
        return { status: 200, body: detail().points[1] };
      },
      'POST /api/admin/points/12/activate': () => {
        active = true;
        return { status: 200, body: detail().points[1] };
      },
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-02' }));

    await user.click(screen.getByRole('button', { name: 'ปิดใช้งานจุดนี้' }));
    expect(await screen.findByText('ปิดใช้งานจุด ห้องน้ำหญิง แล้ว')).toBeInTheDocument();
    expect(await screen.findByText('จุดนี้ปิดใช้งานอยู่ ป้ายสแกนไม่ได้')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'เปิดใช้งานจุดนี้' }));
    expect(await screen.findByText('เปิดใช้งานจุด ห้องน้ำหญิง แล้ว')).toBeInTheDocument();
    expect(
      server.calls.filter((call) => call.path.startsWith('/api/admin/points/12/')).map((call) => call.path),
    ).toEqual(['/api/admin/points/12/deactivate', '/api/admin/points/12/activate']);
  });

  it('sends a new point once: the save is disabled while it is in flight', async () => {
    let answer: (value: { status: number; body: unknown }) => void = () => {};
    const newPoint = adminPoint({
      id: 31,
      name: 'โถงทางเดิน',
      sign: unlocatedSign({ id: 301, code: 'AR02-01' }),
      roundWindows: [{ id: 9, shift: 'Day', start: '08:00', end: '10:00' }],
    });
    const server = serve({
      'POST /api/admin/areas/2/points': () =>
        new Promise((resolve) => {
          answer = resolve;
        }),
    });
    const { user } = await renderApp('/admin/areas/2');
    await user.click(await screen.findByRole('button', { name: '+ เพิ่มจุดทำความสะอาดใน Area นี้' }));

    const editor = screen.getByRole('region', { name: 'เพิ่มจุด · AR02 Office ทดลอง' });
    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'โถงทางเดิน');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    const saving = await within(editor).findByRole('button', { name: 'กำลังบันทึก...' });
    expect(saving).toBeDisabled();
    expect(within(editor).getByLabelText('ชื่อจุด')).toBeDisabled();
    const pointPosts = () => server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/areas/2/points');
    await user.click(saving);
    expect(pointPosts()).toHaveLength(1);

    answer({ status: 201, body: newPoint });
    expect(await screen.findByText('บันทึกจุด โถงทางเดิน แล้ว')).toBeInTheDocument();
    expect(pointPosts()).toHaveLength(1);
  });
});
