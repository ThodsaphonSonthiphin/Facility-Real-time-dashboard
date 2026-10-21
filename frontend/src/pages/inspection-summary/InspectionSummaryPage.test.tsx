import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { summary } from '../../test/reportFixtures';
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
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
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

    expect(await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({});
    expect(screen.getAllByRole('rowheader').map((cell) => cell.textContent)).toEqual([
      'หัวหน้าทดสอบ660001',
      'หัวหน้ากะดึก660002',
      'หัวหน้าตึกบี660003',
    ]);
    expect(cellsOf('หัวหน้าทดสอบ')).toEqual(['A · กะเช้า', '12 จาก 12 จุด', '101', '168', '96', '57%', '72', '11', '3']);
    expect(cellsOf('หัวหน้ากะดึก')).toEqual([
      'A · กะดึก',
      '9 จาก 12 จุด · ไม่ได้ตรวจ 3 จุด',
      '41',
      '150',
      '40',
      '27%',
      '110',
      '2',
      '1',
    ]);
    expect(cellsOf('หัวหน้าตึกบี')).toEqual(['B · กะเช้า', '0 จาก 10 จุด · ไม่ได้ตรวจ 10 จุด', '0', '0', '0', '–', '0', '0', '0']);
    expect(screen.getByRole('progressbar', { name: 'ตรวจ 57% ของงานที่ส่ง' })).toHaveAttribute('value', '57');
    // facility-0059: no link to a sign page
    expect(within(screen.getByRole('table')).queryAllByRole('link')).toHaveLength(0);
  });

  it('the fields start on the 7 dates ending today, and the shortcuts fill both', async () => {
    serve();
    const { user } = await renderApp('/admin/inspection-summary');
    const today = thaiToday();

    expect(fromField()).toHaveValue(formatShiftDate(addDays(today, -6)));
    expect(toField()).toHaveValue(formatShiftDate(today));

    await user.click(screen.getByRole('button', { name: 'เดือนนี้' }));
    expect(fromField()).toHaveValue(formatShiftDate(`${today.slice(0, 8)}01`));
    expect(toField()).toHaveValue(formatShiftDate(today));

    await user.click(screen.getByRole('button', { name: 'วันนี้' }));
    expect(fromField()).toHaveValue(formatShiftDate(today));
  });

  it('shows a typed range; the address keeps it with the building', async () => {
    const server = serve();
    const { user, router } = await renderApp('/admin/inspection-summary');
    await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');

    await user.clear(fromField());
    await user.type(fromField(), '01/10/2026');
    await user.clear(toField());
    await user.type(toField(), '07/10/2026{Enter}');

    expect(await screen.findByText('Admin เท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    await screen.findByRole('option', { name: 'B' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');

    await waitFor(() => expect(router.state.location.search).toBe('?from=2026-10-01&to=2026-10-07&building=2'));
    await waitFor(() =>
      expect(query(summaryGets(server).at(-1)!.search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '2' }),
    );
  });

  it('a wrong, backwards or too long range is refused before asking', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/inspection-summary');
    await screen.findByText('Admin เท่านั้น · 7 วันล่าสุด (09/10/2026–15/10/2026)');
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

    expect(await screen.findByText('Admin เท่านั้น · ช่วงที่เลือก (01/10/2026–07/10/2026)')).toBeInTheDocument();
    expect(query(summaryGets(server)[0].search)).toEqual({ from: '2026-10-01', to: '2026-10-07', buildingId: '1' });
    expect(fromField()).toHaveValue('01/10/2026');
    expect(toField()).toHaveValue('07/10/2026');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('A'));
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

  it('is in the Admin menu as สรุปการตรวจ', async () => {
    serve();
    await renderApp('/admin/inspection-summary');
    const menu = screen.getByRole('navigation', { name: 'เมนู Admin' });
    expect(within(menu).getByRole('link', { name: 'สรุปการตรวจ' })).toHaveAttribute('aria-current', 'page');
  });
});
