import { act, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ScheduleDay, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { authAnswer, fakeServer } from '../../../test/fakeServer';
import { hub } from '../../../test/fakeSignalR';
import { renderApp } from '../../../test/renderApp';

/**
 * ADR facility-0176–0186, 0218 and spec §4/§12, through the real app: which week a schedule opens on, which week it
 * returns to, which filters survive a return, and the three week states it must never confuse. Only fetch and SignalR are
 * faked (regression-net #32).
 *
 * The week is 16–22 November 2026, a Monday to Sunday; "today" is set to the 17th so the first open must land on the 16th.
 */

const session = (role: 'cleaner' | 'supervisor' | 'admin') => {
  const answer = () => authAnswer(role);
  return { 'POST /api/auth/refresh': answer };
};

/** One resolved day. A working day has an Area, a group and `available`; every other state leaves what it has out. */
function day(date: string, availability: ScheduleDay['availability'] = 'available'): ScheduleDay {
  const working = availability === 'available';
  return {
    date,
    shift: 'Day',
    area: working
      ? { value: 4, sourceId: 11, sourceKind: 'baseline', revision: '2' }
      : { sourceKind: availability === 'unavailable' ? 'unavailable' : 'unset', revision: '0' },
    breakGroup: working
      ? { value: 1, sourceId: 11, sourceKind: 'baseline', revision: '2' }
      : { sourceKind: availability === 'unavailable' ? 'unavailable' : 'unset', revision: '0' },
    isDayOff: availability === 'day-off',
    isLeave: availability === 'leave',
    isUnset: availability === 'unset',
    availability,
    problems: [],
  };
}

function person(overrides: Partial<SchedulePerson> = {}): SchedulePerson {
  return {
    userId: 7,
    displayName: 'แม่บ้านทดสอบ',
    cleanerKind: 'Regular',
    homeBuildingId: 1,
    hasWeeklyChoice: true,
    weeklyRevision: '3',
    regularBaselineRevision: '2',
    reliefBaselineRevision: '0',
    days: [
      day('2026-11-16'),
      day('2026-11-17'),
      day('2026-11-18', 'day-off'),
      day('2026-11-19'),
      day('2026-11-20', 'leave'),
      day('2026-11-21'),
      day('2026-11-22', 'unset'),
    ],
    ...overrides,
  };
}

function week(overrides: Partial<ScheduleWeek> = {}): ScheduleWeek {
  return {
    weekStart: '2026-11-16',
    revision: 'digest-1',
    readAt: '2026-11-17T02:00:00Z',
    availability: 'available',
    rows: [person()],
    relief: [],
    ...overrides,
  };
}

/** The week the read answers for: the server names the week it answered, so a request for another one is visible. */
function serveWeeks(role: 'cleaner' | 'supervisor' | 'admin', answer: () => ScheduleWeek) {
  const path = role === 'cleaner' ? '/api/schedules/me' : '/api/schedules';
  const server = fakeServer({
    ...session(role),
    [`GET ${path}`]: () => ({ status: 200, body: answer() }),
  });
  return { server, reads: () => server.calls.filter((call) => call.path === path) };
}

/** Pin the clock so "this week" is decidable: Tuesday 17 November 2026, 09:00 Thai (02:00Z). */
function pinClock(iso = '2026-11-17T02:00:00Z') {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date(iso));
}

afterEach(() => {
  vi.useRealTimers();
});

