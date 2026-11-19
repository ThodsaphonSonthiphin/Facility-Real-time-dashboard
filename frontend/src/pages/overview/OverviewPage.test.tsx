import { setupListeners } from '@reduxjs/toolkit/query';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeResponse } from '../../test/fakeServer';
import { overviewFixture } from '../../test/overviewFixtures';
import { renderApp } from '../../test/renderApp';
import type { OverviewAreaList, OverviewAreaRow, OverviewCount } from '../../shared/overview/types';

function makeAreaRow(
  areaId: number,
  overrides: Partial<OverviewAreaRow> = {},
): OverviewAreaRow {
  return {
    areaId,
    buildingId: 1,
    code: `AR${areaId}`,
    name: `พื้นที่ทดลอง ${areaId}`,
    buildingCode: 'A',
    buildingName: 'ตึกทดลอง A',
    entry: 'entered',
    work: {
      duePoints: 4,
      submittedPoints: 4,
      inspectedPoints: 4,
      submitted: true,
      inspected: true,
      incomplete: false,
      pendingInspection: false,
      noSubmission: false,
      rework: false,
      issue: false,
      overdue: false,
    },
    reasons: ['entered', 'submitted', 'inspected'],
    roundKeys: [],
    originsIncomplete: false,
    ...overrides,
  };
}

function unknownCount(observed: number, state: 'partial' | 'unavailable' | 'notStarted', reasons: string[]): OverviewCount {
  return {
    value: null,
    observed,
    availability: { state, reasons },
  };
}

