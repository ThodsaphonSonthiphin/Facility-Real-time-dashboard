import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';
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

/** Types one Round Window into a shift's two time fields (facility-0083); an empty string leaves that field empty. */
async function typeWindow(user: UserEvent, shift: HTMLElement, from: string, to: string) {
  await user.click(within(shift).getByRole('button', { name: 'เพิ่มรอบ' }));
  const rows = within(shift).getAllByRole('group', { name: /รอบ/ });
  const row = rows[rows.length - 1];
  const n = rows.length;
  for (const [name, text] of [
    [`เริ่มรอบ ${n}`, from],
    [`สิ้นสุดรอบ ${n}`, to],
  ] as const) {
    const box = within(row).getByLabelText(name);
    await user.clear(box);
    if (text !== '') {
      await user.type(box, text);
    }
  }
}

describe('PointEditor (D4, facility-0045, 0047)', () => {
  it('edits an existing numbered round and preserves its draft after a server refusal', async () => {
    const server = serve({
      'PUT /api/admin/points/11': () => ({ status: 400, body: { message: 'รอบซ้อนกัน' } }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    const row = screen.getByRole('group', { name: 'กะเช้า รอบ 1' });
    expect(within(row).getByText('2 ชม.')).toBeInTheDocument();
    await user.clear(within(row).getByLabelText('เริ่มรอบ 1'));
    await user.type(within(row).getByLabelText('เริ่มรอบ 1'), '0830');
    await user.click(screen.getByRole('button', { name: 'บันทึกจุด' }));
    expect(await screen.findByText('รอบซ้อนกัน')).toBeInTheDocument();
    expect(within(row).getByLabelText('เริ่มรอบ 1')).toHaveValue('08:30');
    expect(within(row).getByText('30 นาที')).toBeInTheDocument();
    expect((server.calls.find((call) => call.method === 'PUT')?.body as { roundWindows: unknown[] }).roundWindows).toContainEqual({ shift: 'Day', start: '08:30', end: '09:00' });
    await user.clear(within(row).getByLabelText('เริ่มรอบ 1'));
    await user.type(within(row).getByLabelText('เริ่มรอบ 1'), '24');
    await user.click(screen.getByRole('button', { name: 'บันทึกจุด' }));
    expect(server.calls.filter((call) => call.method === 'PUT')).toHaveLength(1);
    expect(within(row).getByLabelText('เริ่มรอบ 1')).toHaveValue('24');
  });

  it('opens QR management from its table row and retains the safe confirmation without sending on cancel', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/1');
    const opener = await screen.findByRole('button', { name: 'จัดการ QR AR01-01' });
    await user.click(opener);
    await user.click(await screen.findByText('พิกัดและ QR ของป้าย'));
    const editor = screen.getByRole('dialog', { name: 'แก้ไขจุด · AR01-01' });
    await user.click(within(editor).getByRole('button', { name: 'สร้าง QR ใหม่' }));
    const ask = within(editor).getByRole('alertdialog', { name: 'สร้าง QR ใหม่ของป้าย AR01-01 ห้องน้ำชาย?' });
    expect(within(ask).getByRole('heading')).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(editor).toBeInTheDocument();
    expect(within(editor).getByRole('button', { name: 'สร้าง QR ใหม่' })).toHaveFocus();
    await user.click(within(editor).getByRole('button', { name: 'สร้าง QR ใหม่' }));
    fireEvent(screen.getByRole('alertdialog'), new Event('cancel', { bubbles: true, cancelable: true }));
    expect(editor).toBeInTheDocument();
    expect(within(editor).getByRole('button', { name: 'สร้าง QR ใหม่' })).toHaveFocus();
    expect(server.calls.some((call) => call.path.endsWith('/regenerate-token'))).toBe(false);
    await user.click(within(editor).getByRole('button', { name: 'ปิด' }));
    expect(opener).toHaveFocus();
  });

  it('refuses to save an incomplete added round and sends nothing', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01-01' });
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });

    await typeWindow(user, day, '0800', '');
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent(
      'ใส่เวลารอบให้ครบเป็น HH:mm (ชั่วโมง 0–23 นาที 00–59)',
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

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01-01' });
    expect(within(editor).getByLabelText('ชื่อจุด')).toHaveValue('ห้องน้ำชาย');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    const night = within(editor).getByRole('group', { name: 'กะดึก' });
    expect(within(day).getByText('07:00–09:00')).toBeInTheDocument();
    expect(within(night).getByText('03:00–05:00')).toBeInTheDocument();

    await user.click(within(day).getByRole('button', { name: 'ลบรอบ 2 กะเช้า' }));
    await typeWindow(user, night, '2300', '0100');
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
    await user.click(await screen.findByRole('button', { name: 'เพิ่มจุด' }));

    const editor = screen.getByRole('region', { name: 'เพิ่มจุด' });
    expect(within(editor).queryByRole('group', { name: 'กะดึก' })).not.toBeInTheDocument();
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));
    expect(within(editor).getByRole('alert')).toHaveTextContent('ใส่ชื่อจุดก่อน');
    expect(server.calls.some((call) => call.path === '/api/admin/areas/2/points')).toBe(false);

    await user.type(within(editor).getByLabelText('ชื่อจุด'), 'โถงทางเดิน');
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await typeWindow(user, day, '0800', '1000');
    await user.keyboard('{Enter}'); // Enter edits a round without submitting the point
    await user.click(within(editor).getByRole('button', { name: 'บันทึกจุด' }));

    expect(await screen.findByText('บันทึกจุด โถงทางเดิน แล้ว')).toBeInTheDocument();
    expect(server.calls.find((call) => call.path === '/api/admin/areas/2/points')?.body).toEqual({
      name: 'โถงทางเดิน',
      roundWindows: [{ shift: 'Day', start: '08:00', end: '10:00' }],
    });
    // The save closes the editor, and the new point's row is in the table
    expect(screen.queryByRole('region', { name: 'เพิ่มจุด' })).not.toBeInTheDocument();
    expect(await screen.findByRole('row', { name: /AR02-01/ })).toBeInTheDocument();
  });

  it('keeps numeric editing and Backspace, and Enter does not submit an incomplete round', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    const day = screen.getByRole('group', { name: 'กะเช้า' });
    await typeWindow(user, day, '1030', '');
    const start = within(day).getByLabelText('เริ่มรอบ 3');
    expect(start).toHaveValue('10:30');
    await user.type(start, '{Backspace}{Enter}');
    expect(start).toHaveValue('10:3');
    expect(server.calls.some((call) => call.method === 'PUT')).toBe(false);
    await user.click(screen.getByRole('button', { name: 'บันทึกจุด' }));
    expect(screen.getByRole('alert')).toHaveTextContent('ใส่เวลารอบให้ครบ');
  });

  it('shows the server’s refusal and keeps the draft', async () => {
    serve({
      'PUT /api/admin/points/11': () => ({ status: 400, body: { message: 'ช่วง 18:00–20:00 ต้องอยู่ในกะเช้า' } }),
    });
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));

    const editor = screen.getByRole('region', { name: 'แก้ไขจุด · AR01-01' });
    const day = within(editor).getByRole('group', { name: 'กะเช้า' });
    await typeWindow(user, day, '1800', '2000');
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

    const editor = screen.getByRole('dialog', { name: 'แก้ไขจุด · AR01-02' });
    await user.click(within(editor).getByRole('button', { name: 'ปิดใช้งานจุดนี้' }));
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'ปิดใช้งานจุด' }));
    expect(await screen.findByText('ปิดใช้งานจุด ห้องน้ำหญิง แล้ว')).toBeInTheDocument();

    // The save closed the editor; the row stays in the table, marked, and opening it again offers the way back
    expect(editor).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'แก้ไข AR01-02' }));
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
    await user.click(await screen.findByRole('button', { name: 'เพิ่มจุด' }));

    const editor = screen.getByRole('region', { name: 'เพิ่มจุด' });
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