describe('opening a schedule for the first time (facility-0180, 0182, 0183)', () => {
  it("opens the Cleaner's own week on this week, without being asked", async () => {
    pinClock();
    const { reads } = serveWeeks('cleaner', () => week());
    await renderApp('/my-schedule');

    await screen.findByRole('heading', { name: 'ตารางของฉัน' });
    expect(reads()[0].search).toContain('weekStart=2026-11-16');
    expect(await screen.findByText('16/11–22/11 2026')).toBeInTheDocument();
  });

  it('keeps the week an ongoing Sunday-start night shift began in, once Monday has begun (facility-0183)', async () => {
    // Monday 23 November 02:00 Thai is Sunday the 22nd 19:00Z: the night shift that started on the 22nd is still running
    pinClock('2026-11-22T19:00:00Z');
    const { reads } = serveWeeks('supervisor', () => week());
    await renderApp('/schedule-management');

    await screen.findByRole('heading', { level: 1, name: 'จัดตารางงาน' });
    expect(screen.getByRole('link', { name: 'ตึกของฉัน' })).toHaveAttribute('href', '/my-building');
    expect(screen.getByText('หัวหน้าทดสอบ')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /ตึกของฉัน/ })).toHaveAttribute('href', '/my-building');
    // It is the week of the 16th, the one the shift's start date belongs to — not the calendar week that just began
    await waitFor(() => expect(reads()[0].search).toContain('weekStart=2026-11-16'));
  });
});

describe('returning to a schedule during the same ongoing use (facility-0181, 0185, 0218)', () => {
  it('restores the week and the filter the person just looked at', async () => {
    pinClock();
    const { reads } = serveWeeks('admin', () => week());
    const { user, router } = await renderApp('/admin/schedules');
    await screen.findByRole('heading', { level: 1, name: 'ตารางงาน' });

    await user.click(screen.getByRole('button', { name: 'สัปดาห์ถัดไป' }));
    await waitFor(() => expect(reads().at(-1)?.search).toContain('weekStart=2026-11-23'));

    // Another page, then back: the week just viewed, not this week again
    await act(() => router.navigate('/dashboard'));
    await act(() => router.navigate('/admin/schedules'));

    await waitFor(() => expect(reads().at(-1)?.search).toContain('weekStart=2026-11-23'));
    expect(await screen.findByText('23/11–29/11 2026')).toBeInTheDocument();
  });

  it('a fresh page load starts from this week again: nothing is saved anywhere (facility-0218)', async () => {
    pinClock();
    const { reads } = serveWeeks('admin', () => week());
    const first = await renderApp('/admin/schedules');
    await screen.findByRole('heading', { level: 1, name: 'ตารางงาน' });
    await first.user.click(screen.getByRole('button', { name: 'สัปดาห์ถัดไป' }));
    await waitFor(() => expect(reads().at(-1)?.search).toContain('2026-11-23'));

    // A reload: a new store, which is what a new tab or a new login is too
    await renderApp('/admin/schedules');

    await waitFor(() => expect(reads().at(-1)?.search).toContain('weekStart=2026-11-16'));
  });

  it('a reload never writes the week it was showing to storage', async () => {
    pinClock();
    serveWeeks('supervisor', () => week());
    const { user } = await renderApp('/schedule-management');
    await screen.findByRole('heading', { level: 1, name: 'จัดตารางงาน' });
    await user.click(screen.getByRole('button', { name: 'สัปดาห์ถัดไป' }));

    // The memory is the store's, not the browser's (facility-0218): no localStorage, sessionStorage or cookie
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });
});

