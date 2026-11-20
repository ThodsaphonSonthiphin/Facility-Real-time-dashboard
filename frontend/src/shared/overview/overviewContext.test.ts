import { createElement, type ReactNode } from 'react';
import { MemoryRouter } from 'react-router';
import { renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  OVERVIEW_GROUPS,
  areaHref,
  overviewHref,
  overviewParams,
  readOverviewContext,
} from './overviewContext';
import { useOverviewSource } from './useOverviewSource';

describe('overviewContext', () => {
  it('preserves slot, building, group and page through a scoped Area', () => {
    const context = {
      date: '2026-11-04',
      shift: 'Night' as const,
      building: 2,
      group: 'pendingInspection' as const,
      page: 2,
    };
    const href = areaHref(context, 17, 'inspection');
    const params = new URL(href, 'http://localhost').searchParams;
    expect(params.get('source')).toBe('overview');
    expect(params.get('areaId')).toBe('17');
    const parsed = readOverviewContext(params, true);
    expect(parsed.ok).toBe(true);
    if (parsed.ok) expect(overviewHref(parsed.context)).toBe(overviewHref(context));
  });

  it.each([
    '?date=2026-11-04',
    '?areaId=0&source=overview',
    '?date=2026-02-31&shift=Day',
    '?group=unknown',
    '?page=0',
  ])('rejects malformed overview context %s', (search) => {
    expect(readOverviewContext(new URLSearchParams(search)).ok).toBe(false);
  });

  it('defaults empty params to notEntered page 1 without date, shift, or building', () => {
    const parsed = readOverviewContext(new URLSearchParams());
    expect(parsed).toEqual({ ok: true, context: { group: 'notEntered', page: 1 } });
    if (parsed.ok) {
      expect(overviewHref(parsed.context)).toBe('/admin/overview?group=notEntered&page=1');
    }
  });

  it('routes attendance view to /admin/attendance and cleaning/inspection views to /dashboard', () => {
    const base = { date: '2026-11-04', shift: 'Day' as const, group: 'notEntered' as const, page: 1 };
    const attendanceUrl = new URL(areaHref(base, 91, 'attendance'), 'http://localhost');
    expect(attendanceUrl.pathname).toBe('/admin/attendance');
    expect(attendanceUrl.searchParams.get('building')).toBeNull();

    const roundKey = '2026-11-04:Day:91:9101:12:0700-0800';
    const cleaningUrl = new URL(areaHref(base, 91, 'cleaning', roundKey), 'http://localhost');
    expect(cleaningUrl.pathname).toBe('/dashboard');
    expect(cleaningUrl.searchParams.get('view')).toBe('cleaning');
    expect(cleaningUrl.searchParams.get('roundKey')).toBe(roundKey);

    const parsed = readOverviewContext(cleaningUrl.searchParams, true);
    expect(parsed).toEqual({
      ok: true,
      context: { ...base, areaId: 91, view: 'cleaning', roundKey },
    });
  });

  it('accepts all 16 allowlisted groups and valid leap-year dates', () => {
    for (const group of OVERVIEW_GROUPS) {
      const parsed = readOverviewContext(new URLSearchParams(`?date=2024-02-29&shift=Day&group=${group}&page=3`));
      expect(parsed).toEqual({
        ok: true,
        context: { date: '2024-02-29', shift: 'Day', group, page: 3 },
      });
    }
  });

  it.each([
    '?shift=Night',
    '?date=2025-02-29&shift=Day',
    '?date=2026-04-31&shift=Day',
    '?date=2026-11-04&shift=Evening',
    '?building=-1',
    '?building=1.5',
    '?building=01',
    '?building=9007199254740993',
    '?page=abc',
    '?areaId=1.2',
    '?view=other',
    '?source=other',
    '?roundKey=',
    '?group=entered&group=covered',
    '?page=1&page=2',
    '?returnUrl=https://evil.example',
    '?redirect=/login',
    '?RedirectUrl=/elsewhere',
  ])('rejects invalid or unsafe parameter %s', (search) => {
    expect(readOverviewContext(new URLSearchParams(search)).ok).toBe(false);
  });

  it('requires date, shift, areaId, and view when destination is true', () => {
    expect(readOverviewContext(new URLSearchParams('?group=notEntered&page=1'), true).ok).toBe(false);
    expect(
      readOverviewContext(new URLSearchParams('?date=2026-11-04&shift=Day&areaId=91'), true).ok,
    ).toBe(false);
    expect(
      readOverviewContext(new URLSearchParams('?date=2026-11-04&shift=Day&view=attendance'), true).ok,
    ).toBe(false);
  });

  it('ignores benign unrecognized UI parameters that cannot alter target', () => {
    const parsed = readOverviewContext(
      new URLSearchParams('?date=2026-11-04&shift=Day&group=entered&page=1&tab=timeline'),
    );
    expect(parsed).toEqual({
      ok: true,
      context: { date: '2026-11-04', shift: 'Day', group: 'entered', page: 1 },
    });
  });

  it('overviewParams emits date/shift only when paired and omits undefined optional fields', () => {
    const params = overviewParams({ date: '2026-11-04', group: 'notEntered', page: 1 });
    expect(params.has('date')).toBe(false);
    expect(params.has('shift')).toBe(false);
    expect(params.toString()).toBe('group=notEntered&page=1');
  });
});

describe('useOverviewSource', () => {
  function wrapperFor(initialEntry: string) {
    return function Wrapper({ children }: { children: ReactNode }) {
      return createElement(MemoryRouter, { initialEntries: [initialEntry] }, children);
    };
  }

  it('returns true whenever source=overview is present, even if other params are malformed', () => {
    const { result } = renderHook(() => useOverviewSource(), {
      wrapper: wrapperFor('/admin/attendance?source=overview&areaId=0'),
    });
    expect(result.current).toBe(true);
  });

  it('returns false when source is missing or not overview', () => {
    const missing = renderHook(() => useOverviewSource(), {
      wrapper: wrapperFor('/admin/attendance?date=2026-11-04&shift=Day'),
    });
    expect(missing.result.current).toBe(false);

    const other = renderHook(() => useOverviewSource(), {
      wrapper: wrapperFor('/dashboard?source=alerts'),
    });
    expect(other.result.current).toBe(false);
  });
});
