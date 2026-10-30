import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession, managedBuildings } from '../../test/adminFixtures';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { summary, summaryRow } from '../../test/reportFixtures';
import { renderApp } from '../../test/renderApp';

const query = (search: string) => Object.fromEntries(new URLSearchParams(search));

/** The server answers the range it was asked for, or its default. */
function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/inspection-summary': (request) => {
      const asked = query(request.search);
      return { status: 200, body: { ...summary, from: asked.from ?? summary.from, to: asked.to ?? summary.to } };
    },
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    ...routes,
  });
}

const summaryGets = (server: ReturnType<typeof serve>) =>
  server.calls.filter((call) => call.path === '/api/admin/inspection-summary');

/** The cells after the Supervisor's name. */
function cellsOf(name: string): (string | null)[] {
  const row = screen
    .getAllByRole('row')
    .find((candidate) => within(candidate).queryByRole('rowheader')?.textContent?.startsWith(name));
  if (!row) {
    throw new Error(`no row for ${name}`);
  }
  return within(row).getAllByRole('cell').map((cell) => cell.textContent);
}

const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('InspectionSummaryPage (D7, facility-0048 rule 5)', () => {
  it('shows each Supervisor of the last 7 days as the server counts them, judging nothing', async () => {
    const server = serve();
    await renderApp('/admin/inspection-summary');

    expect(await screen.findByText('แอดมินเท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({});
    expect(screen.getByRole('heading', { name: 'สรุปการตรวจ' })).toBeInTheDocument();
    expect(screen.getByText('ระบบไม่ให้คะแนนหรือจัดอันดับหัวหน้า')).toBeVisible();
    expect(screen.getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual([
      'หัวหน้า', 'ตึก · กะ', 'ตรวจ (ครั้ง)', 'ส่งแล้วไม่ได้ตรวจ', 'ให้แก้', 'ตรวจไกลป้าย', 'จุดที่ไม่ได้ตรวจเลย', 'รายละเอียด',
    ]);
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'หัวหน้าทดสอบ', 'หัวหน้ากะดึก', 'หัวหน้าตึกบี',
    ]);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['A · กะเช้า', '101', '72', '11', '3', '0', 'รายละเอียด']);
    expect(cellsOf('หัวหน้ากะดึก')).toEqual(['A · กะดึก', '41', '110', '2', '1', '3', 'ดูจุด']);
    expect(cellsOf('หัวหน้าตึกบี')).toEqual(['B · กะเช้า', '0', '0', '0', '0', '10', 'ดูจุด']);
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('the compact preset select applies dates and keeps numeric ranges', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/inspection-summary');
    await screen.findByText('หัวหน้ากะดึก');
    const today = thaiToday();
    const picker = screen.getByRole('combobox', { name: 'ช่วงวัน' });
    expect(picker).toHaveValue('week');
    expect(within(picker).getByRole('option', { name: `7 วันล่าสุด (${formatShiftDate(addDays(today, -6))}–${formatShiftDate(today)})` })).toBeInTheDocument();
    await user.selectOptions(picker, 'today');
    await waitFor(() => expect(router.state.location.search).toBe(`?from=${today}&to=${today}`));
    await waitFor(() => expect(query(summaryGets(server).at(-1)!.search)).toEqual({ from: today, to: today }));
    await user.selectOptions(picker, 'month');
    await waitFor(() => expect(router.state.location.search).toBe(`?from=${today.slice(0, 8)}01&to=${today}`));
    await user.selectOptions(picker, 'custom');
    expect(fromField()).toBeVisible();
    expect(toField()).toBeVisible();
    expect(fromField()).toHaveValue(formatShiftDate(`${today.slice(0, 8)}01`));
  });

  it('shows a typed range; the address keeps it with the building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/inspection-summary');
    await screen.findByText('แอดมินเท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');
    await user.click(screen.getByText('ตัวกรองเพิ่มเติม · ตึกและกำหนดช่วงวัน'));

    await user.clear(fromField());
    await user.type(fromField(), '01/10/2026');
    await user.clear(toField());
    await user.type(toField(), '07/10/2026{Enter}');

    expect(await screen.findByText('แอดมินเท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    await screen.findByRole('option', { name: 'B · ตึกข้างเคียง' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B · ตึกข้างเคียง');

    await waitFor(() => expect(router.state.location.search).toBe('?from=2026-10-01&to=2026-10-07&building=2'));
    await waitFor(() =>
      expect(query(summaryGets(server).at(-1)!.search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '2' }),
    );
  });

  it('a wrong, backwards or too long range is refused before asking', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/inspection-summary');
    await screen.findByText('แอดมินเท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');
    await user.click(screen.getByText('ตัวกรองเพิ่มเติม · ตึกและกำหนดช่วงวัน'));
    const show = screen.getByRole('button', { name: 'แสดง' });

    await user.clear(fromField());
    await user.type(fromField(), '1/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');

    await user.clear(fromField());
    await user.type(fromField(), '08/10/2026');
    await user.clear(toField());
    await user.type(toField(), '01/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('วันสุดท้ายต้องไม่ก่อนวันเริ่ม');

    await user.clear(fromField());
    await user.type(fromField(), '01/09/2026');
    await user.clear(toField());
    await user.type(toField(), '02/10/2026');
    await user.click(show);
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');
    expect(summaryGets(server)).toHaveLength(1);
  });

  it('opens the range and building in the address, and the fields show them', async () => {
    const server = serve();
    await renderApp('/admin/inspection-summary?from=2026-10-01&to=2026-10-07&building=1');

    expect(await screen.findByText('แอดมินเท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '1' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(toField()).toHaveValue('07/10/2026');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('A · ตึกทดลอง'));
  });

  it('"แสดง" on the range already in the address asks again, and adds no history entry', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/inspection-summary?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แอดมินเท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)');
    const before = summaryGets(server).length;
    const entries = window.history.length;

    await user.click(screen.getByRole('button', { name: 'แสดง' }));

    await waitFor(() => expect(summaryGets(server)).toHaveLength(before + 1));
    expect(router.state.location.search).toBe('?from=2026-10-01&to=2026-10-07');
    expect(window.history.length).toBe(entries);
  });

  it("shows the server's refusal", async () => {
    serve({ 'GET /api/admin/inspection-summary': () => ({ status: 400, body: { message: 'เลือกได้ไม่เกิน 31 วัน' } }) });
    await renderApp('/admin/inspection-summary?from=2026-01-01&to=2026-10-07');
    expect(await screen.findByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');
  });

  it('says so when no Supervisor is active in the building chosen', async () => {
    serve({ 'GET /api/admin/inspection-summary': () => ({ status: 200, body: { ...summary, rows: [] } }) });
    await renderApp('/admin/inspection-summary?building=2');
    expect(await screen.findByText('ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก')).toBeInTheDocument();
  });

  it('the fields follow the address when it changes while the page stays open', async () => {
    serve();
    const { user } = await renderApp('/admin/inspection-summary?from=2026-10-01&to=2026-10-07');
    await screen.findByText('แอดมินเท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)');

    await user.click(within(screen.getByRole('navigation', { name: 'เมนูแอดมิน' })).getByRole('link', { name: 'สรุปการตรวจ' }));

    await screen.findByText('แอดมินเท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');
    expect(fromField()).toHaveValue(formatShiftDate(addDays(thaiToday(), -6)));
    expect(toField()).toHaveValue(formatShiftDate(thaiToday()));
  });

  it('is in the Admin menu as สรุปการตรวจ', async () => {
    serve();
    await renderApp('/admin/inspection-summary');
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getByRole('link', { name: 'สรุปการตรวจ' })).toHaveAttribute('aria-current', 'page');
  });

  it('keeps Export only in the Admin menu (facility-0100)', async () => {
    serve();
    await renderApp('/admin/inspection-summary?building=1');
    await screen.findByText('แอดมินเท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');

    expect(within(screen.getByRole('main')).queryByRole('link', { name: 'ส่งออก Excel' })).not.toBeInTheDocument();
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getAllByRole('link', { name: 'ส่งออก Excel' })).toHaveLength(1);
    expect(within(menu).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('href', '/admin/export');
  });

  it('the building picker lists active buildings only, an off one is left out (facility-0090)', async () => {
    serve();
    const { user } = await renderApp('/admin/inspection-summary');
    await user.click(screen.getByText('ตัวกรองเพิ่มเติม · ตึกและกำหนดช่วงวัน'));

    const picker = await screen.findByRole('combobox', { name: 'ตึก' });
    await waitFor(() =>
      expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual(['ทุกตึก', 'A · ตึกทดลอง', 'B · ตึกข้างเคียง']),
    );
    expect(within(picker).queryByRole('option', { name: /ปิดใช้งาน/ })).not.toBeInTheDocument();
  });

  it('expands real safe point chips with Enter and Space, and preserves old aggregates in a disclosure', async () => {
    serve();
    const { user } = await renderApp('/admin/inspection-summary');
    const opener = await screen.findByRole('button', { name: 'ดูจุด หัวหน้ากะดึก' });
    expect(opener).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('region', { name: 'รายละเอียดของ หัวหน้ากะดึก' })).not.toBeInTheDocument();
    opener.focus();
    await user.keyboard('{Enter}');
    expect(opener).toHaveAttribute('aria-expanded', 'true');
    const details = screen.getByRole('region', { name: 'รายละเอียดของ หัวหน้ากะดึก' });
    expect(details).toHaveAttribute('id', opener.getAttribute('aria-controls'));
    expect(within(details).getAllByRole('listitem')).toHaveLength(3);
    expect(within(details).getByText('ห้องน้ำทดสอบ')).toBeVisible();
    expect(within(details).getByTitle('AR01-01')).toBeVisible();
    expect(within(details).queryByRole('link')).not.toBeInTheDocument();
    await user.click(within(details).getByText('ข้อมูลรวมเพิ่มเติม'));
    expect(within(details).getByText('9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด')).toBeVisible();
    expect(within(details).getByRole('progressbar', { name: 'ตรวจ 27% ของงานที่ส่ง' })).toHaveAttribute('value', '27');
    expect(within(details).getByText('A · ตึกทดลอง · กะดึก')).toBeVisible();
    opener.focus();
    await user.keyboard(' ');
    expect(opener).toHaveAttribute('aria-expanded', 'false');
    expect(opener).toHaveFocus();
    expect(screen.queryByRole('region', { name: 'รายละเอียดของ หัวหน้ากะดึก' })).not.toBeInTheDocument();
  });

  it('filters the existing returned shift values without changing any tally or making a new request', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/inspection-summary');
    await screen.findByText('หัวหน้ากะดึก');
    await user.click(screen.getByRole('button', { name: 'ดูจุด หัวหน้ากะดึก' }));
    const before = summaryGets(server).length;
    await user.selectOptions(screen.getByRole('combobox', { name: 'กะ' }), 'Day');
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['หัวหน้าทดสอบ', 'หัวหน้าตึกบี']);
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
    await user.selectOptions(screen.getByRole('combobox', { name: 'กะ' }), 'Night');
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual(['หัวหน้ากะดึก']);
    expect(cellsOf('หัวหน้ากะดึก')[1]).toBe('41');
    expect(screen.getByRole('button', { name: 'ดูจุด หัวหน้ากะดึก' })).toHaveAttribute('aria-expanded', 'false');
    expect(summaryGets(server)).toHaveLength(before);
  });

  it('keeps details and zero metrics available even with no beat points or submitted work', async () => {
    serve({ 'GET /api/admin/inspection-summary': () => ({ status: 200, body: {
      ...summary, rows: [summaryRow({ pointsTotal: 0, pointsInspected: 0, submitted: 0, submittedInspected: 0, inspections: 0, notInspected: 0, reworks: 0, gpsFlagged: 0 })],
    } }) });
    const { user } = await renderApp('/admin/inspection-summary');
    await user.click(await screen.findByRole('button', { name: 'รายละเอียด หัวหน้าทดสอบ' }));
    const details = screen.getByRole('region', { name: 'รายละเอียดของ หัวหน้าทดสอบ' });
    expect(within(details).getByText('ไม่มีจุดที่ไม่ได้ตรวจในช่วงนี้')).toBeVisible();
    await user.click(within(details).getByText('ข้อมูลรวมเพิ่มเติม'));
    expect(within(details).getByText('0 จาก 0 จุด')).toBeVisible();
    expect(within(details).getByText('–')).toBeVisible();
    await user.selectOptions(screen.getByRole('combobox', { name: 'กะ' }), 'Night');
    expect(screen.getByText('ไม่มีหัวหน้าที่เปิดใช้งานในกะที่เลือก')).toBeVisible();
  });

  it('shows loading and retries a failed summary request', async () => {
    let finish: ((answer: { status: number; body: unknown }) => void) | undefined;
    let calls = 0;
    serve({ 'GET /api/admin/inspection-summary': () => {
      calls++;
      return calls === 1 ? new Promise((resolve) => { finish = resolve; }) : { status: 200, body: summary };
    } });
    const { user } = await renderApp('/admin/inspection-summary');
    expect(screen.getByText('กำลังโหลด…')).toBeVisible();
    finish!({ status: 500, body: { message: 'โหลดสรุปไม่สำเร็จ' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('โหลดสรุปไม่สำเร็จ');
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
    expect(await screen.findByText('หัวหน้ากะดึก')).toBeVisible();
  });

  it.each(['cleaner', 'supervisor'] as const)('does not fetch or expose point details to a %s', async (role) => {
    const server = serve({ 'POST /api/auth/refresh': () => authAnswer(role) });
    await renderApp('/admin/inspection-summary');
    expect(screen.queryByRole('heading', { name: 'สรุปการตรวจ' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /ดูจุด/ })).not.toBeInTheDocument();
    expect(summaryGets(server)).toHaveLength(0);
  });

});