describe('the three week states, which are never the same screen (spec §3, §10)', () => {
  it('keeps each Cleaner row in the weekly roster when several people are scheduled', async () => {
    pinClock();
    serveWeeks('admin', () => week({
      rows: [person(), person({ userId: 8, displayName: 'แม่บ้านคนที่สอง' })],
    }));
    await renderApp('/admin/schedules');

    const table = await screen.findByRole('table', { name: 'ตารางงานรายสัปดาห์' });
    const rows = within(table).getAllByRole('row');
    expect(rows).toHaveLength(3);
    expect(within(rows[1]).getByRole('rowheader')).toHaveTextContent('แม่บ้านทดสอบ');
    expect(within(rows[2]).getByRole('rowheader')).toHaveTextContent('แม่บ้านคนที่สอง');
  });

  it('says so when the dates are outside retained coverage, instead of drawing an empty roster', async () => {
    pinClock();
    serveWeeks('cleaner', () =>
      week({
        availability: 'unavailable',
        rows: [person({ days: ['2026-11-16', '2026-11-17', '2026-11-18', '2026-11-19', '2026-11-20', '2026-11-21', '2026-11-22'].map((date) => day(date, 'unavailable')) })],
      }),
    );
    await renderApp('/my-schedule');

    expect(await screen.findByText('ไม่มีข้อมูลของสัปดาห์นี้')).toBeInTheDocument();
    // It is not "nobody is planned": no day card is drawn from it at all
    expect(screen.queryByText('ยังไม่กำหนด')).not.toBeInTheDocument();
    expect(screen.queryByText('วันหยุด')).not.toBeInTheDocument();
  });

  it('says the filter has nothing when the actor may read nothing in that week', async () => {
    pinClock();
    serveWeeks('admin', () => week({ availability: 'empty', rows: [] }));
    await renderApp('/admin/schedules');

    expect(await screen.findByText('ไม่มีตารางให้แสดงในสัปดาห์นี้')).toBeInTheDocument();
    // An empty week is not an unavailable one: it must not claim the history is missing
    expect(screen.queryByText('ไม่มีข้อมูลของสัปดาห์นี้')).not.toBeInTheDocument();
  });

  it('shows every day of a real week, each with its words rather than a colour', async () => {
    pinClock();
    serveWeeks('cleaner', () => week());
    await renderApp('/my-schedule');

    const cards = await screen.findAllByRole('article');
    expect(cards).toHaveLength(7);
    expect(within(cards[0]).getByText('มีแผนทำงาน')).toBeInTheDocument();
    expect(within(cards[2]).getByText('วันหยุด')).toBeInTheDocument();
    expect(within(cards[4]).getByText('ลา')).toBeInTheDocument();
    // An unknown weekday is shown as unknown: it is not a working day and not a day off
    expect(within(cards[6]).getByText('ยังไม่กำหนด')).toBeInTheDocument();
  });

  it('names the week a person is reading, and asking for another one asks the server for it', async () => {
    pinClock();
    const { reads } = serveWeeks('admin', () => week());
    const { user } = await renderApp('/admin/schedules');
    await screen.findByRole('heading', { level: 1, name: 'ตารางงาน' });

    await user.click(screen.getByRole('button', { name: 'สัปดาห์ก่อนหน้า' }));

    await waitFor(() => expect(reads().at(-1)?.search).toContain('weekStart=2026-11-09'));
    expect(await screen.findByText('09/11–15/11 2026')).toBeInTheDocument();
  });
});

describe('a changed plan and a read that may be old (spec §12)', () => {
  it('warns that the page may be old until a read that started after the change succeeds', async () => {
    pinClock();
    // The first answer lands at once; every read after the push stays pending until this test releases it, so the state
    // between "the plan moved" and "a newer read succeeded" is the one on screen for a while, as it would be on a slow link
    let release: ((answer: { status: number; body: ScheduleWeek }) => void) | undefined;
    let reads = 0;
    fakeServer({
      ...session('cleaner'),
      'GET /api/schedules/me': () => {
        reads += 1;
        return reads === 1
          ? { status: 200, body: week() }
          : new Promise((resolve) => {
              release = resolve;
            });
      },
    });
    await renderApp('/my-schedule');
    await screen.findByRole('heading', { level: 1, name: 'ตารางของฉัน' });
    await screen.findByText('16/11–22/11 2026');

    act(() => hub.latest().push('ScheduleChanged', { commandId: '7f1c2f22-6a44-4c11-9a1e-0f4c8a1b2c33' }));

    // The push arrived and the newer read has not succeeded: the page says so rather than presenting old data as current
    await waitFor(() => expect(reads).toBeGreaterThan(1));
    expect(screen.getByRole('status')).toHaveTextContent('ข้อมูลอาจยังไม่ล่าสุด');

    await act(async () => release?.({ status: 200, body: week({ revision: 'digest-2' }) }));

    // Only a successful read that started after the push clears it
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  });

  it('keeps its warning when the newer read fails, since a request that failed is not evidence', async () => {
    pinClock();
    let reads = 0;
    fakeServer({
      ...session('cleaner'),
      'GET /api/schedules/me': () => {
        reads += 1;
        return reads === 1 ? { status: 200, body: week() } : { status: 500, body: { message: 'ระบบขัดข้อง' } };
      },
    });
    await renderApp('/my-schedule');
    await screen.findByRole('heading', { level: 1, name: 'ตารางของฉัน' });
    const before = reads;

    act(() => hub.latest().push('ScheduleChanged', { commandId: '7f1c2f22-6a44-4c11-9a1e-0f4c8a1b2c33' }));

    await waitFor(() => expect(reads).toBeGreaterThan(before));
    expect(await screen.findByRole('status')).toHaveTextContent('ข้อมูลอาจยังไม่ล่าสุด');
  });

  it('keeps its warning while the connection is down, and asks again when it comes back', async () => {
    pinClock();
    const { reads } = serveWeeks('cleaner', () => week());
    await renderApp('/my-schedule');
    await screen.findByRole('heading', { level: 1, name: 'ตารางของฉัน' });
    const before = reads().length;

    act(() => hub.latest().reconnecting());
    expect(screen.getByRole('status')).toHaveTextContent('ข้อมูลอาจยังไม่ล่าสุด');

    act(() => hub.latest().reconnected());
    await waitFor(() => expect(reads().length).toBeGreaterThan(before));
  });
});

