import type {
  Availability,
  OverviewAreaDetail,
  OverviewAreaList,
  OverviewAreaRow,
  OverviewBuilding,
  OverviewContext,
  OverviewCount,
  OverviewGroup,
  OverviewSummary,
  SectionAvailability,
} from '../shared/overview/types';

const COMPLETE: Availability = { state: 'complete', reasons: [] };

const COMPLETE_SECTIONS: SectionAvailability = {
  entry: COMPLETE,
  work: COMPLETE,
  inspection: COMPLETE,
  history: COMPLETE,
  issue: COMPLETE,
};

function completeCount(n: number): OverviewCount {
  return { value: n, observed: n, availability: COMPLETE };
}

function makeGroups(values: Record<OverviewGroup, number>): Record<OverviewGroup, OverviewCount> {
  return {
    entered: completeCount(values.entered),
    covered: completeCount(values.covered),
    unassigned: completeCount(values.unassigned),
    notEntered: completeCount(values.notEntered),
    submitted: completeCount(values.submitted),
    incomplete: completeCount(values.incomplete),
    inspected: completeCount(values.inspected),
    pendingInspection: completeCount(values.pendingInspection),
    noSubmission: completeCount(values.noSubmission),
    rework: completeCount(values.rework),
    issue: completeCount(values.issue),
    overdue: completeCount(values.overdue),
    upcoming: completeCount(values.upcoming),
    noRounds: completeCount(values.noRounds),
    missedCleaning: completeCount(values.missedCleaning),
    missedInspection: completeCount(values.missedInspection),
  };
}

function makeContext(): OverviewContext {
  return {
    shiftDate: '2026-11-04',
    shift: 'Day',
    buildingId: null,
    phase: 'running',
    isClockCurrent: true,
    evaluationUtc: '2026-11-04T03:00:00Z',
    readAtUtc: '2026-11-04T03:00:00Z',
    closeUtc: '2026-11-04T12:30:00Z',
    targetRevisionId: 12,
    range: {
      completeFrom: '2026-10-01',
      completeThrough: '2026-11-04',
      captureStartedAtUtc: '2026-10-01T00:00:00Z',
    },
  };
}

function makeBuilding(
  id: number,
  code: string,
  name: string,
  totalAreas: number,
  values: Record<OverviewGroup, number>,
): OverviewBuilding {
  return {
    id,
    code,
    name,
    entry: { numerator: completeCount(values.entered), denominator: completeCount(totalAreas) },
    submission: { numerator: completeCount(values.submitted), denominator: completeCount(totalAreas) },
    inspection: { numerator: completeCount(values.inspected), denominator: completeCount(totalAreas) },
    groups: makeGroups(values),
  };
}

function makeNotEnteredRow(
  areaId: number,
  buildingId: number,
  buildingCode: string,
  buildingName: string,
  entry: 'covered' | 'unassigned',
): OverviewAreaRow {
  return {
    areaId,
    buildingId,
    code: `AR${areaId}`,
    name: `พื้นที่ทดลอง ${areaId}`,
    buildingCode,
    buildingName,
    entry,
    work: {
      duePoints: 2,
      submittedPoints: 0,
      inspectedPoints: 0,
      submitted: false,
      inspected: false,
      incomplete: false,
      pendingInspection: false,
      noSubmission: true,
      rework: false,
      issue: false,
      overdue: false,
    },
    reasons: [entry, 'notEntered', 'noSubmission'],
    roundKeys: [],
    originsIncomplete: false,
  };
}

function makeSummary(): OverviewSummary {
  const buildingA = makeBuilding(1, 'A', 'ตึกทดลอง A', 60, {
    entered: 55,
    covered: 3,
    unassigned: 2,
    notEntered: 5,
    submitted: 50,
    incomplete: 5,
    inspected: 45,
    pendingInspection: 8,
    noSubmission: 5,
    rework: 2,
    issue: 1,
    overdue: 2,
    upcoming: 0,
    noRounds: 0,
    missedCleaning: 1,
    missedInspection: 1,
  });

  const buildingB = makeBuilding(2, 'B', 'ตึกทดลอง B', 40, {
    entered: 35,
    covered: 2,
    unassigned: 3,
    notEntered: 5,
    submitted: 30,
    incomplete: 5,
    inspected: 25,
    pendingInspection: 7,
    noSubmission: 5,
    rework: 1,
    issue: 1,
    overdue: 2,
    upcoming: 0,
    noRounds: 0,
    missedCleaning: 1,
    missedInspection: 0,
  });

  return {
    context: makeContext(),
    availability: COMPLETE_SECTIONS,
    entry: { numerator: completeCount(90), denominator: completeCount(100) },
    submission: { numerator: completeCount(80), denominator: completeCount(100) },
    inspection: { numerator: completeCount(70), denominator: completeCount(100) },
    groups: makeGroups({
      entered: 90,
      covered: 5,
      unassigned: 5,
      notEntered: 10,
      submitted: 80,
      incomplete: 10,
      inspected: 70,
      pendingInspection: 15,
      noSubmission: 10,
      rework: 3,
      issue: 2,
      overdue: 4,
      upcoming: 0,
      noRounds: 0,
      missedCleaning: 2,
      missedInspection: 1,
    }),
    buildings: [buildingA, buildingB],
  };
}

