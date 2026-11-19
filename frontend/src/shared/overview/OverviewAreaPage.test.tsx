import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { adminSession } from '../../test/adminFixtures';
import { fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { overviewDetailFixture, overviewFixture } from '../../test/overviewFixtures';
import { renderApp } from '../../test/renderApp';
import type { OverviewAreaDetail } from './types';

const slotDate = '2026-11-16';
const cleaningUrl = '/dashboard?source=overview&date=2026-11-16&shift=Day&building=1&group=incomplete&page=2&areaId=91&view=cleaning';

function scopedFixture(overrides: Partial<OverviewAreaDetail> = {}) {
  const base = overviewDetailFixture();
  return overviewDetailFixture({ context: { ...base.context, shiftDate: slotDate, buildingId: 1 }, ...overrides });
}

function serve(routes: FakeRoutes = {}) {
  return fakeServer({ ...adminSession, 'GET /api/admin/alerts': () => ({ status: 200, body: [] }), ...routes });
}

describe('Overview Area destinations', () => {
  it('opens only the selected Area and preserves context on returning to Overview', async () => {
    fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas/91': () => ({ status: 200, body: scopedFixture() }),
      'GET /api/admin/overview/areas': () => ({ status: 200, body: overviewFixture({ page: 2, group: 'incomplete' }) }),
    });
    const { user, router } = await renderApp(cleaningUrl);
    expect(await screen.findByRole('heading', { name: /AR91 พื้นที่ทดลอง 91/ })).toBeVisible();
    expect(screen.queryByRole('heading', { name: /AR92/ })).not.toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: 'กลับภาพรวม' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/overview'));
    const params = new URLSearchParams(router.state.location.search);
    expect(Object.fromEntries(params)).toEqual({
      date: slotDate, shift: 'Day', building: '1', group: 'incomplete', page: '2',
    });
    await act(() => router.navigate(-1));
    expect(await screen.findByRole('heading', { name: /AR91 พื้นที่ทดลอง 91/ })).toBeVisible();
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(cleaningUrl);
    await act(() => router.navigate(1));
    expect(router.state.location.pathname).toBe('/admin/overview');
    expect(Object.fromEntries(new URLSearchParams(router.state.location.search))).toEqual(Object.fromEntries(params));
  });

  it('loads the same scoped dashboard directly from a fresh inspection destination', async () => {
    const { calls } = fakeServer({
      ...adminSession,
      'GET /api/admin/alerts': () => ({ status: 200, body: [] }),
      'GET /api/admin/overview/areas/91': () => ({ status: 200, body: scopedFixture() }),
    });
    await renderApp(cleaningUrl.replace('view=cleaning', 'view=inspection'));

    expect(await screen.findByRole('heading', { name: /AR91 พื้นที่ทดลอง 91/ })).toBeVisible();
    expect(screen.getByRole('heading', { name: 'ผลการตรวจ' })).toBeVisible();
    expect(calls.some((call) => call.path === '/api/service-points')).toBe(false);
  });

  it('reads an exact selected historical round and links each finalized round with all source filters', async () => {
    const base = scopedFixture();
    const first = base.rounds[0];
    const second = {
      ...first,
      roundKey: '2026-11-16:Day:91:9102:15:0800-0900',
      round: {
        ...first.round,
        pointId: 9102,
        start: '08:00:00',
        end: '09:00:00',
        finalizedReason: 'configurationChanged',
      },
      outcome: 'notInspected' as const,
      submissionId: 701,
    };
    const { calls } = serve({
      'GET /api/admin/overview/areas/91': ({ search }) => ({
        status: 200,
        body: {
          ...base,
          rounds: [first, second],
          selectedRoundKey: new URLSearchParams(search).get('roundKey'),
          area: { ...base.area, buildingName: 'ตึกเดิม' },
          matchesGroup: false,
        },
      }),
    });
    const { user, router } = await renderApp(cleaningUrl.replace('group=incomplete', 'group=missedCleaning'));

    expect(await screen.findByText(/ตึกเดิม/)).toBeVisible();
    expect(screen.getByText('สถานะพื้นที่เปลี่ยนจากรายการที่เลือกแล้ว')).toBeVisible();
    const history = screen.getByRole('region', { name: 'ประวัติรอบของพื้นที่' });
    expect(within(history).getAllByRole('link')).toHaveLength(2);
    await user.click(within(history).getByRole('link', { name: /08:00–09:00/ }));
    expect(await screen.findByRole('heading', { name: 'รอบที่เลือก 08:00–09:00' })).toBeVisible();
    expect(screen.getByText('เหตุผลที่สรุปรอบ ยกเลิกรอบเมื่อการตั้งค่าเปลี่ยน')).toBeVisible();
    expect(screen.getByText('ประวัติสำหรับอ่านข้อมูล')).toBeVisible();
    expect(screen.getByText('งานที่ส่ง #701')).toBeVisible();
    expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');
    const detailCalls = calls.filter((call) => call.path === '/api/admin/overview/areas/91');
    expect(Object.fromEntries(new URLSearchParams(detailCalls.at(-1)?.search))).toEqual({
      shiftDate: slotDate, shift: 'Day', buildingId: '1', group: 'missedCleaning', roundKey: second.roundKey,
    });
  });

  it('shows a receiving window, then retries the same Area after a 404 and keeps partial availability visible', async () => {
    let attempts = 0;
    const base = scopedFixture();
    const { calls } = serve({
      'GET /api/admin/overview/areas/91': () => ++attempts === 1
        ? { status: 404, body: { message: 'ไม่พบพื้นที่นี้ในวันที่และกะที่เลือก' } }
        : {
            status: 200,
            body: scopedFixture({
              context: { ...base.context, phase: 'receiving', isClockCurrent: false, closeUtc: '2026-11-16T12:30:00Z' },
              availability: { ...base.availability, work: { state: 'partial', reasons: ['workArchived'] } },
            }),
          },
    });
    const { user, router } = await renderApp(cleaningUrl);

    expect(await screen.findByRole('alert')).toHaveTextContent('ไม่พบพื้นที่นี้ในวันที่และกะที่เลือก');
    expect(screen.getByText(/16\/11\/2026 · กะเช้า/)).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));
    expect(await screen.findByText(/ยังรับข้อมูลถึง 16\/11\/2026 19:30/)).toBeVisible();
    expect(screen.getByText('ข้อมูลยังไม่ครบ แสดงเฉพาะรายการที่ยืนยันได้')).toBeVisible();
    expect(screen.getByRole('button', { name: 'ลองโหลดข้อมูลอีกครั้ง' })).toBeEnabled();
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(cleaningUrl);
    const detailCalls = calls.filter((call) => call.path.startsWith('/api/admin/overview/areas/'));
    expect(detailCalls).toHaveLength(2);
    expect(detailCalls[0].search).toBe(detailCalls[1].search);
    expect(detailCalls.every((call) => call.path.endsWith('/91'))).toBe(true);
  });

  it('keeps Point results, next round, and Issue reports distinct and renders report content as text', async () => {
    const base = scopedFixture();
    const report = '<b>ปัญหาจากจุดทดสอบ</b>';
    serve({
      'GET /api/admin/overview/areas/91': () => ({
        status: 200,
        body: scopedFixture({
          area: { ...base.area, work: { ...base.area.work, submittedPoints: 1, inspectedPoints: 1 } },
          facts: {
            ...base.facts,
            submissions: [{ id: 41, pointId: 9101, shiftDate: slotDate, shift: 'Day', submittedAt: '2026-11-16T02:06:00Z', userId: 201, status: 'Issue', placement: 'OnTime', note: report }],
            inspections: [{ id: 42, submissionId: 41, result: 'Rework', inspectedAt: '2026-11-16T02:12:00Z', supervisorId: 212, defect: 'ตรวจขอบประตู' }],
          },
          points: [
            { ...base.points[0], work: { ...base.points[0].work, isSubmitted: true, isInspected: true, submissionId: 41, inspectionId: 42 } },
            { ...base.points[1], work: { ...base.points[1].work, isUpcoming: true, isDue: false } },
          ],
        }),
      }),
    });
    await renderApp(cleaningUrl.replace('view=cleaning', 'view=inspection'));

    const results = await screen.findByRole('region', { name: 'ผลการตรวจ' });
    expect(within(results).getAllByText('1 / 2')).toHaveLength(2);
    expect(within(results).getByText(/งานที่ส่ง #41/)).toHaveTextContent('09:06');
    expect(within(results).getByText(/ผลตรวจ #42/)).toHaveTextContent('09:12');
    expect(within(results).getByText('ข้อบกพร่อง ตรวจขอบประตู')).toBeVisible();
    expect(within(results).getByText(/รอบถัดไป/)).toBeVisible();
    expect(within(results).queryByText(report)).not.toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'ปัญหาที่แจ้ง' })).getByText(report)).toBeVisible();
  });

  it('keeps distinct windows for one upcoming Point and replaces them when the current round opens', async () => {
    const base = scopedFixture();
    const point = base.points[0];
    const upcoming = { ...point, roundKey: null, work: { ...point.work, isDue: false, isUpcoming: true } };
    let body = scopedFixture({
      points: [
        { ...upcoming, windowId: 61, start: '10:00:00', end: '11:00:00' },
        { ...upcoming, windowId: 72, start: '13:00:00', end: '14:00:00' },
      ],
      availability: { ...base.availability, work: { state: 'partial', reasons: ['targetCoverageGap'] } },
    });
    serve({ 'GET /api/admin/overview/areas/91': () => ({ status: 200, body }) });
    const errors = vi.spyOn(console, 'error');
    try {
      const { user } = await renderApp(cleaningUrl.replace('group=incomplete', 'group=upcoming'));
      expect(await screen.findByText('รอบถัดไป 10:00–11:00')).toBeVisible();
      expect(screen.getByText('รอบถัดไป 13:00–14:00')).toBeVisible();

      body = scopedFixture({ points: [{ ...point, start: '10:00:00', end: '11:00:00' }] });
      await user.click(screen.getByRole('button', { name: 'ลองโหลดข้อมูลอีกครั้ง' }));
      expect(await screen.findByText('รอบ 10:00–11:00')).toBeVisible();
      expect(screen.queryByText(/รอบถัดไป/)).not.toBeInTheDocument();
      expect(errors.mock.calls.some(([message]) => String(message).includes('same key'))).toBe(false);
    } finally {
      errors.mockRestore();
    }
  });

  it('describes an Area selected from No Rounds as having no rounds in that shift', async () => {
    serve({ 'GET /api/admin/overview/areas/91': () => ({
      status: 200, body: scopedFixture({ points: [], rounds: [] }),
    }) });
    await renderApp(cleaningUrl.replace('group=incomplete', 'group=noRounds'));
    expect(await screen.findByText('ไม่มีรอบทำความสะอาดในกะที่เลือก')).toBeVisible();
    expect(screen.queryByText('ไม่มีจุดทำความสะอาดในบริบทนี้')).not.toBeInTheDocument();
  });
});