describe("a week's filters and the authority behind them (facility-0184, 0185, 0186)", () => {
  it('an Admin opens on every building in the shift on the clock, asks for no building, and labels the empty shift option ทุกกะ', async () => {
    pinClock();
    const { reads } = serveWeeks('admin', () => week());
    await renderApp('/admin/schedules');

    await screen.findByRole('heading', { level: 1, name: 'ตารางงาน' });
    // No building filter in the read: every building the shift holds, decided by the server's own scope
    expect(reads()[0].search).not.toContain('buildingId=');
    expect(reads()[0].search).toContain('shift=Day');
    const shiftGroup = screen.getByRole('group', { name: 'กะ' });
    expect(within(shiftGroup).getByRole('button', { name: 'ทุกกะ' })).toBeInTheDocument();
    expect(within(shiftGroup).queryByRole('button', { name: 'กะของฉัน' })).not.toBeInTheDocument();
  });

  it("a Supervisor asks for no building either: their read is every assigned building in their own shift", async () => {
    pinClock();
    const { reads } = serveWeeks('supervisor', () => week());
    await renderApp('/schedule-management');

    await screen.findByRole('heading', { level: 1, name: 'จัดตารางงาน' });
    expect(reads()[0].search).not.toContain('buildingId=');
    // A Supervisor cannot choose a shift, so none is sent and the server keeps the read inside their own
    expect(reads()[0].search).not.toContain('shift=');
  });

  it('a Cleaner has no filter at all: their read is only ever their own row', async () => {
    pinClock();
    const { reads } = serveWeeks('cleaner', () => week());
    await renderApp('/my-schedule');

    await screen.findByRole('heading', { level: 1, name: 'ตารางของฉัน' });
    expect(reads()[0].search).not.toContain('buildingId=');
    expect(reads()[0].search).not.toContain('shift=');
  });
});