describe('OverviewPage', () => {
  it('shows the matching list below the overview and keeps category count independent of paging', async () => {
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: overviewFixture() }),
    });
    await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=notEntered&page=1');

    expect(await screen.findByRole('heading', { name: 'ภาพรวมการดูแลพื้นที่' })).toBeVisible();
    const results = screen.getByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });
    expect(within(results).getAllByRole('link', { name: /ดูพื้นที่/ })).toHaveLength(5);
    expect(within(results).getByText('แสดง 1–5 จาก 10 พื้นที่')).toBeVisible();
    expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' })).toBeVisible();
  });

  it('requests distinct groups when clicking entered 90 vs missing 10 and supports keyboard activation with aria-pressed', async () => {
    const base = overviewFixture();
    const enteredRows = [makeAreaRow(101), makeAreaRow(102)];
    const { calls } = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        if (params.get('group') === 'entered') {
          return {
            status: 200,
            body: overviewFixture({
              group: 'entered',
              total: base.summary.groups.entered,
              rows: enteredRows,
            }),
          };
        }
        return { status: 200, body: base };
      },
    });

    const { user, router } = await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=notEntered&page=1');

    const missingButton = await screen.findByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' });
    const enteredButton = screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' });
    expect(missingButton).toHaveAttribute('aria-pressed', 'true');
    expect(enteredButton).toHaveAttribute('aria-pressed', 'false');

    enteredButton.focus();
    expect(enteredButton).toHaveFocus();
    await user.keyboard('{Enter}');

    expect(await screen.findByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' })).toHaveAttribute('aria-pressed', 'false');
    expect(new URLSearchParams(router.state.location.search).get('group')).toBe('entered');
    expect(screen.getByRole('status', { name: 'สถานะภาพรวม' })).toHaveTextContent(/พื้นที่ที่ลงเวลาแล้ว/);

    await user.click(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' }));
    expect(await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' })).toBeVisible();
    const overviewGroups = calls
      .filter((c) => c.path === '/api/admin/overview/areas')
      .map((c) => new URLSearchParams(c.search).get('group'));
    // Each new connection can catch up once with the same selection.
    expect(overviewGroups.filter((group, index) => group !== overviewGroups[index - 1]))
      .toEqual(['notEntered', 'entered', 'notEntered']);
    expect(overviewGroups.length).toBeLessThanOrEqual(6);
  });

  it('keeps the category count on page 2 and resets page to 1 when a filter changes', async () => {
    const page2Rows = [96, 97, 98, 99, 100].map((id) =>
      makeAreaRow(id, { entry: 'unassigned', reasons: ['unassigned', 'notEntered', 'noSubmission'] }),
    );
    const { calls } = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        const page = Number(params.get('page') ?? '1');
        if (page === 2) {
          return { status: 200, body: overviewFixture({ page: 2, rows: page2Rows }) };
        }
        return { status: 200, body: overviewFixture() };
      },
    });

    const { user, router } = await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=notEntered&page=1');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    await user.click(screen.getByRole('button', { name: 'ถัดไป' }));
    const results = await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });
    expect(within(results).getByText('แสดง 6–10 จาก 10 พื้นที่')).toBeVisible();
    expect(screen.getByRole('button', { name: 'ยังไม่ลงเวลา 10 พื้นที่' })).toBeVisible();
    expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');

    // Changing shift or building resets page back to 1
    await user.click(screen.getByRole('button', { name: /กะดึก/ }));
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).get('page')).toBe('1'));
    expect(new URLSearchParams(router.state.location.search).get('shift')).toBe('Night');
    const lastSearch = new URLSearchParams(calls.filter((c) => c.path === '/api/admin/overview/areas').at(-1)!.search);
    expect(lastSearch.get('shift')).toBe('Night');
    expect(lastSearch.get('page')).toBe('1');
  });

  it('replaces server-default date/shift once retaining group and page without a request loop', async () => {
    const server = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({
        status: 200,
        body: overviewFixture({ group: 'overdue', page: 2 }),
      }),
    });

    const { router } = await renderApp('/admin/overview?group=overdue&page=2');
    await screen.findByRole('region', { name: 'พื้นที่ที่มีงานเลยเวลา' });
    await waitFor(() => {
      const p = new URLSearchParams(router.state.location.search);
      expect(p.get('date')).toBe('2026-11-04');
      expect(p.get('shift')).toBe('Day');
      expect(p.get('group')).toBe('overdue');
      expect(p.get('page')).toBe('2');
    });
    const overviewCalls = server.calls.filter((call) => call.path === '/api/admin/overview/areas');
    expect(overviewCalls.filter((call) => !new URLSearchParams(call.search).has('shiftDate'))).toHaveLength(1);
    expect(overviewCalls.length).toBeLessThanOrEqual(3);
  });

  it('replaces stale clamped page from server in URL without a request loop', async () => {
    const server = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const page = Number(new URLSearchParams(req.search).get('page') ?? '1');
        return {
          status: 200,
          body: overviewFixture({ group: 'overdue', page: page === 99 ? 2 : page }),
        };
      },
    });

    const { router } = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=overdue&page=99');
    await waitFor(() => {
      expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');
      expect(screen.getByRole('region', { name: 'พื้นที่ที่มีงานเลยเวลา' })).toBeVisible();
    });
    expect(server.calls.filter((c) => c.path === '/api/admin/overview/areas').length).toBeLessThanOrEqual(4);
  });

  it('does not allow a delayed older response to replace a newly selected context', async () => {
    let resolveDelayed!: (res: FakeResponse) => void;
    const base = overviewFixture();

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        const group = params.get('group');
        const building = params.get('buildingId');
        if (group === 'entered' && !building) {
          return new Promise((resolve) => {
            resolveDelayed = resolve;
          });
        }
        if (building === '2') {
          return {
            status: 200,
            body: overviewFixture({
              group: 'entered',
              total: base.summary.buildings[1].groups.entered,
              rows: [makeAreaRow(222, { code: 'AR222', name: 'พื้นที่ตึก B', buildingId: 2, buildingCode: 'B', buildingName: 'ตึกทดลอง B' })],
            }),
          };
        }
        return { status: 200, body: base };
      },
    });

    const { user } = await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=notEntered&page=1');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    await user.click(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' }));
    await waitFor(() => expect(resolveDelayed).toBeDefined());
    // Loading state is shown while the newly selected group has no currentData yet
    expect(screen.getByText('กำลังโหลดภาพรวม...')).toBeVisible();

    await user.selectOptions(screen.getByLabelText('ตึก'), '2');
    expect(await screen.findByText(/AR222/)).toBeVisible();

    // Now resolve the delayed 'entered' all-buildings response; building 2 filter must stay active
    await act(async () => {
      resolveDelayed({
        status: 200,
        body: overviewFixture({
          group: 'entered',
          total: base.summary.groups.entered,
          rows: [makeAreaRow(201, { code: 'AR201', name: 'พื้นที่ไม่ควรโผล่' })],
        }),
      });
    });

    expect(screen.getByText(/AR222/)).toBeVisible();
    expect(screen.queryByText(/AR201/)).not.toBeInTheDocument();
    expect(screen.getByLabelText('ตึก')).toHaveValue('2');
  });

  it('prevents delayed group response from replacing a newly picked group when changing selection directly', async () => {
    let resolveDelayed!: (res: FakeResponse) => void;
    const base = overviewFixture();

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        const building = params.get('buildingId');
        const group = params.get('group');
        if (group === 'entered' && !building) {
          return new Promise((resolve) => {
            resolveDelayed = resolve;
          });
        }
        if (building === '1') {
          return {
            status: 200,
            body: overviewFixture({
              group: 'entered',
              total: base.summary.buildings[0].groups.entered,
              rows: [makeAreaRow(111, { code: 'AR111', name: 'พื้นที่ตึก A' })],
            }),
          };
        }
        return { status: 200, body: base };
      },
    });

    const { user } = await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=notEntered&page=1');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    await user.click(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' }));
    await waitFor(() => expect(resolveDelayed).toBeDefined());

    await user.selectOptions(screen.getByLabelText('ตึก'), '1');
    expect(await screen.findByText(/AR111/)).toBeVisible();

    await act(async () => {
      resolveDelayed({
        status: 200,
        body: overviewFixture({
          group: 'entered',
          total: base.summary.groups.entered,
          rows: [makeAreaRow(999, { code: 'AR999', name: 'พื้นที่เก่าช้า' })],
        }),
      });
    });

    expect(await screen.findByText(/AR111/)).toBeVisible();
    expect(screen.queryByText(/AR999/)).not.toBeInTheDocument();
  });

  it('renders null counts as — with explanation and no 0%, and shows observed rows without claiming total', async () => {
    const base = overviewFixture();
    const partialAvail = { state: 'partial' as const, reasons: ['attendancePartlyExpired'] };
    const partialFixture: OverviewAreaList = {
      ...base,
      summary: {
        ...base.summary,
        availability: {
          ...base.summary.availability,
          entry: partialAvail,
        },
        entry: {
          numerator: unknownCount(1, 'partial', ['attendancePartlyExpired']),
          denominator: unknownCount(100, 'partial', ['targetCoverageMissing']),
        },
        groups: {
          ...base.summary.groups,
          entered: unknownCount(1, 'partial', ['attendancePartlyExpired']),
          covered: unknownCount(0, 'partial', ['attendancePartlyExpired']),
          unassigned: unknownCount(0, 'partial', ['attendancePartlyExpired']),
          notEntered: unknownCount(1, 'partial', ['attendancePartlyExpired']),
        },
      },
      group: 'entered',
      total: unknownCount(1, 'partial', ['attendancePartlyExpired']),
      rows: [makeAreaRow(91, { originsIncomplete: true })],
    };

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: partialFixture }),
    });

    await renderApp('/admin/overview?date=2026-10-06&shift=Day&group=entered&page=1');
    const results = await screen.findByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' });

    expect(screen.queryByText(/\b0%/)).not.toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
    expect(screen.getAllByText(/ข้อมูลลงเวลาบางส่วนพ้นช่วงจัดเก็บแล้ว/).length).toBeGreaterThan(0);
    expect(within(results).getByText('รายการที่ยืนยันได้ 1–1 (พบ 1 พื้นที่ · ยอดรวม: —)')).toBeVisible();
    expect(within(results).queryByText(/ทั้งหมด/)).not.toBeInTheDocument();
    expect(within(results).getByText('ข้อมูลจับคู่รอบไม่ครบ')).toBeVisible();
  });

  it('distinguishes complete empty from partial empty and notStarted', async () => {
    const base = overviewFixture();
    const emptyComplete: OverviewAreaList = {
      ...base,
      group: 'overdue',
      total: { value: 0, observed: 0, availability: { state: 'complete', reasons: [] } },
      rows: [],
    };
    const emptyPartial: OverviewAreaList = {
      ...base,
      group: 'overdue',
      total: unknownCount(0, 'partial', ['targetCoverageMissing']),
      rows: [],
    };

    let mode: 'complete' | 'partial' | 'notStarted' = 'complete';
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => {
        if (mode === 'partial') return { status: 200, body: emptyPartial };
        if (mode === 'notStarted') {
          return {
            status: 200,
            body: {
              ...base,
              summary: {
                ...base.summary,
                context: { ...base.summary.context, phase: 'notStarted', isClockCurrent: false },
                availability: {
                  entry: { state: 'notStarted', reasons: ['shiftNotStarted'] },
                  work: { state: 'notStarted', reasons: ['shiftNotStarted'] },
                  inspection: { state: 'notStarted', reasons: ['shiftNotStarted'] },
                  history: { state: 'notStarted', reasons: ['shiftNotStarted'] },
                  issue: { state: 'notStarted', reasons: ['shiftNotStarted'] },
                },
                entry: { numerator: unknownCount(0, 'notStarted', ['shiftNotStarted']), denominator: unknownCount(0, 'notStarted', ['shiftNotStarted']) },
                submission: { numerator: unknownCount(0, 'notStarted', ['shiftNotStarted']), denominator: unknownCount(0, 'notStarted', ['shiftNotStarted']) },
                inspection: { numerator: unknownCount(0, 'notStarted', ['shiftNotStarted']), denominator: unknownCount(0, 'notStarted', ['shiftNotStarted']) },
              },
              group: 'overdue',
              total: unknownCount(0, 'notStarted', ['shiftNotStarted']),
              rows: [],
            },
          };
        }
        return { status: 200, body: emptyComplete };
      },
    });

    const first = await renderApp('/admin/overview?date=2026-11-16&shift=Day&group=overdue&page=1');
    expect(await screen.findByText('ไม่มีพื้นที่ในกลุ่มนี้')).toBeVisible();
    expect(screen.queryByText(/ยอดรวม: —/)).not.toBeInTheDocument();

    mode = 'partial';
    await first.user.click(screen.getByRole('button', { name: /กะดึก/ }));
    expect(await screen.findByText('ไม่พบรายการที่ยืนยันได้ (ยอดรวม: —)')).toBeVisible();
    expect(screen.getByText(/ยังไม่มีประวัติเป้าหมายครบทั้งช่วงกะ/)).toBeVisible();

    mode = 'notStarted';
    await first.user.click(screen.getByRole('button', { name: /กะเช้า/ }));
    await waitFor(() => expect(screen.getAllByText('กะนี้ยังไม่เริ่ม').length).toBeGreaterThan(0));
    expect(screen.queryByText(/\b0%/)).not.toBeInTheDocument();
  });

  it('uses due-Area denominator for work and inspection, keeps rework distinct from inspected, and removes live wording on closed history', async () => {
    const base = overviewFixture();
    const closedFixture: OverviewAreaList = {
      ...base,
      summary: {
        ...base.summary,
        context: { ...base.summary.context, phase: 'closed', isClockCurrent: false },
        submission: {
          numerator: { value: 64, observed: 64, availability: { state: 'complete', reasons: [] } },
          denominator: { value: 80, observed: 80, availability: { state: 'complete', reasons: [] } },
        },
        inspection: {
          numerator: { value: 60, observed: 60, availability: { state: 'complete', reasons: [] } },
          denominator: { value: 80, observed: 80, availability: { state: 'complete', reasons: [] } },
        },
        groups: {
          ...base.summary.groups,
          submitted: { value: 64, observed: 64, availability: { state: 'complete', reasons: [] } },
          inspected: { value: 60, observed: 60, availability: { state: 'complete', reasons: [] } },
          rework: { value: 3, observed: 3, availability: { state: 'complete', reasons: [] } },
          upcoming: { value: 20, observed: 20, availability: { state: 'complete', reasons: [] } },
        },
      },
    };

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const group = (new URLSearchParams(req.search).get('group') as OverviewAreaList['group'] | null) ?? 'notEntered';
        return { status: 200, body: { ...closedFixture, group } };
      },
    });

    const { user } = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=1');

    // Exact CONTEXT.md metric headings
    expect(await screen.findByRole('heading', { name: 'พื้นที่ที่ลงเวลาเข้าแล้ว' })).toBeVisible();
    expect(screen.getByRole('heading', { name: 'พื้นที่ที่ส่งงานครบ' })).toBeVisible();
    expect(screen.getByRole('heading', { name: 'พื้นที่ที่ตรวจครบ' })).toBeVisible();

    // Due-Area denominator 80 (not 100 staffing) and rework distinct from inspected
    expect(screen.getAllByText('จาก 80 พื้นที่ที่มีจุดถึงรอบ')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'ตรวจครบแล้ว 60 พื้นที่' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'มีงานต้องแก้ 3 พื้นที่' })).toBeVisible();

    // Closed history uses ผลรอบในกะนี้ and has no current-live wording
    expect(screen.getByText(/· ปิดรับข้อมูลแล้ว/)).toBeVisible();
    expect(screen.queryByText(/กำลังทำงาน/)).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'ผลรอบในกะนี้' })).toBeVisible();
    expect(screen.queryByRole('heading', { name: 'รอบที่ผ่านมาในกะนี้' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'พื้นที่ที่ต้องตามตอนนี้' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'รายการที่พบในกะที่เลือก' })).toBeVisible();
    expect(screen.getByText('รอบที่ไม่ได้แก้ (Rework)')).toBeVisible();
    expect(screen.getByText('รอบที่ถูกเปลี่ยนหรือยกเลิก')).toBeVisible();

    // Clicking rework switches only to rework while keeping inspected count distinct
    await user.click(screen.getByRole('button', { name: 'มีงานต้องแก้ 3 พื้นที่' }));
    expect(await screen.findByRole('region', { name: 'พื้นที่ที่มีงานต้องแก้' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'ตรวจครบแล้ว 60 พื้นที่' })).toBeVisible();
  });

  it('derives destination views by group, renders every missed round link when multiple exist, and supports building comparison click', async () => {
    const base = overviewFixture();
    const multiRoundRow = makeAreaRow(77, {
      code: 'AR77',
      name: 'พื้นที่หลายรอบ',
      reasons: ['entered', 'missedCleaning'],
      roundKeys: [
        '2026-11-04:Day:77:7701:12:0700-0800',
        '2026-11-04:Day:77:7701:13:0900-1000',
      ],
    });

    const { calls } = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        const group = params.get('group');
        const page = Number(params.get('page') ?? '1');
        if (group === 'missedCleaning') {
          return {
            status: 200,
            body: overviewFixture({
              group: 'missedCleaning',
              total: base.summary.groups.missedCleaning,
              rows: [multiRoundRow],
            }),
          };
        }
        if (group === 'missedInspection') {
          return {
            status: 200,
            body: overviewFixture({
              group: 'missedInspection',
              total: base.summary.groups.missedInspection,
              rows: [
                makeAreaRow(88, {
                  code: 'AR88',
                  name: 'พื้นที่ตรวจหลายรอบ',
                  reasons: ['entered', 'missedInspection'],
                  roundKeys: ['key-1', 'key-2'],
                }),
              ],
            }),
          };
        }
        return {
          status: 200,
          body: overviewFixture({
            group: (group as OverviewAreaList['group']) ?? 'notEntered',
            page,
          }),
        };
      },
    });

    const { user, router } = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=2');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    // Staffing group -> /admin/attendance
    const firstView = screen.getAllByRole('link', { name: /ดูพื้นที่/ })[0];
    expect(firstView).toHaveAttribute('href', expect.stringContaining('/admin/attendance?'));
    expect(firstView).toHaveAttribute('href', expect.stringContaining('view=attendance'));

    // Click missedCleaning -> cleaning view + multiple round links (never pick one arbitrarily)
    await user.click(screen.getByRole('button', { name: /พื้นที่ที่มีรอบไม่ได้ทำ/ }));
    const results = await screen.findByRole('region', { name: 'พื้นที่ที่มีรอบไม่ได้ทำ' });
    const areaLink = within(results).getByRole('link', { name: 'ดูพื้นที่ AR77 พื้นที่หลายรอบ' });
    expect(areaLink).toHaveAttribute('href', expect.stringContaining('/dashboard?'));
    expect(areaLink).toHaveAttribute('href', expect.stringContaining('view=cleaning'));
    expect(areaLink.getAttribute('href')).not.toContain('roundKey=');

    const round1 = within(results).getByRole('link', { name: /รอบ 07:00–08:00/ });
    const round2 = within(results).getByRole('link', { name: /รอบ 09:00–10:00/ });
    expect(round1).toHaveAttribute('href', expect.stringContaining('roundKey=2026-11-04%3ADay%3A77%3A7701%3A12%3A0700-0800'));
    expect(round2).toHaveAttribute('href', expect.stringContaining('roundKey=2026-11-04%3ADay%3A77%3A7701%3A13%3A0900-1000'));

    // Also verify missedInspection with generic roundKeys ['key-1', 'key-2'] renders distinct per-round links
    await user.click(screen.getByRole('button', { name: /พื้นที่ที่มีรอบไม่ได้ตรวจ/ }));
    const inspResults = await screen.findByRole('region', { name: 'พื้นที่ที่มีรอบไม่ได้ตรวจ' });
    const inspAreaLink = within(inspResults).getByRole('link', { name: 'ดูพื้นที่ AR88 พื้นที่ตรวจหลายรอบ' });
    expect(inspAreaLink).toHaveAttribute('href', expect.stringContaining('view=inspection'));
    expect(inspAreaLink.getAttribute('href')).not.toContain('roundKey=');
    const key1Link = within(inspResults).getByRole('link', { name: /รอบที่ 1/ });
    const key2Link = within(inspResults).getByRole('link', { name: /รอบที่ 2/ });
    expect(key1Link).toHaveAttribute('href', expect.stringContaining('view=inspection'));
    expect(key1Link).toHaveAttribute('href', expect.stringContaining('roundKey=key-1'));
    expect(key2Link).toHaveAttribute('href', expect.stringContaining('view=inspection'));
    expect(key2Link).toHaveAttribute('href', expect.stringContaining('roundKey=key-2'));
    expect(key1Link.getAttribute('href')).not.toBe(key2Link.getAttribute('href'));

    // Click Building B inspection cell -> sets building=2, group=inspected, page=1
    await user.click(screen.getByRole('button', { name: /ตึก B · ตึกทดลอง B ตรวจครบ/ }));
    await waitFor(() => {
      const params = new URLSearchParams(router.state.location.search);
      expect(params.get('building')).toBe('2');
      expect(params.get('group')).toBe('inspected');
      expect(params.get('page')).toBe('1');
    });
    const lastApiSearch = new URLSearchParams(calls.filter((c) => c.path === '/api/admin/overview/areas').at(-1)!.search);
    expect(lastApiSearch.get('buildingId')).toBe('2');
    expect(lastApiSearch.get('group')).toBe('inspected');
    expect(lastApiSearch.get('page')).toBe('1');
  });

  it('rejects invalid URL context without fetching', async () => {
    const server = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: overviewFixture() }),
    });

    await renderApp('/admin/overview?date=2026-11-04');
    expect(await screen.findByText('ลิงก์หน้านี้ไม่ถูกต้อง')).toBeVisible();
    expect(screen.getByText('ต้องระบุวันที่และกะคู่กัน')).toBeVisible();
    expect(server.calls.some((c) => c.path === '/api/admin/overview/areas')).toBe(false);
  });

  it('retries failed initial fetch and keeps previous response on background refresh error without stealing focus', async () => {
    let fail = true;
    let readAt = '2026-11-04T03:00:00Z';
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => {
        if (fail) {
          return { status: 500, body: { message: 'เซิร์ฟเวอร์ขัดข้อง' } };
        }
        const fixture = overviewFixture();
        return {
          status: 200,
          body: {
            ...fixture,
            summary: { ...fixture.summary, context: { ...fixture.summary.context, readAtUtc: readAt } },
          },
        };
      },
    });

    const retryApp = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=1');
    expect(await screen.findByText('เซิร์ฟเวอร์ขัดข้อง')).toBeVisible();

    fail = false;
    await retryApp.user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
    expect(await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' })).toBeVisible();

    // Focus a control, then verify background refresh updates status announcement without stealing focus
    const buildingSelect = screen.getByLabelText('ตึก');
    buildingSelect.focus();
    expect(buildingSelect).toHaveFocus();

    const stopListening = setupListeners(retryApp.store.dispatch);
    try {
      readAt = '2026-11-04T03:05:00Z';
      await act(async () => {
        window.dispatchEvent(new Event('focus'));
      });
      await waitFor(() => expect(screen.getByRole('status', { name: 'สถานะภาพรวม' })).toHaveTextContent(/10:05/));
      expect(buildingSelect).toHaveFocus();

      // Background refresh failure keeps previous response + readAtUtc + retry
      fail = true;
      await act(async () => {
        window.dispatchEvent(new Event('focus'));
      });
      const warnNote = await screen.findByText(/ข้อมูลอาจยังไม่ล่าสุด/);
      expect(warnNote).toBeVisible();
      expect(warnNote).toHaveTextContent(/10:05/);
      expect(screen.getByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' })).toBeVisible();
    } finally {
      stopListening();
    }
  });

  it('shows error without stale data from previous context when switching to a new context fails', async () => {
    const base = overviewFixture();
    let failEntered = true;
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        if (params.get('group') === 'entered') {
          if (failEntered) {
            return { status: 500, body: { message: 'โหลดกลุ่มใหม่ไม่สำเร็จ' } };
          }
          return {
            status: 200,
            body: overviewFixture({
              group: 'entered',
              total: base.summary.groups.entered,
              rows: [makeAreaRow(202, { code: 'AR202', name: 'พื้นที่เข้าใหม่' })],
            }),
          };
        }
        return { status: 200, body: base };
      },
    });

    const { user } = await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=1');
    const initialResults = await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });
    const firstRowText = within(initialResults).getAllByRole('link', { name: /ดูพื้นที่/ })[0].getAttribute('aria-label')!;

    await user.click(screen.getByRole('button', { name: 'ลงเวลาแล้ว 90 พื้นที่' }));
    expect(await screen.findByText('โหลดกลุ่มใหม่ไม่สำเร็จ')).toBeVisible();
    expect(screen.queryByText(/ข้อมูลอาจยังไม่ล่าสุด/)).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: firstRowText })).not.toBeInTheDocument();

    failEntered = false;
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
    expect(await screen.findByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' })).toBeVisible();
    expect(screen.getByText(/AR202/)).toBeVisible();
  });

  it.each(['reset to current', 'remount', 'Back and Forward'] as const)('waits for the fresh default slot after %s', async (navigation) => {
    const base = overviewFixture();
    const day = {
      ...base,
      rows: [makeAreaRow(1, { name: 'พื้นที่กะเช้า' })],
    };
    const night = {
      ...base,
      summary: {
        ...base.summary,
        context: { ...base.summary.context, shift: 'Night' as const },
      },
      rows: [makeAreaRow(2, { name: 'พื้นที่กะดึก' })],
    };
    let rolledOver = false;
    let resolveRollover!: (res: FakeResponse) => void;
    const rolloverPromise = new Promise<FakeResponse>((resolve) => {
      resolveRollover = resolve;
    });

    const { calls } = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/buildings/all': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': (req) => {
        const params = new URLSearchParams(req.search);
        const shiftDate = params.get('shiftDate');
        const shift = params.get('shift');
        if (!shiftDate && !shift) {
          if (rolledOver) {
            return rolloverPromise;
          }
          return { status: 200, body: day };
        }
        if (shiftDate === '2026-11-03') {
          return {
            status: 200,
            body: {
              ...base,
              summary: {
                ...base.summary,
                context: { ...base.summary.context, shiftDate: '2026-11-03', shift: 'Day', isClockCurrent: false },
              },
              rows: [makeAreaRow(3, { name: 'พื้นที่เมื่อวาน' })],
            },
          };
        }
        return { status: 200, body: shift === 'Night' ? night : day };
      },
    });

    const { user, router } = await renderApp('/admin/overview');
    await waitFor(() => {
      const p = new URLSearchParams(router.state.location.search);
      expect(p.get('date')).toBe('2026-11-04');
      expect(p.get('shift')).toBe('Day');
    });

    if (navigation === 'remount') {
      await act(() => router.navigate('/admin/export'));
      expect(screen.queryByRole('heading', { name: 'ภาพรวมการดูแลพื้นที่' })).not.toBeInTheDocument();
    } else {
      fireEvent.change(screen.getByLabelText('วันที่ของกะ'), { target: { value: '2026-11-03' } });
      expect(await screen.findByRole('row', { name: /AR3/ })).toBeVisible();
    }

    // Night is now current while the earlier default Day response remains cached.
    rolledOver = true;
    if (navigation === 'remount') {
      await act(() => router.navigate('/admin/overview'));
    } else {
      await user.click(screen.getByRole('button', { name: 'กลับกะปัจจุบัน' }));
    }
    const defaultCalls = () => calls.filter((call) => {
      if (call.path !== '/api/admin/overview/areas') return false;
      const params = new URLSearchParams(call.search);
      return !params.has('shiftDate') && !params.has('shift');
    });
    await waitFor(() => expect(defaultCalls()).toHaveLength(2));

    if (navigation === 'Back and Forward') {
      await act(() => router.navigate(-1));
      expect(await screen.findByRole('row', { name: /AR3/ })).toBeVisible();
      await act(() => router.navigate(1));
    }

    // The URL stays unresolved while the server's current slot is still pending.
    expect(new URLSearchParams(router.state.location.search).get('date')).toBeNull();
    expect(new URLSearchParams(router.state.location.search).get('shift')).toBeNull();
    expect(screen.queryByRole('row', { name: /AR1/ })).not.toBeInTheDocument();
    expect(screen.getByText('กำลังโหลดภาพรวม...')).toBeVisible();

    await act(async () => {
      resolveRollover({ status: 200, body: night });
    });

    await waitFor(() => {
      const p = new URLSearchParams(router.state.location.search);
      expect(p.get('date')).toBe('2026-11-04');
      expect(p.get('shift')).toBe('Night');
    });
    expect(await screen.findByRole('row', { name: /AR2/ })).toBeVisible();
    expect(defaultCalls()).toHaveLength(2);
  });

  it('renders — instead of ยังไม่มีรอบในกะนี้ when a partial row has duePoints 0 without upcoming or noRounds', async () => {
    const base = overviewFixture();
    const partialFixture: OverviewAreaList = {
      ...base,
      summary: {
        ...base.summary,
        availability: {
          ...base.summary.availability,
          work: { state: 'partial', reasons: ['targetCoverageMissing'] },
          inspection: { state: 'partial', reasons: ['targetCoverageMissing'] },
        },
        submission: {
          numerator: unknownCount(1, 'partial', ['targetCoverageMissing']),
          denominator: unknownCount(1, 'partial', ['targetCoverageMissing']),
        },
        inspection: {
          numerator: unknownCount(0, 'partial', ['targetCoverageMissing']),
          denominator: unknownCount(1, 'partial', ['targetCoverageMissing']),
        },
      },
      group: 'entered',
      total: { value: 2, observed: 2, availability: { state: 'complete', reasons: [] } },
      rows: [
        makeAreaRow(92, {
          code: 'AR92',
          name: 'พื้นที่เป้าไม่ครบ',
          work: {
            duePoints: 0,
            submittedPoints: 1,
            inspectedPoints: 0,
            submitted: false,
            inspected: false,
            incomplete: false,
            pendingInspection: false,
            noSubmission: false,
            rework: false,
            issue: false,
            overdue: false,
          },
          reasons: ['entered'],
        }),
        makeAreaRow(93, {
          code: 'AR93',
          name: 'พื้นที่ไม่มีรอบจริง',
          work: {
            duePoints: 0,
            submittedPoints: 0,
            inspectedPoints: 0,
            submitted: false,
            inspected: false,
            incomplete: false,
            pendingInspection: false,
            noSubmission: false,
            rework: false,
            issue: false,
            overdue: false,
          },
          reasons: ['entered', 'noRounds'],
        }),
      ],
    };

    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: partialFixture }),
    });

    await renderApp('/admin/overview?date=2026-10-06&shift=Day&group=entered&page=1');
    const results = await screen.findByRole('region', { name: 'พื้นที่ที่ลงเวลาแล้ว' });
    const row92 = within(results).getByRole('row', { name: /AR92/ });
    const row93 = within(results).getByRole('row', { name: /AR93/ });

    expect(within(row92).getByText('—')).toBeVisible();
    expect(within(row92).queryByText('ยังไม่มีรอบในกะนี้')).not.toBeInTheDocument();
    expect(within(row93).getByText('ยังไม่มีรอบในกะนี้')).toBeVisible();
  });

  it('shows visible date/shift/building labels and server range help instead of mockup dates', async () => {
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: overviewFixture() }),
    });

    await renderApp('/admin/overview?date=2026-11-04&shift=Day&group=notEntered&page=1');
    await screen.findByRole('region', { name: 'พื้นที่ที่ยังไม่ลงเวลา' });

    const dateInput = screen.getByLabelText('วันที่ของกะ');
    expect(dateInput).toHaveValue('2026-11-04');
    expect(dateInput).toHaveAccessibleDescription('ข้อมูลครบช่วง 01/10/2026–04/11/2026');
    expect(screen.getByRole('group', { name: 'กะ' })).toBeVisible();
    expect(screen.getByLabelText('ตึก')).toBeVisible();
  });
});
