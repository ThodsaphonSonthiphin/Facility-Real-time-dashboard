import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { adminSession, managedBuildings } from '../../test/adminFixtures';
import { fakeDownload } from '../../test/fakeDownload';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/** The server's answer: the bytes, with ASP.NET's Content-Disposition. */
function xlsx(name = 'facility_2026-10-09_2026-10-15.xlsx'): FakeResponse {
  return {
    status: 200,
    file: { content: 'xlsx-bytes', type: XLSX, disposition: `attachment; filename=${name}; filename*=UTF-8''${name}` },
  };
}

function serve(routes: FakeRoutes = {}) {
  return fakeServer({
    ...adminSession,
    'GET /api/admin/buildings/all': () => ({ status: 200, body: managedBuildings }),
    'POST /api/admin/export': () => xlsx(),
    ...routes,
  });
}

const exportPosts = (server: ReturnType<typeof serve>) => server.calls.filter((call) => call.path === '/api/admin/export');
const download = () => screen.getByRole('button', { name: 'ดาวน์โหลด .xlsx' });
const fromField = () => screen.getByRole('textbox', { name: 'ตั้งแต่' });
const toField = () => screen.getByRole('textbox', { name: 'ถึง' });

describe('ExportPage (D8, facility-0053)', () => {
  it('starts on the last 7 days with attendance and submissions, and downloads the file the server sends', async () => {
    const { saved } = fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');
    const today = thaiToday();
    const weekAgo = addDays(today, -6);

    expect(fromField()).toHaveValue(formatShiftDate(weekAgo));
    expect(toField()).toHaveValue(formatShiftDate(today));
    expect(screen.getByRole('checkbox', { name: /^การลงเวลา/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /^การส่งงาน/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /^การตรวจงาน/ })).not.toBeChecked();
    expect(screen.getByText(`facility_${weekAgo}_${today}.xlsx`)).toBeInTheDocument();
    expect(screen.getByText('2 แผ่นงาน · การลงเวลา + การส่งงาน')).toBeInTheDocument();

    await user.click(download());

    expect(await screen.findByText('ดาวน์โหลดแล้ว: facility_2026-10-09_2026-10-15.xlsx')).toBeInTheDocument();
    // decision 1 of plan 5: no buildingId means every building, so it is left out
    expect(exportPosts(server).map((call) => call.body)).toEqual([
      { from: weekAgo, to: today, sets: ['Attendance', 'Submissions'] },
    ]);
    expect(saved).toHaveLength(1);
    expect(saved[0].fileName).toBe('facility_2026-10-09_2026-10-15.xlsx');
    expect(await saved[0].blob.text()).toBe('xlsx-bytes');
  });

  it('sends the typed range, the building and the sets in the file’s order', async () => {
    fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');

    await user.click(screen.getByRole('checkbox', { name: /^การตรวจงาน/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.clear(fromField());
    await user.type(fromField(), '01/10/2026');
    await user.clear(toField());
    await user.type(toField(), '07/10/2026');
    await screen.findByRole('option', { name: 'B' });
    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');
    await user.click(download());

    await screen.findByText(/^ดาวน์โหลดแล้ว/);
    expect(exportPosts(server)[0].body).toEqual({
      from: '2026-10-01',
      to: '2026-10-07',
      buildingId: 2,
      sets: ['Attendance', 'Submissions', 'Inspections'],
    });
  });

  it('refuses before sending when no set is chosen or the range is wrong or too long', async () => {
    const { saved } = fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export');

    await user.click(screen.getByRole('checkbox', { name: /^การลงเวลา/ }));
    await user.click(screen.getByRole('checkbox', { name: /^การส่งงาน/ }));
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('ต้องเลือกข้อมูลอย่างน้อย 1 ชุด');

    await user.click(screen.getByRole('checkbox', { name: /^การส่งงาน/ }));
    await user.clear(fromField());
    await user.type(fromField(), '2026-10-01');
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(screen.getByText('กรอกช่วงวันให้ถูกต้องก่อน')).toBeInTheDocument();

    await user.clear(fromField());
    await user.type(fromField(), '01/09/2026');
    await user.clear(toField());
    await user.type(toField(), '02/10/2026');
    await user.click(download());
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกได้ไม่เกิน 31 วัน');

    expect(exportPosts(server)).toHaveLength(0);
    expect(saved).toHaveLength(0);
  });

  it("shows the server's refusal, and saves nothing", async () => {
    const { saved } = fakeDownload();
    serve({ 'POST /api/admin/export': () => ({ status: 400, body: { message: 'ไม่พบตึกนี้' } }) });
    const { user } = await renderApp('/admin/export?building=9');

    await user.click(download());

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบตึกนี้');
    expect(saved).toHaveLength(0);
  });

  it('sends once at a time: the button waits while the file is made', async () => {
    const { saved } = fakeDownload();
    let answer: (response: FakeResponse) => void = () => undefined;
    const server = serve({
      'POST /api/admin/export': () =>
        new Promise<FakeResponse>((resolve) => {
          answer = resolve;
        }),
    });
    const { user } = await renderApp('/admin/export');

    await user.click(download());
    const busy = await screen.findByRole('button', { name: 'กำลังสร้างไฟล์...' });
    expect(busy).toBeDisabled();
    await user.click(busy);
    expect(exportPosts(server)).toHaveLength(1);

    await act(async () => answer(xlsx()));

    expect(await screen.findByText(/^ดาวน์โหลดแล้ว/)).toBeInTheDocument();
    expect(saved).toHaveLength(1);
  });

  it('an expired access token is refreshed, and the file is saved once (facility-0023)', async () => {
    const { saved } = fakeDownload();
    let posts = 0;
    const server = serve({
      'POST /api/admin/export': () => {
        posts += 1;
        return posts === 1 ? { status: 401 } : xlsx();
      },
    });
    const { user } = await renderApp('/admin/export');

    await user.click(download());

    expect(await screen.findByText(/^ดาวน์โหลดแล้ว/)).toBeInTheDocument();
    expect(exportPosts(server)).toHaveLength(2);
    expect(server.calls.filter((call) => call.path === '/api/auth/refresh')).toHaveLength(2);
    expect(saved).toHaveLength(1);
  });

  it('opens with the range and building of the link that brought the Admin here', async () => {
    fakeDownload();
    const server = serve();
    const { user } = await renderApp('/admin/export?from=2026-10-15&to=2026-10-15&building=2');

    expect(fromField()).toHaveValue('15/10/2026');
    expect(toField()).toHaveValue('15/10/2026');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('B'));
    await user.click(download());

    await screen.findByText(/^ดาวน์โหลดแล้ว/);
    expect(exportPosts(server)[0].body).toEqual({
      from: '2026-10-15',
      to: '2026-10-15',
      buildingId: 2,
      sets: ['Attendance', 'Submissions'],
    });
  });

  it('the form follows the address when the menu link is clicked on the open page', async () => {
    serve();
    const { user } = await renderApp('/admin/export?from=2026-10-15&to=2026-10-15&building=2');
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('B'));

    await user.click(within(screen.getByRole('navigation', { name: 'เมนูแอดมิน' })).getByRole('link', { name: 'ส่งออก Excel' }));

    expect(fromField()).toHaveValue(formatShiftDate(addDays(thaiToday(), -6)));
    expect(toField()).toHaveValue(formatShiftDate(thaiToday()));
    expect(screen.getByRole('combobox', { name: 'ตึก' })).toHaveDisplayValue('ทุกตึก');
  });

  it('is in the Admin menu as ส่งออก Excel, and links back to the Dashboard', async () => {
    serve();
    await renderApp('/admin/export');
    const menu = screen.getByRole('navigation', { name: 'เมนูแอดมิน' });
    expect(within(menu).getByRole('link', { name: 'ส่งออก Excel' })).toHaveAttribute('aria-current', 'page');
    expect(within(screen.getByRole('main')).getByRole('link', { name: '‹ กลับไปหน้าสถานะจุด' })).toHaveAttribute(
      'href',
      '/dashboard',
    );
  });

  it('the building picker lists every building, an off one marked (ปิดใช้งาน) (facility-0090)', async () => {
    serve();
    await renderApp('/admin/export');

    const picker = await screen.findByRole('combobox', { name: 'ตึก' });
    await waitFor(() =>
      expect(within(picker).getAllByRole('option').map((option) => option.textContent)).toEqual([
        'ทุกตึก',
        'A',
        'B',
        'C (ปิดใช้งาน)',
      ]),
    );
  });
});
