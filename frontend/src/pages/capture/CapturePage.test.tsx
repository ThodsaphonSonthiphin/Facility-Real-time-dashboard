import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { adminSession, areaDetail, unlocatedSign } from '../../test/adminFixtures';
import { fakeGpsTrack } from '../../test/fakeGps';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

afterEach(() => vi.useRealTimers());

const reading = (latitude: number, longitude: number, accuracy: number) => ({ latitude, longitude, accuracy });
const atTheSign = [reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)];

function serve(routes: FakeRoutes = {}) {
  return fakeServer({ ...adminSession, 'GET /api/admin/areas/1': () => ({ status: 200, body: areaDetail() }), ...routes });
}

/**
 * Opens the page at a sign, then fakes the clock for the 30 s reading. Clicks after this use fireEvent, which needs
 * no timer.
 */
async function openAtSign(url: string, signHeading: string) {
  const app = await renderApp(url);
  await screen.findByRole('heading', { level: 2, name: signHeading });
  vi.useFakeTimers({ shouldAdvanceTime: true });
  return app;
}

const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

describe('CapturePage (M17, facility-0045 item 2)', () => {
  it('reads the GPS for 30 s at the sign and saves the median as a site location', async () => {
    const gps = fakeGpsTrack(atTheSign);
    const server = serve({
      'PUT /api/admin/signs/102/location': () => ({
        status: 200,
        body: unlocatedSign({ latitude: 13.7564, longitude: 100.5019, locationAccuracyM: 16, locationSource: 'Site' }),
      }),
    });
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    expect(screen.getByText('แอดมิน · เก็บพิกัดป้าย · AR01 Area ทดลอง')).toBeInTheDocument();
    expect(screen.getByText('รัศมี 50 ม. · ตอนนี้: ยังไม่มีพิกัด')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(18_000);
    expect(screen.getByText('กำลังอ่านตำแหน่ง 18 / 30 วินาที')).toBeInTheDocument();
    expect(screen.getByText('ยืนนิ่งตรงที่ติดป้าย · อ่านได้ 3 ครั้ง · ความแม่นยำตอนนี้ ±16 ม.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด (รอครบ 30 วินาที)' })).toBeDisabled();
    expect(within(screen.getByRole('list', { name: 'ป้ายของพื้นที่นี้' })).getByText('กำลังเก็บ')).toBeInTheDocument();

    await wait(12_000);
    expect(screen.getByText('อ่านครบแล้ว 3 ครั้ง ใน 30 วินาที')).toBeInTheDocument();
    expect(screen.getByText('ค่ากลาง: 13.756400, 100.501900')).toBeInTheDocument();
    expect(screen.getByText('ความแม่นยำ ±16 ม. · น้อยกว่ารัศมี ใช้ได้')).toBeInTheDocument();
    expect(gps.cleared).toBe(1);

    fireEvent.click(screen.getByRole('button', { name: 'บันทึกพิกัด' }));
    expect(await screen.findByText('บันทึกพิกัดป้าย AR01-02 แล้ว วัดที่ป้ายจริง ±16 ม.')).toBeInTheDocument();
    expect(server.calls.find((call) => call.method === 'PUT')).toMatchObject({
      path: '/api/admin/signs/102/location',
      body: { latitude: 13.7564, longitude: 100.5019, accuracyM: 16, source: 'Site' },
    });
  });

  it('warns when the accuracy is worse than the radius, but still lets it be saved', async () => {
    fakeGpsTrack([reading(13.7563, 100.5018, 70), reading(13.7563, 100.5018, 75), reading(13.7563, 100.5018, 62)]);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    expect(
      screen.getByText('ความแม่นยำ ±70 ม. แย่กว่ารัศมี 50 ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'เก็บใหม่' })).toBeEnabled();
  });

  it('with fewer than 3 readings it saves nothing and offers to read again', async () => {
    fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.7564, 100.5019, 16)]);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    expect(
      screen.getByText('อ่านตำแหน่งได้แค่ 2 ครั้งใน 30 วินาที ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วเก็บใหม่'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'บันทึกพิกัด' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'เก็บใหม่' })).toBeInTheDocument();
  });

  it('a refused location says how to allow it, then lets the Admin try again', async () => {
    fakeGpsTrack(['denied']);
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(1_000);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'เปิดตำแหน่งและอนุญาตให้เว็บนี้อ่านตำแหน่ง แล้วกด "ใช้ตำแหน่งตรงนี้" อีกครั้ง',
    );
    expect(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();
  });

  it('on plain http it explains that https is needed and asks for no location', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const gps = fakeGpsTrack(atTheSign);
    serve();
    await renderApp('/admin/areas/1/capture?sign=102');
    await screen.findByRole('heading', { level: 2, name: 'AR01-02 ห้องน้ำหญิง' });

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง');
    expect(gps.watches).toBe(0);
    expect(screen.queryByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).not.toBeInTheDocument();
  });

  it('lists the Area’s signs to choose from; choosing another sign or leaving stops the GPS', async () => {
    const gps = fakeGpsTrack(atTheSign);
    serve();
    const { router } = await renderApp('/admin/areas/1/capture');

    expect(await screen.findByText('ยืนตรงที่ติดป้าย แล้วเลือกป้ายนั้นจากรายการข้างล่าง')).toBeInTheDocument();
    const list = screen.getByRole('list', { name: 'ป้ายของพื้นที่นี้' });
    expect(within(list).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'AR01-IN ป้ายลงเวลาวัดที่ป้ายจริง ±14 ม.',
      'AR01-01 ห้องน้ำชายวัดที่ป้ายจริง ±14 ม.',
      'AR01-02 ห้องน้ำหญิงยังไม่มีพิกัด',
    ]);
    // M17 is a phone page: no Admin menu
    expect(screen.queryByRole('navigation', { name: 'เมนูแอดมิน' })).not.toBeInTheDocument();

    fireEvent.click(within(list).getByRole('button', { name: /^AR01-IN/ }));
    await waitFor(() => expect(router.state.location.search).toBe('?sign=100'));
    expect(await screen.findByRole('heading', { level: 2, name: 'AR01-IN ป้ายลงเวลา' })).toBeInTheDocument();

    vi.useFakeTimers({ shouldAdvanceTime: true });
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(5_000);
    // The page remounts the list with each sign, so the list is read again after a choice.
    fireEvent.click(within(screen.getByRole('list', { name: 'ป้ายของพื้นที่นี้' })).getByRole('button', { name: /^AR01-01/ }));
    expect(gps.cleared).toBe(1);
    expect(await screen.findByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(5_000);
    fireEvent.click(screen.getByRole('link', { name: '‹ กลับไปหน้าจัดการพื้นที่และป้าย' }));
    await waitFor(() => expect(gps.cleared).toBe(2));
  });

  it('while a save is in flight, neither the save nor the read-again button can be pressed', async () => {
    fakeGpsTrack(atTheSign);
    let answerSave: (answer: { status: number; body: unknown }) => void = () => undefined;
    serve({
      'PUT /api/admin/signs/102/location': () =>
        new Promise((resolve) => {
          answerSave = resolve;
        }),
    });
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    fireEvent.click(screen.getByRole('button', { name: 'บันทึกพิกัด' }));

    expect(await screen.findByRole('button', { name: 'กำลังบันทึก...' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'เก็บใหม่' })).toBeDisabled();

    answerSave({
      status: 200,
      body: unlocatedSign({ latitude: 13.7564, longitude: 100.5019, locationAccuracyM: 16, locationSource: 'Site' }),
    });
    expect(await screen.findByText('บันทึกพิกัดป้าย AR01-02 แล้ว วัดที่ป้ายจริง ±16 ม.')).toBeInTheDocument();
  });

  it('a save the server refuses shows its message, keeps the median, and can be sent again', async () => {
    fakeGpsTrack(atTheSign);
    const server = serve({
      'PUT /api/admin/signs/102/location': () => ({
        status: 400,
        body: { message: 'พิกัดที่ส่งมาไม่ถูกต้อง ลองเก็บพิกัดใหม่อีกครั้ง' },
      }),
    });
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);

    fireEvent.click(screen.getByRole('button', { name: 'บันทึกพิกัด' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('พิกัดที่ส่งมาไม่ถูกต้อง ลองเก็บพิกัดใหม่อีกครั้ง');
    expect(screen.getByText('ค่ากลาง: 13.756400, 100.501900')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: 'บันทึกพิกัด' }));
    await waitFor(() => expect(server.calls.filter((call) => call.method === 'PUT')).toHaveLength(2));
  });

  it('navigating to another sign by the URL (Back) drops the finished reading of the sign left', async () => {
    fakeGpsTrack(atTheSign);
    serve();
    const { router } = await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(30_000);
    expect(screen.getByRole('button', { name: 'บันทึกพิกัด' })).toBeEnabled();

    await act(() => router.navigate('/admin/areas/1/capture?sign=101'));

    expect(await screen.findByRole('heading', { level: 2, name: 'AR01-01 ห้องน้ำชาย' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'บันทึกพิกัด' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();
  });

  it('navigating to another sign during a reading stops the GPS', async () => {
    const gps = fakeGpsTrack(atTheSign);
    serve();
    const { router } = await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');
    fireEvent.click(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' }));
    await wait(5_000);

    await act(() => router.navigate('/admin/areas/1/capture?sign=101'));

    expect(await screen.findByRole('heading', { level: 2, name: 'AR01-01 ห้องน้ำชาย' })).toBeInTheDocument();
    expect(gps.cleared).toBe(1);
    expect(screen.getByRole('button', { name: 'ใช้ตำแหน่งตรงนี้' })).toBeInTheDocument();
  });
});
