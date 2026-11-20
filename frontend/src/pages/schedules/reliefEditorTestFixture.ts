import { afterEach, beforeEach, vi } from 'vitest';
import type { ReliefCandidate } from '../../shared/api/scheduleTypes';
import { offsetDate } from './commandRules';
import { fixturePerson, fixtureWeek } from './scheduleEditorTestFixture';

export function installReliefFixtureClock() {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-11-16T02:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());
}

export function reliefWeek(monday = '2026-11-16') {
  const relief = (id: number, name: string, home: number) => {
    const row = fixturePerson(id, name, monday);
    return { ...row, cleanerKind: 'Relief' as const, homeBuildingId: home,
      homeBuildingCode: home === 1 ? 'A' : 'B', reliefBaselineRevision: id === 7 ? '9007199254740993' : '0',
      days: row.days.map((day) => ({ ...day, edit: { ...day.edit!, baselineAreaId: undefined, baselineBreakGroup: undefined } })),
    };
  };
  return {
    ...fixtureWeek([relief(7, 'สแปร์หนึ่ง', 1), relief(8, 'สแปร์สอง', 2), fixturePerson(9, 'แม่บ้านประจำหนึ่ง', monday)], 'week', monday),
    destinations: Array.from({ length: 7 }, (_, offset) => [1, 2].map((buildingId) => ({
      date: offsetDate(monday, offset), shift: 'Day' as const, buildingId, buildingCode: buildingId === 1 ? 'A' : 'B',
      areas: [{ id: buildingId === 1 ? 4 : 5, label: buildingId === 1 ? 'AR04' : 'AR05' }],
    }))).flat(),
    sourceBuildings: [{ id: 1, code: 'A' }, { id: 2, code: 'B' }, { id: 3, code: 'C' }],
  };
}

export function reliefCandidate(overrides: Partial<ReliefCandidate> = {}): ReliefCandidate {
  return { userId: 7, employeeId: 'E007', displayName: 'สแปร์หนึ่ง', homeBuildingId: 1, homeBuildingCode: 'A',
    shift: 'Day', eligible: true, status: 'eligible', conflicts: [], revision: '0', agreementRequired: false, ...overrides };
}