/**
 * Synthetic 100/90/5/5 overview response with complete availability, matching building sums, and the first five of ten
 * `notEntered` Area rows (starting with Area 91).
 */
export function overviewFixture(overrides: Partial<OverviewAreaList> = {}): OverviewAreaList {
  return {
    summary: makeSummary(),
    group: 'notEntered',
    page: 1,
    pageSize: 5,
    total: completeCount(10),
    rows: [
      makeNotEnteredRow(91, 1, 'A', 'ตึกทดลอง A', 'covered'),
      makeNotEnteredRow(92, 1, 'A', 'ตึกทดลอง A', 'covered'),
      makeNotEnteredRow(93, 1, 'A', 'ตึกทดลอง A', 'covered'),
      makeNotEnteredRow(94, 1, 'A', 'ตึกทดลอง A', 'unassigned'),
      makeNotEnteredRow(95, 1, 'A', 'ตึกทดลอง A', 'unassigned'),
    ],
    ...overrides,
  };
}

/** Synthetic Area 91 detail matching `overviewFixture().rows[0]`, containing only Area 91's facts and points. */
export function overviewDetailFixture(overrides: Partial<OverviewAreaDetail> = {}): OverviewAreaDetail {
  return {
    context: makeContext(),
    availability: COMPLETE_SECTIONS,
    area: makeNotEnteredRow(91, 1, 'A', 'ตึกทดลอง A', 'covered'),
    matchesGroup: true,
    entryEvidence: [],
    currentResponsibility: {
      userId: 201,
      areaId: 91,
      state: 'instructed',
      lastConfirmedAreaId: null,
      confirmedAt: null,
    },
    facts: {
      submissions: [],
      inspections: [],
      attendance: [],
      covers: [
        {
          id: 501,
          areaId: 91,
          userId: 201,
          assignedAt: '2026-11-04T00:15:00Z',
          cancelledAt: null,
        },
      ],
      people: [{ id: 201, displayName: 'แม่บ้านทดลอง 91' }],
      entryEvidence: [],
      assignedAreas: [91],
      originsComplete: true,
      issueStateKnown: true,
    },
    points: [
      {
        work: {
          pointId: 9101,
          isDue: true,
          isSubmitted: false,
          isInspected: false,
          needsRework: false,
          isIssue: false,
          isOverdue: false,
          isUpcoming: false,
          submissionId: null,
          inspectionId: null,
        },
        code: 'AR91-01',
        name: 'จุดตรวจ AR91-1',
        sortOrder: 1,
        roundKey: '2026-11-04:Day:91:9101:12:0900-1030',
        start: '09:00:00',
        end: '10:30:00',
      },
      {
        work: {
          pointId: 9102,
          isDue: true,
          isSubmitted: false,
          isInspected: false,
          needsRework: false,
          isIssue: false,
          isOverdue: false,
          isUpcoming: false,
          submissionId: null,
          inspectionId: null,
        },
        code: 'AR91-02',
        name: 'จุดตรวจ AR91-2',
        sortOrder: 2,
        roundKey: '2026-11-04:Day:91:9102:12:0900-1030',
        start: '09:00:00',
        end: '10:30:00',
      },
    ],
    rounds: [
      {
        roundKey: '2026-11-04:Day:91:9101:12:0700-0800',
        round: {
          pointId: 9101,
          areaId: 91,
          buildingId: 1,
          areaCode: 'AR91',
          areaName: 'พื้นที่ทดลอง 91',
          buildingCode: 'A',
          buildingName: 'ตึกทดลอง A',
          originId: 701,
          start: '07:00:00',
          end: '08:00:00',
          activeFromUtc: '2026-11-04T00:00:00Z',
          finalizedAtUtc: '2026-11-04T01:00:00Z',
          activationRevisionId: 12,
          finalizedReason: 'superseded',
        },
        outcome: 'notDone',
        submissionId: null,
        inspectionId: null,
        isPartial: false,
        unresolvedSubmissions: null,
      },
    ],
    selectedRoundKey: null,
    ...overrides,
  };
}