describe('clean roster, KPI summary cards, and contextual action bar', () => {
  it('renders 4 KPI summary cards from real week data for businessDate', async () => {
    pinClock();
    serveWeeks('admin', () =>
      week({
        rows: [
          person({ userId: 7, displayName: 'แม่บ้านหนึ่ง' }),
          person({
            userId: 8,
            displayName: 'แม่บ้านสอง',
            days: [
              day('2026-11-16'),
              day('2026-11-17', 'leave'),
              day('2026-11-18'),
              day('2026-11-19'),
              day('2026-11-20'),
              day('2026-11-21'),
              day('2026-11-22'),
            ],
          }),
          person({
            userId: 9,
            displayName: 'แม่บ้านสาม',
            days: [
              day('2026-11-16'),
              day('2026-11-17', 'unset'),
              day('2026-11-18'),
              day('2026-11-19'),
              day('2026-11-20'),
              day('2026-11-21'),
              day('2026-11-22'),
            ],
          }),
        ],
      }),
    );
    await renderApp('/admin/schedules');

    const summary = await screen.findByRole('list', { name: 'สรุปตารางงาน' });
    const items = within(summary).getAllByRole('listitem');
    expect(items).toHaveLength(4);
    expect(items[0]).toHaveTextContent('พนักงานทั้งหมด');
    expect(items[0]).toHaveTextContent('3');
    expect(items[1]).toHaveTextContent('กำลังทำงาน');
    expect(items[1]).toHaveTextContent('1');
    expect(items[2]).toHaveTextContent('ลา / หยุด');
    expect(items[2]).toHaveTextContent('1');
    expect(items[3]).toHaveTextContent('มีปัญหา / รอจัดคนแทน');
    expect(items[3]).toHaveTextContent('1');
  });

  it('defaults selection to first row and businessDate, keeps table cells clean, and switches context bar between today and past', async () => {
    pinClock();
    const editablePerson = person();
    editablePerson.days = editablePerson.days.map((d) => ({
      ...d,
      edit: {
        canChange: true,
        weeklyOff: false,
        baselineAreaId: 4,
        baselineDayOff: false,
        areaRevision: '0',
        breakRevision: '0',
        areas: [{ id: 4, label: 'AR04' }],
        corrections: [],
      },
    }));
    serveWeeks('admin', () => week({ rows: [editablePerson] }));
    const { user } = await renderApp('/admin/schedules');

    const table = await screen.findByRole('table', { name: 'ตารางงานรายสัปดาห์' });
    expect(within(table).queryByRole('button', { name: /แก้เฉพาะวันที่|แก้ไขแผนย้อนหลัง|แก้ตารางประจำ/ })).not.toBeInTheDocument();

    const todayCell = within(table).getByRole('button', { name: /เลือกช่อง แม่บ้านทดสอบ .* 17\/11\/2026/ });
    const pastCell = within(table).getByRole('button', { name: /เลือกช่อง แม่บ้านทดสอบ .* 16\/11\/2026/ });
    expect(todayCell).toHaveAttribute('aria-pressed', 'true');
    expect(pastCell).toHaveAttribute('aria-pressed', 'false');

    const contextBar = screen.getByRole('region', { name: 'จัดการวันที่เลือก' });
    expect(within(contextBar).getByText('แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(within(contextBar).getByRole('button', { name: /แก้เฉพาะวันที่.*17\/11\/2026/ })).toBeInTheDocument();
    expect(within(contextBar).getByRole('button', { name: 'แก้ตารางประจำ' })).toBeInTheDocument();

    await user.click(pastCell);
    expect(pastCell).toHaveAttribute('aria-pressed', 'true');
    expect(todayCell).toHaveAttribute('aria-pressed', 'false');
    expect(within(contextBar).getByRole('button', { name: /แก้ไขแผนย้อนหลัง.*16\/11\/2026/ })).toBeInTheDocument();
    expect(within(contextBar).queryByRole('button', { name: /แก้เฉพาะวันที่/ })).not.toBeInTheDocument();

    // Closing the context bar deselects cleanly (Heuristic #3)
    await user.click(within(contextBar).getByRole('button', { name: 'ปิด' }));
    expect(screen.queryByRole('region', { name: 'จัดการวันที่เลือก' })).not.toBeInTheDocument();
  });

  it('shows supervisor team actions and schedule history inside ScheduleContextBar when eligible', async () => {
    pinClock();
    const editablePerson = person();
    editablePerson.days = editablePerson.days.map((d) => ({
      ...d,
      edit: {
        canChange: true,
        weeklyOff: false,
        baselineDayOff: false,
        areaRevision: '0',
        breakRevision: '0',
        areas: [{ id: 4, label: 'AR04' }],
        corrections: d.date === '2026-11-17'
          ? [{
              field: 'breakGroup',
              before: 1,
              after: 2,
              reason: 'ปรับกลุ่มพักตามหน้างาน',
              at: '2026-11-17T02:00:00Z',
              actor: 'หัวหน้าทดสอบ',
            }]
          : [],
      },
    }));
    serveWeeks('supervisor', () => week({ rows: [editablePerson] }));
    const { user } = await renderApp('/schedule-management');

    const contextBar = await screen.findByRole('region', { name: 'จัดการวันที่เลือก' });
    expect(within(contextBar).getByRole('button', { name: /เพิ่มเวลาที่ขาด แม่บ้านทดสอบ 17\/11\/2026/ })).toBeInTheDocument();
    expect(within(contextBar).getByRole('button', { name: /ย้ายพื้นที่ระหว่างกะ แม่บ้านทดสอบ 17\/11\/2026/ })).toBeInTheDocument();

    const historySummary = within(contextBar).getByText('ประวัติการแก้ไขแผน', { selector: 'summary span' });
    await user.click(historySummary);
    expect(await within(contextBar).findByText('เหตุผล · ปรับกลุ่มพักตามหน้างาน')).toBeVisible();
  });

  it('highlights today with (วันนี้) and renders semantic status pills in OwnWeek (/my-schedule)', async () => {
    pinClock('2026-11-17T02:00:00Z');
    serveWeeks('cleaner', () => week());
    await renderApp('/my-schedule');

    await screen.findByRole('heading', { level: 1, name: 'ตารางของฉัน' });
    const todayCard = (await screen.findByText(/17\/11\/2026 \(วันนี้\)/)).closest('article');
    expect(todayCard).not.toBeNull();
    expect(within(todayCard!).getByText('มีแผนทำงาน')).toBeInTheDocument();
    expect(screen.getByText('วันหยุด')).toBeInTheDocument();
    expect(screen.getByText('ลา')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่กำหนด')).toBeInTheDocument();
  });

  it('shows shift filter (ทุกกะ/กะเช้า/กะดึก) for Supervisor and sends chosen shift to GET /api/schedules', async () => {
    pinClock();
    const { reads } = serveWeeks('supervisor', () => week());
    const { user } = await renderApp('/schedule-management');

    await screen.findByRole('heading', { level: 1, name: 'จัดตารางงาน' });
    const shiftGroup = screen.getByRole('group', { name: 'กะ' });
    expect(within(shiftGroup).getByRole('button', { name: 'ทุกกะ' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(within(shiftGroup).getByRole('button', { name: 'กะดึก' }));
    await waitFor(() => expect(reads().some((call) => call.search.includes('shift=Night'))).toBe(true));

    await user.click(within(shiftGroup).getByRole('button', { name: 'กะเช้า' }));
    await waitFor(() => expect(reads().some((call) => call.search.includes('shift=Day'))).toBe(true));
  });

  it('displays unassigned-shift Regular Cleaner badge and allows opening regular baseline setup', async () => {
    pinClock();
    const unassignedPerson = person({
      userId: 9,
      displayName: 'แม่บ้านรอจัดกะ',
      hasWeeklyChoice: false,
      days: [
        '2026-11-16', '2026-11-17', '2026-11-18', '2026-11-19', '2026-11-20', '2026-11-21', '2026-11-22',
      ].map((date) => ({
        ...day(date, 'unset'),
        shift: undefined,
        edit: {
          canChange: true,
          weeklyOff: false,
          baselineDayOff: false,
          areaRevision: '0',
          breakRevision: '0',
          areas: [{ id: 4, label: 'AR04' }],
          corrections: [],
        },
      })),
    });
    serveWeeks('supervisor', () => week({ rows: [unassignedPerson] }));
    const { user } = await renderApp('/schedule-management');

    const table = await screen.findByRole('table', { name: 'ตารางงานรายสัปดาห์' });
    expect(within(table).getByText('ยังไม่จัดกะ')).toBeInTheDocument();

    const contextBar = await screen.findByRole('region', { name: 'จัดการวันที่เลือก' });
    expect(within(contextBar).queryByRole('button', { name: /แก้เฉพาะวันที่/ })).not.toBeInTheDocument();
    expect(within(contextBar).queryByRole('button', { name: /แก้ไขแผนย้อนหลัง/ })).not.toBeInTheDocument();

    const setupButton = await screen.findByRole('button', { name: 'ตั้งตารางประจำ' });
    await user.click(setupButton);
    expect(await screen.findByRole('dialog', { name: 'ตั้งตารางประจำ' })).toBeInTheDocument();
  });
});
