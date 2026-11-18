import type { ScheduleDay, SchedulePerson, ScheduleWeek } from '../../shared/api/scheduleTypes';
import { authAnswer, fakeServer, type FakeRoutes } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';
import { offsetDate } from './commandRules';

export const fixtureMonday = '2026-11-16';
export const commandId = '6f034830-4382-4c69-a02a-1af87d20ff96';

export function fixturePerson(userId = 7, displayName = 'แม่บ้านทดสอบ', monday = fixtureMonday): SchedulePerson {
  const days = Array.from({ length: 7 }, (_, offset): ScheduleDay => ({
    date: offsetDate(monday, offset),
    shift: 'Day',
    area: { sourceKind: 'baseline', value: 4, revision: '2' },
    breakGroup: { sourceKind: 'baseline', value: 1, revision: '2' },
    availability: 'available', isDayOff: false, isLeave: false, isUnset: false, problems: [],
    edit: {
      canChange: true, weeklyOff: false, baselineAreaId: 4, baselineBreakGroup: 1, baselineDayOff: false,
      areaRevision: '0', breakRevision: '0', corrections: [],
      areas: [{ id: 4, label: 'AR04 · พื้นที่เดิม' }, { id: 5, label: 'AR05 · พื้นที่ใหม่' }],
    },
  }));
  return {
    userId, displayName, cleanerKind: 'Regular', homeBuildingId: 1, days, hasWeeklyChoice: false,
    regularBaselineRevision: '2', weeklyRevision: '0', reliefBaselineRevision: '0', weeklyReplaceAllowed: true,
  };
}

export function fixtureWeek(rows = [fixturePerson()], revision = 'fixture-1', weekStart = fixtureMonday): ScheduleWeek {
  return { weekStart, rows, revision, readAt: '2026-11-17T02:00:00Z', availability: 'available', relief: [] };
}

export async function editorApp({
  week = () => fixtureWeek(), source = fixtureWeek([], 'source', '2026-11-09'), routes = {},
}: { week?: () => ScheduleWeek; source?: ScheduleWeek; routes?: FakeRoutes } = {}) {
  const server = fakeServer({
    'POST /api/auth/refresh': () => authAnswer('supervisor'),
    'GET /api/schedules': ({ search }) => ({
      status: 200,
      body: new URLSearchParams(search).get('weekStart') === fixtureMonday ? week() : source,
    }),
    'POST /api/schedules/command-tokens': () => ({ status: 200, body: { token: 'signed', commandId, expiresAt: '2026-11-18T02:00:00Z' } }),
    'POST /api/schedules/commands': () => committed(),
    ...routes,
  });
  const app = await renderApp('/schedule-management');
  return { ...app, calls: server.calls };
}

export function committed() {
  return { status: 200, body: { commandId, outcome: 'committed', revisions: {}, problems: [] } };
}
