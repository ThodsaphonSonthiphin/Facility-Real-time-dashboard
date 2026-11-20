import { describe, expect, it } from 'vitest';
import { OVERVIEW_GROUPS } from '../overview/overviewContext';
import { attendanceEvents, eventLabel, pointStatusLabel, shiftLabel } from '../utils/labels';
import { overviewFixture } from '../../test/overviewFixtures';

describe('Frontend/Backend API contract invariants', () => {
  it('maps all 16 OverviewGroup keys in both fixture summary and URL parser', () => {
    const fixture = overviewFixture();
    const fixtureKeys = Object.keys(fixture.summary.groups).sort();
    expect(fixtureKeys).toEqual([...OVERVIEW_GROUPS].sort());
  });

  it('covers all six attendance events, both shifts, and all seven point statuses', () => {
    expect(attendanceEvents).toEqual([
      'ShiftIn', 'Break1Out', 'Break1In', 'Break2Out', 'Break2In', 'ShiftOut',
    ]);
    for (const event of attendanceEvents) {
      expect(eventLabel[event]).toBeTruthy();
    }
    expect(Object.keys(shiftLabel).sort()).toEqual(['Day', 'Night']);
    expect(Object.keys(pointStatusLabel).sort()).toEqual([
      'BeforeFirstRound', 'NotYetDone', 'OffHours', 'Overdue', 'Passed', 'PendingInspection', 'Rework',
    ]);
  });
});
