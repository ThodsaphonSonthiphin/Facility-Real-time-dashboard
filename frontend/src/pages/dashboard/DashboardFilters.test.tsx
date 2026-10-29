import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession, buildings } from '../../test/adminFixtures';
import { fakeServer } from '../../test/fakeServer';
import { boardPoints } from '../../test/liveFixtures';
import { renderApp } from '../../test/renderApp';
import { sessionExpired } from '../../store/slices/authSlice';

function serve() {
  return fakeServer({
    ...adminSession,
    'GET /api/service-points': () => ({ status: 200, body: boardPoints }),
    'GET /api/admin/buildings': () => ({ status: 200, body: buildings }),
    'POST /api/auth/logout': () => ({ status: 204 }),
  });
}

const tileNames = () =>
  within(screen.getByRole('main'))
    .queryAllByRole('heading', { level: 3 })
    .map((h) => h.textContent);
const legend = () => within(screen.getByRole('list', { name: 'สถานะ' }));
/** Turns "เรียงพื้นที่ที่มีปัญหาขึ้นก่อน" off, so every Area shows its cards (on, the Areas with nothing wrong fold into a line). */
const showEveryArea = (user: { click: (element: HTMLElement) => Promise<void> }) =>
  user.click(screen.getByRole('checkbox', { name: 'เรียงพื้นที่ที่มีปัญหาขึ้นก่อน' }));
const startState = { buildingCode: '', status: 'all', issueOnly: false, search: '', problemFirst: true };

describe('Dashboard filters (D1, facility-0024)', () => {
  it('the legend counts each status; choosing one shows only those cards, choosing it again shows all', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    expect(
      legend()
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).toEqual(['ยังไม่ถึงรอบ 1', 'ยังไม่ทำ 0', 'รอตรวจ 2', 'ตรวจผ่าน 1', 'ต้องแก้ไข 1', 'เลยเวลา 1', 'นอกเวลา 1']);

    await user.click(legend().getByRole('button', { name: 'รอตรวจ 2' }));

    expect(tileNames()).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);
    expect(legend().getByRole('button', { name: 'รอตรวจ 2' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(legend().getByRole('button', { name: 'ต้องแก้ไข 1' }));
    expect(tileNames()).toEqual(['จุดทิ้งขยะ']);

    await user.click(legend().getByRole('button', { name: 'ต้องแก้ไข 1' }));
    await showEveryArea(user);
    expect(tileNames()).toHaveLength(7);
  });

  it('the building picker narrows the cards and the counts', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('option', { name: 'B · ตึกข้างเคียง' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'ตึก' }), 'B');
    await showEveryArea(user);

    expect(screen.queryByRole('region', { name: 'AR01 Area ทดลอง' })).not.toBeInTheDocument();
    expect(tileNames()).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
    expect(legend().getByRole('button', { name: 'รอตรวจ 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'มีปัญหาที่แจ้ง 0' })).toBeInTheDocument();
  });

  it('มีปัญหาที่แจ้ง keeps the cards with an issue the Cleaner reported (facility-0046)', async () => {
    serve();
    const { user } = await renderApp('/dashboard');

    await user.click(await screen.findByRole('button', { name: 'มีปัญหาที่แจ้ง 1' }));

    expect(tileNames()).toEqual(['ห้องน้ำหญิง']);
    expect(screen.getByRole('button', { name: 'มีปัญหาที่แจ้ง 1' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('a filter that leaves nothing says so', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    await user.click(legend().getByRole('button', { name: 'ยังไม่ทำ 0' }));

    expect(screen.getByText('ไม่มีจุดตามตัวกรองนี้')).toBeInTheDocument();
  });

  it('the filters are still set after another page, and cleared by logout (facility-0024)', async () => {
    serve();
    const { user, store } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    await user.click(legend().getByRole('button', { name: 'รอตรวจ 2' }));

    await user.click(screen.getByRole('link', { name: 'บัญชีผู้ใช้' }));
    await screen.findByRole('heading', { level: 1, name: 'บัญชีผู้ใช้' });
    await user.click(screen.getByRole('link', { name: 'สถานะจุด' }));
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    expect(tileNames()).toEqual(['ห้องน้ำชาย', 'ห้องน้ำ Office']);

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));
    await waitFor(() => expect(store.getState().dashboard).toEqual(startState));
  });

  it('an expired session also clears the filters (facility-0024)', async () => {
    serve();
    const { user, store } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    await user.click(legend().getByRole('button', { name: 'รอตรวจ 2' }));
    expect(store.getState().dashboard.status).toBe('PendingInspection');

    act(() => {
      store.dispatch(sessionExpired());
    });

    expect(store.getState().dashboard).toEqual(startState);
  });

  it('with the sort on, the Areas with nothing wrong fold into one line each, and "ดูจุด" opens one', async () => {
    serve();
    const { user } = await renderApp('/dashboard');

    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    expect(screen.queryByRole('region', { name: 'AR02 Office ทดลอง' })).not.toBeInTheDocument();
    const quiet = screen.getByRole('region', { name: 'พื้นที่ที่ไม่มีปัญหา' });
    expect(within(quiet).getByText('AR02 Office ทดลอง')).toBeInTheDocument();
    expect(within(quiet).getByText(/^ตึก B .*3 จุด$/)).toBeInTheDocument();
    expect(within(quiet).getByText('ยังไม่ถึงรอบ 1 · รอตรวจ 1 · นอกเวลา 1')).toBeInTheDocument();
    expect(tileNames()).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง', 'จุดทิ้งขยะ', 'โถงทางเดิน']);

    await user.click(within(quiet).getByRole('button', { name: 'ดูจุด' }));

    expect(tileNames()).toHaveLength(7);
    expect(within(quiet).getByRole('button', { name: 'ซ่อนจุด' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('the search narrows the Areas and the points, and a search shows every Area it found as cards', async () => {
    serve();
    const { user } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });

    await user.type(screen.getByRole('searchbox', { name: 'ค้นหา' }), 'office');

    expect(await screen.findByRole('region', { name: 'AR02 Office ทดลอง' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'AR01 Area ทดลอง' })).not.toBeInTheDocument();
    expect(tileNames()).toEqual(['ห้องทำงาน', 'ห้องน้ำ Office', 'ห้องเก็บของ']);
  });

  it('"ล้างตัวกรอง" brings back every card when a filter leaves nothing', async () => {
    serve();
    const { user, store } = await renderApp('/dashboard');
    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    await user.click(legend().getByRole('button', { name: 'ยังไม่ทำ 0' }));

    await user.click(screen.getByRole('button', { name: 'ล้างตัวกรอง' }));

    expect(store.getState().dashboard).toEqual(startState);
    expect(await screen.findByRole('region', { name: 'AR01 Area ทดลอง' })).toBeInTheDocument();
  });
});
