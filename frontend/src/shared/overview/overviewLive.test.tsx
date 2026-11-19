import { setupListeners } from '@reduxjs/toolkit/query';
import { act, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import { StrictMode, type PropsWithChildren } from 'react';
import { Provider } from 'react-redux';
import { expect, it, vi } from 'vitest';
import { useCreateAreaMutation } from '../../pages/areas/areaFormApi';
import { useCreatePointMutation } from '../../pages/areas/areasApi';
import { useCorrectAttendanceMutation } from '../../pages/attendance/attendanceApi';
import { useCreateBuildingMutation } from '../../pages/buildings/buildingsApi';
import { makeStore, type AppStore } from '../../store';
import { adminSession } from '../../test/adminFixtures';
import { point } from '../../test/cleanerFixtures';
import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { overviewDetailFixture, overviewFixture } from '../../test/overviewFixtures';
import { renderApp } from '../../test/renderApp';
import { freshOnOpen } from '../api/baseApi';
import { startSession } from '../auth/session';
import { useAssignCoverMutation, useCancelCoverMutation } from '../cover/coverApi';
import { overviewApi, useGetOverviewAreasQuery } from './overviewApi';
import type { OverviewAreaList, OverviewContext, OverviewListArgs, OverviewSummary } from './types';

const LIST = '/api/admin/overview/areas';
const DETAIL = `${LIST}/91`;
const listUrl = '/admin/overview?date=2026-11-04&shift=Day';
const detailUrl = '/dashboard?source=overview&date=2026-11-04&shift=Day&group=incomplete&areaId=91&view=cleaning';
const listArgs: OverviewListArgs = { shiftDate: '2026-11-04', shift: 'Day', group: 'notEntered', page: 1, pageSize: 5 };
const outdated = /ไม่สามารถอัปเดตข้อมูลล่าสุดได้.*ข้อมูลอาจยังไม่ล่าสุด/;

function serve(routes: FakeRoutes) {
  return fakeServer({ ...adminSession, 'GET /api/admin/alerts': () => ({ status: 200, body: [] }), ...routes });
}

function listResponse(context: Partial<OverviewContext> = {}): OverviewAreaList {
  const body = overviewFixture();
  return { ...body, summary: { ...body.summary, context: { ...body.summary.context, ...context } } };
}

function projection(revision: number): OverviewAreaList {
  const result = listResponse({ readAtUtc: `2026-11-04T03:0${revision}:00Z`, targetRevisionId: revision === 4 ? 16 : 12 });
  const count = (value: number) => ({ ...result.total, value, observed: value });
  const moveToEntered = <T extends Pick<OverviewSummary, 'entry' | 'groups'>>(subject: T): T => ({
    ...subject,
    entry: { ...subject.entry, numerator: count(subject.groups.entered.observed + revision) },
    groups: {
      ...subject.groups,
      entered: count(subject.groups.entered.observed + revision),
      covered: count(subject.groups.covered.observed - Math.min(3, revision)),
      unassigned: count(subject.groups.unassigned.observed - Math.max(0, revision - 3)),
      notEntered: count(subject.groups.notEntered.observed - revision),
    },
  });
  const buildingBRows = Array.from({ length: 5 }, (_, index) => {
    const areaId = 96 + index;
    const row = result.rows[index < 2 ? 0 : 4];
    return { ...row, areaId, code: `AR${areaId}`, name: `พื้นที่ทดลอง ${areaId}`,
      buildingId: 2, buildingCode: 'B', buildingName: 'ตึกทดลอง B' };
  });
  const [buildingA, buildingB] = result.summary.buildings;
  const remaining = [...result.rows, ...buildingBRows].slice(revision);
  return {
    ...result,
    total: count(remaining.length),
    rows: remaining.slice(0, result.pageSize),
    summary: { ...moveToEntered(result.summary), buildings: [moveToEntered(buildingA), buildingB] },
  };
}

async function listSettled(store: AppStore) {
  await waitFor(() => expect(overviewApi.endpoints.getOverviewAreas.select(listArgs)(store.getState()).status).toBe('fulfilled'));
}

function areaLinks() {
  return within(screen.getByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' })).getAllByRole('link', { name: /ดูพื้นที่/ });
}

async function advance(ms: number) {
  await act(async () => { await vi.advanceTimersByTimeAsync(ms); });
}

it('rereads the complete aggregate after scan, inspection, attendance and target events', async () => {
  let revision = 0;
  let paused: Promise<FakeResponse> | undefined;
  const { calls } = serve({
    [`GET ${LIST}`]: () => paused ?? { status: 200, body: projection(revision) },
  });
  const { store } = await renderApp(listUrl);
  await waitFor(() => expect(calls.filter((call) => call.path === LIST)).toHaveLength(2));
  await listSettled(store);
  expect(areaLinks()).toHaveLength(5);
  expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' })).toBeVisible();

  const events = [
    ['ScanRecorded', point({ id: 9101, status: 'Passed' })],
    ['InspectionRecorded', point({ id: 9101, status: 'PendingInspection' })],
    ['AttendanceRecorded', { areaId: 93, shiftDate: listArgs.shiftDate, shift: 'Day' }],
    ['OverviewChanged', { revisionId: 16 }],
  ] as const;

  for (const [event, payload] of events) {
    revision += 1;
    const before = calls.filter((call) => call.path === LIST).length;
    let complete!: (answer: FakeResponse) => void;
    paused = new Promise((resolve) => { complete = resolve; });
    act(() => hub.latest().push(event, payload));
    await waitFor(() => expect(calls.filter((call) => call.path === LIST)).toHaveLength(before + 1));

    // Even a conflicting legacy PointCard cannot change either group tallies or Area rows.
    expect(screen.getByRole('button', { name: `ยังไม่ลงเวลา ${11 - revision} พื้นที่` })).toBeVisible();
    expect(areaLinks()[0]).toHaveAccessibleName(`ดูพื้นที่ AR${90 + revision} พื้นที่ทดลอง ${90 + revision}`);
    await act(async () => {
      paused = undefined;
      complete({ status: 200, body: projection(revision) });
    });
    expect(await screen.findByRole('button', { name: `ยังไม่ลงเวลา ${10 - revision} พื้นที่` })).toBeVisible();
    expect(areaLinks()).toHaveLength(5);
    expect(screen.getByText(`แสดง 1–5 จาก ${10 - revision} พื้นที่`)).toBeVisible();
    expect(areaLinks()[0]).toHaveAccessibleName(`ดูพื้นที่ AR${91 + revision} พื้นที่ทดลอง ${91 + revision}`);
  }

  expect(hub.connections).toHaveLength(1);
  expect(hub.latest().serverTimeoutInMilliseconds).toBe(10_000);
  expect(calls.some((call) => call.path === '/api/admin/overview')).toBe(false);
});

it('ignores a transfer-instruction schedule event and rereads after the confirmed arrival attendance event', async () => {
  let arrived = false;
  const { calls } = serve({
    [`GET ${LIST}`]: () => ({ status: 200, body: projection(arrived ? 1 : 0) }),
  });
  const { store } = await renderApp(listUrl);
  const requests = () => calls.filter((call) => call.path === LIST);
  await waitFor(() => expect(requests()).toHaveLength(2));
  await listSettled(store);

  await act(async () => {
    hub.latest().push('ScheduleChanged', { commandId: 'instruction' });
    await new Promise((resolve) => setTimeout(resolve, 30));
  });
  expect(requests()).toHaveLength(2);
  expect(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' })).toBeVisible();

  arrived = true;
  act(() => hub.latest().push('AttendanceRecorded', { areaId: 91, shiftDate: listArgs.shiftDate, shift: 'Day' }));
  expect(await screen.findByRole('button', { name: 'ลงเวลาแล้ว 91 พื้นที่' })).toBeVisible();
  expect(requests()).toHaveLength(3);
});

it('holds one connection across StrictMode and cached remount, then stops it when the cache expires', async () => {
  const { calls } = serve({ [`GET ${LIST}`]: () => ({ status: 200, body: overviewFixture() }) });
  const store = makeStore();
  await act(() => startSession(store.dispatch));

  function Probe() {
    useGetOverviewAreasQuery(listArgs, freshOnOpen);
    return null;
  }
  const view = <StrictMode><Provider store={store}><Probe /></Provider></StrictMode>;
  const first = render(view);
  const requests = () => calls.filter((call) => call.path === LIST);
  await waitFor(() => expect(requests()).toHaveLength(2));
  await listSettled(store);
  expect(hub.connections).toHaveLength(1);
  const connection = hub.latest();
  expect(connection.starts).toBe(1);

  vi.useFakeTimers();
  try {
    first.unmount();
    await advance(59_000);
    expect(connection.stops).toBe(0);

    const second = render(view);
    await advance(0);
    expect(requests()).toHaveLength(3);
    expect(hub.connections).toHaveLength(1);
    expect(connection.starts).toBe(1);
    second.unmount();
    await advance(60_001);

    expect(connection.stops).toBe(1);
    expect(overviewApi.endpoints.getOverviewAreas.select(listArgs)(store.getState()).isUninitialized).toBe(true);
  } finally {
    vi.useRealTimers();
  }
});

it.each([
  { label: 'list', url: listUrl, path: LIST },
  { label: 'detail', url: detailUrl, path: DETAIL },
])('retains the last $label snapshot until a reconnect recovery read succeeds', async ({ url, path }) => {
  let fail = false;
  let response = listResponse();
  let detail = overviewDetailFixture();
  let retrying = false;
  let finish!: (body: FakeResponse) => void;
  const delayed = new Promise<FakeResponse>((resolve) => { finish = resolve; });
  const { calls } = serve({
    [`GET ${path}`]: () => retrying ? delayed : (fail
      ? { status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง' } }
      : { status: 200, body: path === LIST ? response : detail }),
  });
  const { user } = await renderApp(url);
  await waitFor(() => expect(calls.filter((call) => call.path === path)).toHaveLength(2));
  const connection = hub.latest();
  const retained = () => path === LIST ? areaLinks()[0] : screen.getByRole('heading', { name: /AR91 พื้นที่ทดลอง 91/ });
  expect(retained()).toBeVisible();

  act(() => connection.reconnecting());
  expect(screen.getByText(/กำลังเชื่อมต่อใหม่ ข้อมูลอาจยังไม่ล่าสุด/)).toBeVisible();
  fail = true;
  act(() => connection.reconnected());
  const warning = await screen.findByText(outdated);
  expect(warning).toHaveTextContent('10:00');
  expect(retained()).toBeVisible();
  expect(screen.queryByText(/กำลังเชื่อมต่อใหม่/)).not.toBeInTheDocument();

  retrying = true;
  await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
  expect(warning).toBeVisible();
  expect(retained()).toBeVisible();
  response = projection(1);
  detail = { ...detail, area: { ...detail.area, name: 'พื้นที่หลังอัปเดต' } };
  await act(async () => finish({ status: 200, body: path === LIST ? response : detail }));
  await waitFor(() => expect(screen.queryByText(outdated)).not.toBeInTheDocument());
  if (path === LIST) {
    expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 9 พื้นที่' })).toBeVisible();
  } else {
    expect(screen.getByRole('heading', { name: 'AR91 พื้นที่หลังอัปเดต' })).toBeVisible();
  }
});

it('polls before the slot, on a server-defined receiving window and through the inclusive close boundary', async () => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  let body = listResponse({ phase: 'notStarted' });
  let fail = false;
  const { calls } = serve({
    [`GET ${LIST}`]: () => fail
      ? { status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง' } }
      : { status: 200, body },
  });
  const requests = () => calls.filter((call) => call.path === LIST);
  const closeUtc = '2026-11-04T13:00:00Z';
  let stopListening: (() => void) | undefined;
  try {
    const { store } = await renderApp(listUrl);
    stopListening = setupListeners(store.dispatch);
    await waitFor(() => expect(requests()).toHaveLength(2));
    await listSettled(store);

    body = listResponse({ phase: 'running' });
    await advance(29_000);
    expect(requests()).toHaveLength(2);
    await advance(1_001);
    expect(requests()).toHaveLength(3);
    expect(screen.getByText(/กำลังทำงาน/)).toBeVisible();

    body = listResponse({ phase: 'receiving', readAtUtc: '2026-11-04T12:59:30Z', closeUtc });
    await act(async () => { window.dispatchEvent(new Event('focus')); });
    expect(requests()).toHaveLength(4);
    expect(await screen.findByText(/ยังเปิดรับข้อมูลถึง 20:00/)).toBeVisible();

    fail = true;
    await advance(30_001);
    expect(requests()).toHaveLength(5);
    expect(await screen.findByText(outdated)).toHaveTextContent('19:59');

    fail = false;
    body = listResponse({ phase: 'receiving', readAtUtc: closeUtc, evaluationUtc: closeUtc, closeUtc });
    await advance(30_001);
    expect(requests()).toHaveLength(6);
    await listSettled(store);
    expect(screen.queryByText(outdated)).not.toBeInTheDocument();
    expect(await screen.findByText(/ยังเปิดรับข้อมูลถึง 20:00/)).toBeVisible();

    body = listResponse({ phase: 'closed', isClockCurrent: false, readAtUtc: '2026-11-04T13:00:30Z', evaluationUtc: closeUtc, closeUtc });
    await advance(30_001);
    expect(requests()).toHaveLength(7);
    expect(await screen.findByText(/· ปิดรับข้อมูลแล้ว \(/)).toBeVisible();
    await advance(90_000);
    await act(async () => { window.dispatchEvent(new Event('focus')); });
    expect(requests()).toHaveLength(7);
  } finally {
    stopListening?.();
    vi.useRealTimers();
  }
});

it('keeps closed Area detail fresh on reopen and attendance corrections without polling or focus reads', async () => {
  const data = overviewDetailFixture();
  let changed = false;
  const { calls } = serve({
    [`GET ${DETAIL}`]: () => ({ status: 200, body: {
      ...data,
      area: changed ? { ...data.area, name: 'พื้นที่หลังแก้เวลา' } : data.area,
      context: { ...data.context, phase: 'closed', isClockCurrent: false },
    } }),
    'GET /api/admin/buildings/all': () => ({ status: 200, body: [] }),
  });
  const requests = () => calls.filter((call) => call.path === DETAIL);
  vi.useFakeTimers({ shouldAdvanceTime: true });
  let stopListening: (() => void) | undefined;
  try {
    const { router, store } = await renderApp(detailUrl);
    stopListening = setupListeners(store.dispatch);
    await waitFor(() => expect(requests()).toHaveLength(2));
    expect(screen.getByText('ปิดรับข้อมูลของกะนี้แล้ว')).toBeVisible();
    act(() => hub.latest().reconnecting());
    expect(screen.queryByText(/กำลังเชื่อมต่อใหม่/)).not.toBeInTheDocument();
    await advance(30_001);
    await act(async () => { window.dispatchEvent(new Event('focus')); });
    expect(requests()).toHaveLength(2);

    await act(() => router.navigate('/admin/export'));
    await act(() => router.navigate(detailUrl));
    await waitFor(() => expect(requests()).toHaveLength(3));
    await screen.findByRole('heading', { name: /AR91 พื้นที่ทดลอง 91/ });
    changed = true;
    act(() => hub.latest().push('AttendanceRecorded', { areaId: 91, shiftDate: listArgs.shiftDate, shift: 'Day' }));
    expect(await screen.findByRole('heading', { name: 'AR91 พื้นที่หลังแก้เวลา' })).toBeVisible();
    expect(requests()).toHaveLength(4);
  } finally {
    stopListening?.();
    vi.useRealTimers();
  }
});

it('keeps the new selection visible when an event response for the previous slot arrives late', async () => {
  let holdPrevious = false;
  let resolvePrevious!: (body: FakeResponse) => void;
  const paused = new Promise<FakeResponse>((resolve) => { resolvePrevious = resolve; });
  const { calls } = serve({
    [`GET ${LIST}`]: (request) => {
      if (new URLSearchParams(request.search).get('shift') === 'Day') {
        return holdPrevious ? paused : { status: 200, body: listResponse() };
      }
      const night = listResponse({ shift: 'Night' });
      return { status: 200, body: { ...night, rows: [{ ...night.rows[0], name: 'พื้นที่กะดึก' }] } };
    },
  });
  const { store, router } = await renderApp(listUrl);
  await waitFor(() => expect(calls.filter((call) => call.path === LIST)).toHaveLength(2));
  await listSettled(store);

  holdPrevious = true;
  act(() => hub.latest().push('OverviewChanged', { revisionId: 13 }));
  await waitFor(() => expect(calls.filter((call) => call.path === LIST)).toHaveLength(3));
  await act(() => router.navigate(listUrl.replace('Day', 'Night')));

  expect(await screen.findByRole('link', { name: 'ดูพื้นที่ AR91 พื้นที่กะดึก' })).toBeVisible();
  await act(async () => resolvePrevious({ status: 200, body: projection(1) }));

  expect(screen.getByRole('link', { name: 'ดูพื้นที่ AR91 พื้นที่กะดึก' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' })).toBeVisible();
  expect(router.state.location.search).toContain('shift=Night');
});

it('invalidates locally after successful setup, corrections and covers when no push arrives', async () => {
  let saveSucceeds = false;
  const { calls } = serve({
    [`GET ${LIST}`]: () => ({ status: 200, body: overviewFixture() }),
    'POST /api/admin/buildings': () => saveSucceeds ? { status: 201, body: {} } : { status: 400, body: {} },
    'POST /api/admin/areas': () => ({ status: 201, body: {} }),
    'POST /api/admin/areas/91/points': () => ({ status: 201, body: {} }),
    'PUT /api/admin/attendance': () => ({ status: 200, body: {} }),
    'POST /api/admin/cover-assignments': () => ({ status: 201, body: {} }),
    'DELETE /api/admin/cover-assignments/1': () => ({ status: 204 }),
  });
  const { store } = await renderApp(listUrl);
  const requests = () => calls.filter((call) => call.path === LIST);
  await waitFor(() => expect(requests()).toHaveLength(2));
  await listSettled(store);
  const wrapper = ({ children }: PropsWithChildren) => <Provider store={store}>{children}</Provider>;
  const { result } = renderHook(() => {
    const [building] = useCreateBuildingMutation();
    const [area] = useCreateAreaMutation();
    const [point] = useCreatePointMutation();
    const [correct] = useCorrectAttendanceMutation();
    const [cover] = useAssignCoverMutation();
    const [cancel] = useCancelCoverMutation();
    const slot = { shiftDate: '2026-11-04', shift: 'Day' as const };
    return {
      building: () => building({ code: 'A', name: 'อาคารทดลอง' }),
      area: () => area({ code: 'AR91', name: 'พื้นที่ทดลอง', buildingId: 1, shiftPattern: 'DayOnly', dayCleanerId: null, nightCleanerId: null }),
      point: () => point({ areaId: 91, body: { name: 'จุดทดลอง', roundWindows: [] } }),
      correct: () => correct({ userId: 1, eventType: 'ShiftIn', time: '08:00', reason: 'บันทึกเวลา', ...slot }),
      cover: () => cover({ userId: 1, areaId: 91, ...slot }),
      cancel: () => cancel(1),
    };
  }, { wrapper });

  await act(async () => { await result.current.building(); });
  expect(requests()).toHaveLength(2);
  saveSucceeds = true;
  for (const save of Object.values(result.current)) {
    const before = requests().length;
    await act(async () => { await save().unwrap(); });
    await waitFor(() => expect(requests()).toHaveLength(before + 1));
    await listSettled(store);
  }
});
