import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { overviewDetailFixture, overviewFixture } from '../../test/overviewFixtures';
import { overviewApi } from './overviewApi';

function loggedInStore() {
  const store = makeStore();
  store.dispatch(setCredentials(authAnswer('admin', 'admin-token').body));
  return store;
}

describe('overviewApi', () => {
  it('fetches overview summary with exact query keys and preserves explicit null counts', async () => {
    const list = overviewFixture();
    const partialSummary = {
      ...list.summary,
      entry: {
        numerator: { value: null, observed: 90, availability: { state: 'partial' as const, reasons: ['attendancePartlyExpired'] } },
        denominator: { value: 100, observed: 100, availability: { state: 'complete' as const, reasons: [] } },
      },
    };
    const server = fakeServer({
      'GET /api/admin/overview': () => ({ status: 200, body: partialSummary }),
    });
    const store = loggedInStore();

    const result = await store.dispatch(
      overviewApi.endpoints.getOverview.initiate({
        shiftDate: '2026-11-04',
        shift: 'Night',
        buildingId: undefined,
      }),
    );

    expect(server.calls).toHaveLength(1);
    expect(server.calls[0].search).toBe('?shiftDate=2026-11-04&shift=Night');
    expect(server.calls[0].search).not.toContain('undefined');
    expect(result.data?.entry.numerator.value).toBeNull();
    expect(result.data?.entry.numerator.observed).toBe(90);
  });

  it('fetches paginated area rows with pageSize=5 and exact context params', async () => {
    const fixture = overviewFixture();
    const server = fakeServer({
      'GET /api/admin/overview/areas': () => ({ status: 200, body: fixture }),
    });
    const store = loggedInStore();

    const result = await store.dispatch(
      overviewApi.endpoints.getOverviewAreas.initiate({
        shiftDate: '2026-11-04',
        shift: 'Day',
        buildingId: 2,
        group: 'notEntered',
        page: 1,
        pageSize: 5,
      }),
    );

    expect(server.calls).toHaveLength(1);
    const params = new URLSearchParams(server.calls[0].search);
    expect(params.get('shiftDate')).toBe('2026-11-04');
    expect(params.get('shift')).toBe('Day');
    expect(params.get('buildingId')).toBe('2');
    expect(params.get('group')).toBe('notEntered');
    expect(params.get('page')).toBe('1');
    expect(params.get('pageSize')).toBe('5');
    expect(server.calls[0].search).not.toContain('undefined');
    expect(result.data?.rows).toHaveLength(5);
    expect(result.data?.total.value).toBe(10);
  });

  it('fetches area detail with areaId in the path and optional roundKey without undefined strings', async () => {
    const detail = overviewDetailFixture();
    const server = fakeServer({
      'GET /api/admin/overview/areas/91': () => ({ status: 200, body: detail }),
    });
    const store = loggedInStore();

    const result = await store.dispatch(
      overviewApi.endpoints.getOverviewArea.initiate({
        areaId: 91,
        shiftDate: '2026-11-04',
        shift: 'Day',
        buildingId: undefined,
        group: 'notEntered',
        roundKey: '2026-11-04:Day:91:9101:12:0700-0800',
      }),
    );

    expect(server.calls).toHaveLength(1);
    expect(server.calls[0].path).toBe('/api/admin/overview/areas/91');
    const params = new URLSearchParams(server.calls[0].search);
    expect(params.has('areaId')).toBe(false);
    expect(params.has('buildingId')).toBe(false);
    expect(params.get('shiftDate')).toBe('2026-11-04');
    expect(params.get('shift')).toBe('Day');
    expect(params.get('group')).toBe('notEntered');
    expect(params.get('roundKey')).toBe('2026-11-04:Day:91:9101:12:0700-0800');
    expect(server.calls[0].search).not.toContain('undefined');
    expect(result.data?.area.areaId).toBe(91);
    expect(result.data?.points.map((p) => p.name)).toEqual(['จุดตรวจ AR91-1', 'จุดตรวจ AR91-2']);
  });
});
