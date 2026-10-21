import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { actors, firstPage, olderPage } from '../../test/reportFixtures';
import { renderApp } from '../../test/renderApp';

const query = (search: string) => Object.fromEntries(new URLSearchParams(search));

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/audit-log': (request) => ({
      status: 200,
      body: query(request.search).beforeId === '46' ? olderPage : firstPage,
    }),
    'GET /api/admin/audit-log/actors': () => ({ status: 200, body: actors }),
    ...routes,
  });
}

const logGets = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/audit-log');

/** Each row's cells. */
const rows = () =>
  within(screen.getByRole('table', { name: 'ประวัติของ Admin' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell'));

/** Time, actor, type, what was changed, and the reason (the last cell). */
const mainCells = () => rows().map((cells) => [...cells.slice(0, 4), cells[cells.length - 1]].map((cell) => cell.textContent));

const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('HistoryPage (D9, facility-0057)', () => {
  it('lists the last 7 days newest first: when, who, what kind, what changed, and why', async () => {
    const server = serve();
    await renderApp('/admin/history');

    await screen.findByRole('table', { name: 'ประวัติของ Admin' });
    expect(query(logGets(server)[0].search)).toEqual({});
    expect(mainCells()).toEqual([
      ['15/10 10:42', 'ผู้ดูแลทดสอบ', 'เพิ่มเวลาลงงาน', 'เพิ่มเวลาเข้างานของ 650001 แม่บ้านทดสอบ AR01 กะเช้า 15/10/2026', 'มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน'],
      ['15/10 09:12', 'ผู้ดูแลคนที่สอง', 'มอบหมายทำแทน', 'มอบหมาย แม่บ้านที่อื่น ทำแทน AR01 กะเช้า 15/10/2026', '–'],
      ['15/10 09:05', 'ผู้ดูแลทดสอบ', 'ปิดเตือน', 'ปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ เวลา 06:52', 'GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง'],
      ['14/10 11:20', 'ผู้ดูแลทดสอบ', 'แก้เบอร์โทร', 'เปลี่ยนเบอร์โทรของ 650001 แม่บ้านทดสอบ', '–'],
      ['14/10 08:15', 'ผู้ดูแลทดสอบ', 'ส่งออก Excel', 'ส่งออก Excel การลงเวลา + การส่งงาน วันที่ 2026-10-01 ถึง 2026-10-05 ทุกตึก', '–'],
    ]);
    expect(screen.getByText('แสดง 5 รายการ')).toBeInTheDocument();
    expect(fromField()).toHaveValue(formatShiftDate(addDays(thaiToday(), -6)));
    expect(toField()).toHaveValue(formatShiftDate(thaiToday()));
    expect(
      screen.getByText(
        'ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก',
      ),
    ).toBeInTheDocument();
  });

  it('"โหลดรายการเก่ากว่านี้" adds the older page, and goes once the last page is in', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.click(screen.getByRole('button', { name: 'โหลดรายการเก่ากว่านี้' }));

    expect(await screen.findByText('แสดง 6 รายการ')).toBeInTheDocument();
    expect(mainCells()[5]).toEqual(['13/10 14:40', 'ผู้ดูแลทดสอบ', 'ออก QR ใหม่', 'ออก QR ใหม่ของป้าย AR01-03 ป้ายเดิมสแกนไม่ได้แล้ว', '–']);
    expect(query(logGets(server)[1].search)).toEqual({ beforeId: '46' });
    expect(screen.queryByRole('button', { name: 'โหลดรายการเก่ากว่านี้' })).not.toBeInTheDocument();
  });

  it('an empty last page (rows exactly filled the page before) adds nothing and the button goes', async () => {
    const server = serve({
      'GET /api/admin/audit-log': (request) => ({
        status: 200,
        body: query(request.search).beforeId === '46' ? { entries: [] } : firstPage,
      }),
    });
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.click(screen.getByRole('button', { name: 'โหลดรายการเก่ากว่านี้' }));

    await waitFor(() => expect(logGets(server)).toHaveLength(2));
    await waitFor(() => expect(screen.queryByText('กำลังโหลด...')).not.toBeInTheDocument());
    await waitFor(() => expect(screen.queryByRole('button', { name: 'โหลดรายการเก่ากว่านี้' })).not.toBeInTheDocument());
    expect(screen.getByText('แสดง 5 รายการ')).toBeInTheDocument();
  });

  it('the actor and type pickers ask at once, and the address keeps them', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByRole('option', { name: 'ผู้ดูแลคนที่สอง' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'แก้การลงเวลา');
    await waitFor(() => expect(router.state.location.search).toBe('?category=Attendance'));
    await user.selectOptions(screen.getByRole('combobox', { name: 'ผู้ทำ' }), 'ผู้ดูแลคนที่สอง');

    await waitFor(() => expect(router.state.location.search).toBe('?actor=8&category=Attendance'));
    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toEqual({ actorId: '8', category: 'Attendance' }));
    expect(
      within(screen.getByRole('combobox', { name: 'ประเภท' }))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['ทุกประเภท', 'แก้การลงเวลา', 'มอบหมายทำแทน', 'ปิดเตือน', 'Area · จุด · รอบ · ป้าย', 'ออก QR ใหม่', 'บัญชี', 'ส่งออก Excel']);
  });

  it('a typed range is checked before asking (92 days at most), then kept in the address', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    await user.clear(fromField());
    await user.type(fromField(), '01/07/2026');
    await user.clear(toField());
    await user.type(toField(), '01/10/2026');
    await user.click(screen.getByRole('button', { name: 'แสดง' }));
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 92 วัน');
    expect(logGets(server)).toHaveLength(1);

    await user.clear(fromField());
    await user.type(fromField(), '02/07/2026{Enter}');

    await waitFor(() => expect(router.state.location.search).toBe('?from=2026-07-02&to=2026-10-01'));
    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toEqual({ from: '2026-07-02', to: '2026-10-01' }));
  });

  it('opens the filters in the address', async () => {
    const server = serve();
    await renderApp('/admin/history?from=2026-10-01&to=2026-10-07&actor=7&category=Export');

    await screen.findByText('แสดง 5 รายการ');
    expect(query(logGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', actorId: '7', category: 'Export' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(screen.getByRole('combobox', { name: 'ประเภท' })).toHaveDisplayValue('ส่งออก Excel');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ผู้ทำ' })).toHaveDisplayValue('ผู้ดูแลทดสอบ'));
  });

  it('"แสดง" on the range already in the address asks again, and adds no history entry', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แสดง 5 รายการ');
    const before = logGets(server).length;
    const entries = window.history.length;

    await user.click(screen.getByRole('button', { name: 'แสดง' }));

    await waitFor(() => expect(logGets(server)).toHaveLength(before + 1));
    expect(router.state.location.search).toBe('?from=2026-10-01&to=2026-10-07');
    expect(window.history.length).toBe(entries);
  });

  it("shows the server's refusal", async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 400, body: { message: 'ไม่มีประเภทนี้ในประวัติ' } }) });
    await renderApp('/admin/history');
    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่มีประเภทนี้ในประวัติ');
  });

  it('says so when nothing happened in the range', async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 200, body: { entries: [] } }) });
    await renderApp('/admin/history');
    expect(await screen.findByText('ไม่มีประวัติในช่วงนี้')).toBeInTheDocument();
  });

  it('is in the Admin menu as ประวัติ Admin', async () => {
    serve();
    await renderApp('/admin/history');
    const menu = await screen.findByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getByRole('link', { name: 'ประวัติ Admin' })).toHaveAttribute('aria-current', 'page');
  });

  it('the typed range follows the address when the menu link opens the page again', async () => {
    serve();
    const { user } = await renderApp('/admin/history?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แสดง 5 รายการ');
    expect(fromField()).toHaveValue('01/10/2026');

    await user.click(within(screen.getByRole('navigation', { name: 'เมนู Admin' })).getByRole('link', { name: 'ประวัติ Admin' }));

    await waitFor(() => expect(fromField()).toHaveValue(formatShiftDate(addDays(thaiToday(), -6))));
    expect(toField()).toHaveValue(formatShiftDate(thaiToday()));
  });

  it('"ก่อน → หลัง" lists each change, and never a phone number (facility-0057)', async () => {
    serve();
    await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');

    expect(screen.getByRole('columnheader', { name: 'ก่อน → หลัง' })).toBeInTheDocument();
    const changes = rows().map((cells) => cells[4]);
    expect(within(changes[0]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'รายการ: – → เข้างาน',
      'เวลา: – → 15/10 06:58',
      'ที่มา: – → Admin เพิ่ม',
    ]);
    expect(within(changes[2]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'คุยแล้ว: ไม่ใช่ → ใช่',
      'ประเภทเตือน: – → ลงเวลาไกลจากป้าย',
    ]);
    expect(changes[3]).toHaveTextContent('เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)');
  });

  it('search narrows the rows already loaded, and says when none match', async () => {
    serve();
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 5 รายการ');
    const search = screen.getByRole('searchbox', { name: 'ค้นหา' });

    await user.type(search, 'แม่บ้านที่อื่น');
    expect(mainCells().map((cells) => cells[2])).toEqual(['มอบหมายทำแทน']);
    expect(screen.getByText('แสดง 1 จาก 5 รายการที่โหลดแล้ว')).toBeInTheDocument();

    await user.clear(search);
    await user.type(search, 'ไม่มีคำนี้');
    expect(screen.getByText('ไม่พบรายการที่ตรงกับที่ค้นหา')).toBeInTheDocument();
    expect(screen.getByText('ค้นหาเฉพาะรายการที่โหลดแล้ว')).toBeInTheDocument();
  });
});
