import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  adminSession,
  areaDetail,
  areaSummary,
  buildings,
  cleaners,
  officeSummary,
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

const optionTexts = (select: HTMLElement) => within(select).getAllByRole('option').map((option) => option.textContent);

describe('AreaFormPage (D10, facility-0045)', () => {
  it('adds an Area with its code, building, shift pattern and the free Cleaners of each shift', async () => {
    const created = areaDetail({
      area: areaSummary({
        id: 3,
        code: 'AR03',
        name: 'Area ใหม่',
        dayCleaner: { id: 23, employeeId: '650003', displayName: 'แม่บ้านว่าง' },
        nightCleaner: undefined,
        activePointCount: 0,
      }),
      checkInSign: unlocatedSign({ id: 300, code: 'AR03-IN' }),
      points: [],
    });
    const server = serve({
      'POST /api/admin/areas': () => ({ status: 201, body: created }),
      'GET /api/admin/areas/3': () => ({ status: 200, body: created }),
    });
    const { user, router } = await renderApp('/admin/areas/new');

    expect(await screen.findByRole('heading', { level: 1, name: 'เพิ่มพื้นที่' })).toBeInTheDocument();
    const day = await screen.findByLabelText('แม่บ้านประจำกะเช้า');
    // D10: only the shift's Cleaners who have no Area yet
    expect(optionTexts(day)).toEqual(['— ยังไม่มี —', 'แม่บ้านว่าง (650003)']);
    expect(optionTexts(screen.getByLabelText('แม่บ้านประจำกะดึก'))).toEqual(['— ยังไม่มี —', 'แม่บ้านว่างกะดึก (650004)']);

    await user.type(screen.getByLabelText('รหัสพื้นที่'), 'AR03');
    await user.type(screen.getByLabelText('ชื่อพื้นที่'), 'Area ใหม่');
    await user.selectOptions(screen.getByLabelText('ตึก'), 'ตึกทดลอง');
    await user.selectOptions(day, 'แม่บ้านว่าง (650003)');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas/3'));
    expect(server.calls.find((call) => call.method === 'POST' && call.path === '/api/admin/areas')?.body).toEqual({
      code: 'AR03',
      name: 'Area ใหม่',
      buildingId: 1,
      shiftPattern: 'DayAndNight',
      dayCleanerId: 23,
      nightCleanerId: null,
    });
  });

  it('says what is missing before sending anything', async () => {
    const server = serve();
    const { user } = await renderApp('/admin/areas/new');

    await user.click(await screen.findByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('ใส่รหัสพื้นที่ก่อน');
    await user.type(screen.getByLabelText('รหัสพื้นที่'), 'AR03');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('ใส่ชื่อพื้นที่ก่อน');
    await user.type(screen.getByLabelText('ชื่อพื้นที่'), 'Area ใหม่');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));
    expect(screen.getByRole('alert')).toHaveTextContent('เลือกตึกก่อน');
    expect(server.calls.some((call) => call.method === 'POST' && call.path === '/api/admin/areas')).toBe(false);
  });

  it('edits an Area; going day-only hides the night Cleaner and warns that night rounds go', async () => {
    const server = serve({
      'PUT /api/admin/areas/1': () => ({
        status: 200,
        body: areaDetail({ area: areaSummary({ shiftPattern: 'DayOnly', nightCleaner: undefined }) }),
      }),
    });
    const { user, router } = await renderApp('/admin/areas/1/edit');

    expect(await screen.findByRole('heading', { level: 1, name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' })).toBeInTheDocument();
    expect(screen.getByText('รหัสพื้นที่ AR01 (แก้ไม่ได้ พิมพ์อยู่บนทุกป้าย)')).toBeInTheDocument();
    expect(screen.getByLabelText('ชื่อพื้นที่')).toHaveValue('Area ทดลอง');
    expect(screen.getByLabelText('ตึก')).toHaveValue('1');
    expect(screen.getByLabelText('แม่บ้านประจำกะเช้า')).toHaveValue('21');
    expect(screen.getByLabelText('แม่บ้านประจำกะดึก')).toHaveValue('22');

    await user.click(screen.getByRole('radio', { name: 'เฉพาะกะเช้า' }));
    expect(screen.queryByLabelText('แม่บ้านประจำกะดึก')).not.toBeInTheDocument();
    expect(
      screen.getByText('บันทึกแล้วเวลารอบกะดึกของทุกจุดในพื้นที่นี้จะถูกลบ และแม่บ้านกะดึกออกจากพื้นที่'),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas/1'));
    expect(server.calls.find((call) => call.method === 'PUT')?.body).toEqual({
      name: 'Area ทดลอง',
      buildingId: 1,
      shiftPattern: 'DayOnly',
      dayCleanerId: 21,
      nightCleanerId: null,
    });
  });

  it('shows the server’s refusal and stays on the form', async () => {
    serve({ 'POST /api/admin/areas': () => ({ status: 409, body: { message: 'รหัสพื้นที่นี้มีแล้ว' } }) });
    const { user, router } = await renderApp('/admin/areas/new');

    await user.type(await screen.findByLabelText('รหัสพื้นที่'), 'AR01');
    await user.type(screen.getByLabelText('ชื่อพื้นที่'), 'Area ซ้ำ');
    await user.selectOptions(screen.getByLabelText('ตึก'), 'ตึกทดลอง');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('รหัสพื้นที่นี้มีแล้ว');
    expect(router.state.location.pathname).toBe('/admin/areas/new');
  });

  it('deactivates the Area and can turn it back on (no delete)', async () => {
    let active = true;
    const server = serve({
      'GET /api/admin/areas/1': () => ({
        status: 200,
        body: areaDetail({ area: areaSummary({ isActive: active, dayCleaner: undefined, nightCleaner: undefined }) }),
      }),
      'POST /api/admin/areas/1/deactivate': () => {
        active = false;
        return {
          status: 200,
          body: areaDetail({ area: areaSummary({ isActive: false, dayCleaner: undefined, nightCleaner: undefined }) }),
        };
      },
      'POST /api/admin/areas/1/activate': () => {
        active = true;
        return {
          status: 200,
          body: areaDetail({ area: areaSummary({ isActive: true, dayCleaner: undefined, nightCleaner: undefined }) }),
        };
      },
    });
    const { user } = await renderApp('/admin/areas/1/edit');

    expect(await screen.findByText('ป้ายลงเวลา AR01-IN ระบบสร้างให้เองตอนเพิ่มพื้นที่')).toBeInTheDocument();
    expect(screen.getByText('จุดทำความสะอาด 2 จุด · เพิ่มหรือแก้ที่หน้า จัดการพื้นที่และป้าย')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ปิดใช้งานพื้นที่นี้' }));

    expect(await screen.findByText('ปิดใช้งาน AR01 แล้ว')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'เปิดใช้งานพื้นที่นี้' })).toBeInTheDocument();
    expect(screen.getByText('พื้นที่นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้')).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/admin/areas/1/deactivate')).toBe(true);

    await user.click(screen.getByRole('button', { name: 'เปิดใช้งานพื้นที่นี้' }));
    expect(await screen.findByText('เปิดใช้งาน AR01 แล้ว')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'ปิดใช้งานพื้นที่นี้' })).toBeInTheDocument();
    expect(server.calls.some((call) => call.path === '/api/admin/areas/1/activate')).toBe(true);
  });

  it('keeps the Area on while Cleaners hold it, and names them (facility-0091)', async () => {
    serve();
    await renderApp('/admin/areas/1/edit');

    const button = await screen.findByRole('button', { name: 'ปิดใช้งานพื้นที่นี้' });
    expect(button).toBeDisabled();
    expect(
      screen.getByText(
        'ยังมีแม่บ้านประจำ แม่บ้านทดสอบ, แม่บ้านกะดึก เอาแม่บ้านออกจากกะในฟอร์มด้านบน หรือย้ายไปพื้นที่อื่นที่หน้าบัญชีผู้ใช้ก่อน จึงจะปิดพื้นที่ได้',
      ),
    ).toBeInTheDocument();
  });

  it('sends a new Area once: the save is disabled while it is in flight', async () => {
    let answer: (value: { status: number; body: unknown }) => void = () => {};
    const created = areaDetail({
      area: areaSummary({ id: 3, code: 'AR03', name: 'Area ใหม่', activePointCount: 0, dayCleaner: undefined, nightCleaner: undefined }),
      checkInSign: unlocatedSign({ id: 300, code: 'AR03-IN' }),
      points: [],
    });
    const server = serve({
      'POST /api/admin/areas': () =>
        new Promise((resolve) => {
          answer = resolve;
        }),
      'GET /api/admin/areas/3': () => ({ status: 200, body: created }),
    });
    const { user, router } = await renderApp('/admin/areas/new');

    await user.type(await screen.findByLabelText('รหัสพื้นที่'), 'AR03');
    await user.type(screen.getByLabelText('ชื่อพื้นที่'), 'Area ใหม่');
    await user.selectOptions(screen.getByLabelText('ตึก'), 'ตึกทดลอง');
    await user.click(screen.getByRole('button', { name: 'บันทึก' }));

    const saving = await screen.findByRole('button', { name: 'กำลังบันทึก...' });
    expect(saving).toBeDisabled();
    await user.click(saving);
    const areaPosts = () => server.calls.filter((call) => call.method === 'POST' && call.path === '/api/admin/areas');
    expect(areaPosts()).toHaveLength(1);

    answer({ status: 201, body: created });
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/areas/3'));
    expect(areaPosts()).toHaveLength(1);
  });
});
