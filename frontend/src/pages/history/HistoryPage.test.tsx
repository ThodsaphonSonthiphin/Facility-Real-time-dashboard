import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { thaiToday } from '../../shared/utils/thaiTime';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
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
  within(screen.getByRole('table', { name: 'ประวัติการแก้ไขของแอดมิน' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell'));

/** Time, actor, type, what was changed, and the reason (the last cell). */
const mainCells = () => rows().map((cells) => [cells[0], cells[1], cells[2], cells[cells.length - 1]].map((cell) => cell.textContent));

const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('HistoryPage (D9, facility-0057)', () => {
  it('keeps the current rows while next loads and ignores its late completion after a filter change', async () => {
    let finish!: (answer: FakeResponse) => void;
    const server = serve({
      'GET /api/admin/audit-log': (request) => query(request.search).beforeId
        ? new Promise<FakeResponse>((resolve) => { finish = resolve; })
        : { status: 200, body: firstPage },
    });
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    await waitFor(() => expect(logGets(server)).toHaveLength(2));
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(rows()).toHaveLength(5);
    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'Cover');
    await waitFor(() => expect(query(logGets(server).at(-1)!.search).category).toBe('Cover'));
    await screen.findByText('แสดง 1–5 รายการ');
    await act(async () => { finish({ status: 200, body: olderPage }); });
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ก่อนหน้า' })).toBeDisabled();
    expect(rows()).toHaveLength(5);
  });
  it('retains the visible page on a failed next fetch, then retries the same cursor', async () => {
    let olderAttempts = 0;
    const server = serve({
      'GET /api/admin/audit-log': (request) => {
        if (!query(request.search).beforeId) return { status: 200, body: firstPage };
        return ++olderAttempts === 1 ? { status: 500, body: { message: 'โหลดหน้าเก่าไม่สำเร็จ' } } : { status: 200, body: olderPage };
      },
    });
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('โหลดหน้าเก่าไม่สำเร็จ');
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(rows()).toHaveLength(5);
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    expect(await screen.findByText('แสดง 6–6 รายการ')).toBeInTheDocument();
    expect(logGets(server).slice(1).map((request) => query(request.search).beforeId)).toEqual(['46', '46']);
  });

  it('resets the visible cursor page on filters and browser Back, including cached queries', async () => {
    serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    await screen.findByText('แสดง 6–6 รายการ');
    await user.selectOptions(screen.getByRole('combobox', { name: 'ประเภท' }), 'Cover');
    expect(await screen.findByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ก่อนหน้า' })).toBeDisabled();
    await router.navigate(-1);
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ประเภท' })).toHaveValue(''));
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(rows()).toHaveLength(5);
  });

  it('applies 30/7-day presets immediately and keeps custom dates available', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.selectOptions(screen.getByRole('combobox', { name: 'ช่วงวัน' }), '30');
    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toEqual({ from: addDays(thaiToday(), -29), to: thaiToday() }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'ช่วงวัน' }), 'week');
    await waitFor(() => expect(router.state.location.search).toBe(''));
    await user.selectOptions(screen.getByRole('combobox', { name: 'ช่วงวัน' }), 'custom');
    expect(fromField()).toBeInTheDocument();
    expect(toField()).toBeInTheDocument();
  });

  it('renders structured arrow-containing names as safe text in a five-column table', async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 200, body: { entries: [{ ...firstPage.entries[0], action: 'POINT_UPDATE', before: { Name: 'A → B' }, after: { Name: '<b>C → D</b>' } }] } }) });
    await renderApp('/admin/history');
    await screen.findByText('A → B');
    expect(screen.getByText('<b>C → D</b>')).toBeInTheDocument();
    expect(screen.getAllByRole('columnheader')).toHaveLength(5);
    expect(within(rows()[0][3]).getByRole('listitem')).toHaveTextContent('ชื่อ: A → B → <b>C → D</b>');
  });
  it('lists the last 7 days newest first: when, who, what kind, what changed, and why', async () => {
    const server = serve();
    await renderApp('/admin/history');

    await screen.findByRole('table', { name: 'ประวัติการแก้ไขของแอดมิน' });
    expect(query(logGets(server)[0].search)).toEqual({});
    expect(mainCells()).toEqual([
      ['15/10 10:42', 'ผู้ดูแลทดสอบ', 'เพิ่มเวลาลงงานเพิ่มเวลาเข้างานของ 650001 แม่บ้านทดสอบ AR01 กะเช้า 15/10/2026', 'มือถือแบตหมด หัวหน้ายืนยันว่ามาทำงาน'],
      ['15/10 09:12', 'ผู้ดูแลคนที่สอง', 'มอบหมายทำแทนมอบหมาย แม่บ้านที่อื่น ทำแทน AR01 กะเช้า 15/10/2026', '–'],
      ['15/10 09:05', 'ผู้ดูแลทดสอบ', 'ปิดเตือนปิดเตือนลงเวลาไกลจากป้ายของ แม่บ้านทดสอบ เวลา 06:52', 'GPS ในอาคารเพี้ยน ยืนอยู่หน้าป้ายจริง'],
      ['14/10 11:20', 'ผู้ดูแลทดสอบ', 'แก้เบอร์โทรเปลี่ยนเบอร์โทรของ 650001 แม่บ้านทดสอบ', '–'],
      ['14/10 08:15', 'ผู้ดูแลทดสอบ', 'ส่งออก Excelส่งออก Excel การลงเวลา + การส่งงาน วันที่ 2026-10-01 ถึง 2026-10-05 ทุกตึก', '–'],
    ]);
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'ช่วงวัน' })).toHaveValue('week');
    expect(
      screen.getByText(
        'ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก',
      ),
    ).toBeInTheDocument();
  });

  it('shows one cursor page, then navigates previous/next from cache without another request', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    expect(screen.getByRole('button', { name: 'ก่อนหน้า' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    expect(await screen.findByText('แสดง 6–6 รายการ')).toBeInTheDocument();
    expect(mainCells()).toEqual([['13/10 14:40', 'ผู้ดูแลทดสอบ', 'สร้าง QR ใหม่ออก QR ใหม่ของป้าย AR01-03 ป้ายเดิมสแกนไม่ได้แล้ว', '–']]);
    expect(query(logGets(server)[1].search)).toEqual({ beforeId: '46' });
    expect(screen.getByRole('button', { name: 'ถัดไป' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'ก่อนหน้า' }));
    expect(await screen.findByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    expect(await screen.findByText('แสดง 6–6 รายการ')).toBeInTheDocument();
    expect(logGets(server)).toHaveLength(2);
  });

  it('an empty terminal cursor retains the last real page and disables next', async () => {
    const server = serve({
      'GET /api/admin/audit-log': (request) => ({ status: 200, body: query(request.search).beforeId === '46' ? { entries: [] } : firstPage }),
    });
    const { user } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    await waitFor(() => expect(logGets(server)).toHaveLength(2));
    await waitFor(() => expect(screen.getByRole('button', { name: 'ถัดไป' })).toBeDisabled());
    expect(screen.getByText('แสดง 1–5 รายการ')).toBeInTheDocument();
    expect(rows()).toHaveLength(5);
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
    ).toEqual(['ทุกประเภท', 'แก้การลงเวลา', 'มอบหมายทำแทน', 'ปิดเตือน', 'ตึก · พื้นที่ · จุด · รอบ · ป้าย', 'สร้าง QR ใหม่', 'บัญชี', 'ส่งออก Excel']);
  });

  it('a typed range is checked before asking (92 days at most), then kept in the address', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');

    await user.selectOptions(screen.getByRole('combobox', { name: 'ช่วงวัน' }), 'custom');
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

    await screen.findByText('แสดง 1–5 รายการ');
    expect(query(logGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', actorId: '7', category: 'Export' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(screen.getByRole('combobox', { name: 'ประเภท' })).toHaveDisplayValue('ส่งออก Excel');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ผู้ทำ' })).toHaveDisplayValue('ผู้ดูแลทดสอบ'));
  });

  it('"แสดง" on the range already in the address asks again, and adds no history entry', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แสดง 1–5 รายการ');
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
    expect(screen.getByText('แสดง 0 รายการ')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ถัดไป' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'ก่อนหน้า' })).toBeDisabled();
  });

  it('is in the Admin menu as ประวัติการแก้ไข', async () => {
    serve();
    await renderApp('/admin/history');
    const menu = await screen.findByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getByRole('link', { name: 'ประวัติการแก้ไข' })).toHaveAttribute('aria-current', 'page');
  });

  it('the typed range follows the address when the menu link opens the page again', async () => {
    serve();
    const { user } = await renderApp('/admin/history?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แสดง 1–5 รายการ');
    expect(fromField()).toHaveValue('01/10/2026');

    await user.click(within(screen.getByRole('navigation', { name: 'เมนูแอดมิน' })).getByRole('link', { name: 'ประวัติการแก้ไข' }));

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ช่วงวัน' })).toHaveValue('week'));
    expect(screen.queryByRole('textbox', { name: 'ตั้งแต่' })).not.toBeInTheDocument();
  });

  it('"ก่อน → หลัง" lists each change, and never a phone number (facility-0057)', async () => {
    serve();
    await renderApp('/admin/history');
    await screen.findByText('แสดง 1–5 รายการ');

    expect(screen.getByRole('columnheader', { name: 'ก่อน → หลัง' })).toBeInTheDocument();
    const changes = rows().map((cells) => cells[3]);
    expect(within(changes[0]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'รายการ: – → เข้างาน',
      'เวลา: – → 15/10 06:58',
      'ที่มา: – → แอดมินเพิ่ม',
    ]);
    expect(within(changes[2]).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'คุยแล้ว: ไม่ใช่ → ใช่',
      'ประเภทเตือน: – → ลงเวลาไกลจากป้าย',
    ]);
    expect(changes[3]).toHaveTextContent('เบอร์เดิม → เบอร์ใหม่ (ไม่แสดงเบอร์)');
  });

  it('search asks the server for every row in the range with แสดง or Enter, and keeps the words in the address', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/history?category=Cover');
    await screen.findByText('แสดง 1–5 รายการ');
    await user.click(screen.getByText('ค้นหาประวัติ'));
    const search = screen.getByRole('searchbox', { name: 'ค้นหา' });

    await user.type(search, 'แม่บ้านที่อื่น');
    expect(logGets(server)).toHaveLength(1); // typing alone asks nothing
    await user.keyboard('{Enter}');

    await waitFor(() => expect(query(logGets(server).at(-1)!.search)).toMatchObject({ category: 'Cover', q: 'แม่บ้านที่อื่น' }));
    expect(query(router.state.location.search)).toMatchObject({ category: 'Cover', q: 'แม่บ้านที่อื่น' });
    expect(await screen.findByText('แสดง 1–5 รายการ ที่ตรงกับ "แม่บ้านที่อื่น"')).toBeInTheDocument();

    await user.clear(search);
    await user.click(screen.getByRole('button', { name: 'ค้นหา' }));
    await waitFor(() => expect(query(router.state.location.search).q).toBeUndefined());
    expect(query(logGets(server).at(-1)!.search).q).toBeUndefined();
  });

  it('a search that finds nothing says so, and the search box follows the address', async () => {
    serve({ 'GET /api/admin/audit-log': () => ({ status: 200, body: { entries: [] } }) });
    const { router, user } = await renderApp('/admin/history?q=ไม่มีคำนี้');

    expect(await screen.findByText('ไม่พบรายการที่ตรงกับที่ค้นหาในช่วงนี้')).toBeInTheDocument();
    expect(screen.getByRole('searchbox', { name: 'ค้นหา' })).toHaveValue('ไม่มีคำนี้');

    await router.navigate('/admin/history');
    await user.click(screen.getByText('ค้นหาประวัติ'));
    await waitFor(() => expect(screen.getByRole('searchbox', { name: 'ค้นหา' })).toHaveValue(''));
  });
});
