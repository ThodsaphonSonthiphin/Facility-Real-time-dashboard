import { skipToken } from '@reduxjs/toolkit/query/react';
import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../../shared/api/baseApi';
import type { Shift } from '../../../shared/api/types';
import { useGetOverviewAreasQuery } from '../../../shared/overview/overviewApi';
import { areaHref, overviewParams, readOverviewContext } from '../../../shared/overview/overviewContext';
import type {
  Availability,
  OverviewAreaList,
  OverviewBuilding,
  OverviewCount,
  OverviewGroup,
  OverviewListArgs,
  OverviewMetric,
  OverviewSelection,
  OverviewSummary,
  OverviewView,
} from '../../../shared/overview/types';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { shiftLabel, shiftShortLabel } from '../../../shared/utils/labels';
import { formatDate, formatShiftDate, formatTime, thaiToday } from '../../../shared/utils/thaiTime';
import type { AreaResultBadge, AreaResultsProps } from '../AreaResults/type';
import type { BuildingComparisonProps } from '../BuildingComparison/type';
import type { CoveragePanelProps, CoverageRingSegment } from '../CoveragePanel/type';
import type { FollowUpGroupsProps } from '../FollowUpGroups/type';
import type { RoundHistoryProps } from '../RoundHistory/type';
import type { BuildingOption, OverviewPageModel } from './type';

const RING_CIRCUMFERENCE = 2 * Math.PI * 48;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

const SHIFT_OPTIONS: readonly { value: Shift; label: string }[] = [
  { value: 'Day', label: shiftLabel.Day },
  { value: 'Night', label: shiftLabel.Night },
];

interface GroupMeta {
  label: string;
  heading: string;
  view: OverviewView;
  tone: AreaResultBadge['tone'];
}

const GROUP_META: Record<OverviewGroup, GroupMeta> = {
  entered: { label: 'ลงเวลาแล้ว', heading: 'พื้นที่ที่ลงเวลาแล้ว', view: 'attendance', tone: 'passed' },
  covered: { label: 'มอบหมายทำแทนแล้ว', heading: 'พื้นที่ที่มอบหมายทำแทนแล้ว', view: 'attendance', tone: 'pending' },
  unassigned: { label: 'ยังไม่มอบหมายทำแทน', heading: 'พื้นที่ที่ยังไม่มอบหมายทำแทน', view: 'attendance', tone: 'overdue' },
  notEntered: { label: 'ยังไม่ลงเวลา', heading: 'พื้นที่ที่ยังไม่ลงเวลา', view: 'attendance', tone: 'overdue' },
  submitted: { label: 'ส่งงานครบ', heading: 'พื้นที่ที่ส่งงานครบ', view: 'cleaning', tone: 'passed' },
  incomplete: { label: 'ส่งงานยังไม่ครบ', heading: 'พื้นที่ที่ส่งงานยังไม่ครบ', view: 'cleaning', tone: 'overdue' },
  inspected: { label: 'ตรวจครบแล้ว', heading: 'พื้นที่ที่ตรวจครบแล้ว', view: 'inspection', tone: 'pending' },
  pendingInspection: { label: 'มีงานรอตรวจ', heading: 'พื้นที่ที่มีงานรอตรวจ', view: 'inspection', tone: 'pending' },
  noSubmission: { label: 'ยังไม่มีงานให้ตรวจ', heading: 'พื้นที่ที่ยังไม่มีงานให้ตรวจ', view: 'inspection', tone: 'idle' },
  rework: { label: 'มีงานต้องแก้', heading: 'พื้นที่ที่มีงานต้องแก้', view: 'cleaning', tone: 'rework' },
  issue: { label: 'แม่บ้านแจ้งปัญหา', heading: 'พื้นที่ที่แม่บ้านแจ้งปัญหา', view: 'cleaning', tone: 'rework' },
  overdue: { label: 'มีงานเลยเวลา', heading: 'พื้นที่ที่มีงานเลยเวลา', view: 'cleaning', tone: 'overdue' },
  upcoming: { label: 'ยังไม่ถึงรอบ', heading: 'พื้นที่ที่ยังไม่ถึงรอบ', view: 'cleaning', tone: 'idle' },
  noRounds: { label: 'ยังไม่ได้ตั้งรอบในกะนี้', heading: 'พื้นที่ที่ยังไม่ได้ตั้งรอบในกะนี้', view: 'cleaning', tone: 'idle' },
  missedCleaning: { label: 'พื้นที่ที่มีรอบไม่ได้ทำ', heading: 'พื้นที่ที่มีรอบไม่ได้ทำ', view: 'cleaning', tone: 'overdue' },
  missedInspection: { label: 'พื้นที่ที่มีรอบไม่ได้ตรวจ', heading: 'พื้นที่ที่มีรอบไม่ได้ตรวจ', view: 'inspection', tone: 'pending' },
};

const AVAILABILITY_REASONS: Record<string, string> = {
  shiftNotStarted: 'กะนี้ยังไม่เริ่ม',
  targetCoverageMissing: 'ยังไม่มีประวัติเป้าหมายครบทั้งช่วงกะ',
  targetCoverageGap: 'ประวัติเป้าหมายมีช่วงขาดความต่อเนื่อง',
  targetRevisionUnreadable: 'มีประวัติเป้าหมายบางรายการที่อ่านไม่ได้',
  receivingPolicyUnknown: 'ยังไม่มีประวัตินโยบายเวลารับข้อมูลครบทั้งกะ',
  attendancePartlyExpired: 'ข้อมูลลงเวลาบางส่วนพ้นช่วงจัดเก็บแล้ว',
  attendanceExpired: 'ข้อมูลลงเวลาของกะนี้พ้นช่วงจัดเก็บแล้ว',
  workArchived: 'ข้อมูลงานทำความสะอาดและการตรวจอยู่นอกช่วงจัดเก็บรายวันแล้ว',
  issueStateUnknown: 'ไม่สามารถยืนยันสถานะปัญหาของบางจุดได้เนื่องจากพ้นช่วงจัดเก็บ',
  originsIncomplete: 'มีงานส่งเก่าที่ไม่มีรหัสรอบเดิมและจับคู่ได้หลายรอบ',
  buildingOwnershipUnproven: 'ยังไม่มีประวัติเป้าหมายเพื่อยืนยันตึกของพื้นที่ในกะนี้',
};

const ENTRY_BADGES: Record<'entered' | 'covered' | 'unassigned', AreaResultBadge> = {
  entered: { label: 'ลงเวลาแล้ว', tone: 'passed' },
  covered: { label: 'มอบหมายทำแทนแล้ว', tone: 'pending' },
  unassigned: { label: 'ยังไม่มอบหมายทำแทน', tone: 'overdue' },
};

const ENTRY_REASON_KEYS = new Set<string>(['entered', 'covered', 'unassigned']);

function isRealIsoDate(value: string): boolean {
  if (!ISO_DATE.test(value)) {
    return false;
  }
  const timestamp = Date.parse(`${value}T00:00:00Z`);
  return !Number.isNaN(timestamp) && new Date(timestamp).toISOString().slice(0, 10) === value;
}

function formatAvailabilityReasons(availability: Availability): string {
  if (availability.reasons.length === 0) {
    return '';
  }
  return availability.reasons.map((code) => AVAILABILITY_REASONS[code] ?? code).join(' · ');
}

function formatCountValue(count: OverviewCount): string {
  return count.value === null ? '—' : String(count.value);
}

function formatMetricRatio(metric: OverviewMetric): { text: string; ringValueText: string; percent: number | null } {
  if (metric.numerator.value === null || metric.denominator.value === null) {
    return { text: '—', ringValueText: '—', percent: null };
  }
  if (metric.denominator.value === 0) {
    return { text: '0/0', ringValueText: '0/0', percent: null };
  }
  const pct = Math.round((metric.numerator.value / metric.denominator.value) * 100);
  return {
    text: `${metric.numerator.value}/${metric.denominator.value} (${pct}%)`,
    ringValueText: `${pct}%`,
    percent: pct,
  };
}

function buildRingSegments(
  denominator: OverviewCount,
  slices: readonly { key: string; count: OverviewCount; tone: CoverageRingSegment['tone'] }[],
): CoverageRingSegment[] {
  if (denominator.value === null || denominator.value <= 0) {
    return [];
  }
  if (slices.some((slice) => slice.count.value === null)) {
    return [];
  }
  const denom = denominator.value;
  let offset = 0;
  const segments: CoverageRingSegment[] = [];
  for (const slice of slices) {
    const value = slice.count.value ?? 0;
    if (value <= 0) {
      continue;
    }
    const length = (value / denom) * RING_CIRCUMFERENCE;
    segments.push({
      key: slice.key,
      dashArray: `${length.toFixed(2)} ${RING_CIRCUMFERENCE.toFixed(2)}`,
      dashOffset: Number((-offset).toFixed(2)),
      tone: slice.tone,
    });
    offset += length;
  }
  return segments;
}

function formatRoundTimeLabel(roundKey: string, index: number): string {
  const match = /:(\d{2})(\d{2})-(\d{2})(\d{2})$/.exec(roundKey);
  if (match) {
    return `รอบ ${match[1]}:${match[2]}–${match[3]}:${match[4]}`;
  }
  return `รอบที่ ${index + 1}`;
}

function sameBuildingOptions(left: readonly BuildingOption[], right: readonly BuildingOption[]): boolean {
  return left.length === right.length && left.every((item, i) => item.id === right[i].id && item.label === right[i].label);
}

function responseDigest(data: OverviewAreaList): string {
  return JSON.stringify({
    phase: data.summary.context.phase,
    targetRevisionId: data.summary.context.targetRevisionId ?? null,
    entry: data.summary.entry,
    submission: data.summary.submission,
    inspection: data.summary.inspection,
    groups: data.summary.groups,
    page: data.page,
    total: data.total,
    rows: data.rows.map((row) => [row.areaId, row.entry, row.work, row.reasons, row.roundKeys, row.originsIncomplete]),
  });
}

function buildPeriodText(response?: OverviewAreaList): string {
  if (!response) {
    return 'ดูความครอบคลุมการเข้าพื้นที่ การส่งงาน และการตรวจของทั้งกะในหน้าเดียว';
  }
  const { context } = response.summary;
  const shiftWord = shiftShortLabel[context.shift];
  const dateWord = formatShiftDate(context.shiftDate);
  const readWord = formatTime(context.readAtUtc);
  if (context.phase === 'closed') {
    const closedTime = context.closeUtc ? ` (${formatTime(context.closeUtc)} น.)` : '';
    return `${shiftWord} · ${dateWord} · ปิดรับข้อมูลแล้ว${closedTime} · อ่านข้อมูลเมื่อ ${readWord} น.`;
  }
  if (context.phase === 'receiving') {
    const closeText = context.closeUtc ? `ถึง ${formatTime(context.closeUtc)} น.` : 'ยังไม่ทราบเวลาปิดรับ';
    return `${shiftWord} · ${dateWord} · หมดกะแล้ว ยังเปิดรับข้อมูล${closeText} · อ่านข้อมูลเมื่อ ${readWord} น.`;
  }
  if (context.phase === 'notStarted') {
    return `${shiftWord} · ${dateWord} · กะนี้ยังไม่เริ่ม · อ่านข้อมูลเมื่อ ${readWord} น.`;
  }
  return `${shiftWord} · ${dateWord} · กำลังทำงาน · ประเมินเมื่อ ${formatTime(context.evaluationUtc)} น. · อ่านข้อมูลเมื่อ ${readWord} น.`;
}

function buildDateHelpText(response?: OverviewAreaList): string {
  if (!response) {
    return 'ช่วงวันที่ที่เก็บเป้าหมายครบจะแสดงเมื่อโหลดข้อมูลจากระบบ';
  }
  const { range } = response.summary.context;
  if (range.completeFrom && range.completeThrough) {
    return `ข้อมูลครบช่วง ${formatShiftDate(range.completeFrom)}–${formatShiftDate(range.completeThrough)}`;
  }
  if (range.captureStartedAtUtc) {
    return `เริ่มเก็บเป้าหมายตั้งแต่ ${formatDate(range.captureStartedAtUtc)} (ยังไม่มีช่วงที่ครบทั้งกะ)`;
  }
  return 'ยังไม่มีช่วงวันที่ที่เก็บประวัติเป้าหมายครบ';
}

function buildPanels(summary: OverviewSummary, selectedGroup: OverviewGroup): CoveragePanelProps[] {
  const { availability, entry, submission, inspection, groups } = summary;
  const entryRatio = formatMetricRatio(entry);
  const workRatio = formatMetricRatio(submission);
  const inspRatio = formatMetricRatio(inspection);

  const entryDenomText =
    entry.denominator.value !== null
      ? `จาก ${entry.denominator.value} พื้นที่ที่ต้องมีคน`
      : entry.denominator.availability.state === 'notStarted'
        ? 'กะนี้ยังไม่เริ่ม'
        : entry.denominator.observed > 0
          ? `ยืนยันได้ ${entry.denominator.observed} พื้นที่ (ฐานเป้า: —)`
          : 'ไม่สามารถคำนวณฐานเป้าได้';
  const entryExplanation =
    availability.entry.state === 'complete'
      ? 'นับพื้นที่ที่มีหลักฐานเข้างานหรือยืนยันถึงพื้นที่แล้ว (คงนับแม้พักหรือเลิกงาน) ไม่รวมพื้นที่ที่เพียงมอบหมายทำแทน'
      : formatAvailabilityReasons(availability.entry) || 'ข้อมูลการลงเวลายังไม่ครบ';

  const workDenomText =
    submission.denominator.value !== null
      ? `จาก ${submission.denominator.value} พื้นที่ที่มีจุดถึงรอบ`
      : submission.denominator.availability.state === 'notStarted'
        ? 'กะนี้ยังไม่เริ่ม'
        : submission.denominator.observed > 0
          ? `ยืนยันได้ ${submission.denominator.observed} พื้นที่ที่มีรอบ (ฐานงาน: —)`
          : 'ไม่สามารถคำนวณฐานงานที่ถึงรอบได้';
  const workExplanation =
    availability.work.state === 'complete'
      ? submission.denominator.value === 0
        ? 'ยังไม่มีพื้นที่ที่มีจุดถึงรอบในกะนี้ (ฐานงานปัจจุบัน 0 พื้นที่)'
        : 'นับพื้นที่ที่ส่งงานครบทุกจุดในรอบที่ถึงเวลาแล้ว (รายละเอียดจำนวนจุดแสดงในตารางด้านล่าง)'
      : formatAvailabilityReasons(availability.work) || 'ข้อมูลงานยังไม่ครบ';

  const inspDenomText =
    inspection.denominator.value !== null
      ? `จาก ${inspection.denominator.value} พื้นที่ที่มีจุดถึงรอบ`
      : inspection.denominator.availability.state === 'notStarted'
        ? 'กะนี้ยังไม่เริ่ม'
        : inspection.denominator.observed > 0
          ? `ยืนยันได้ ${inspection.denominator.observed} พื้นที่ที่มีรอบ (ฐานงาน: —)`
          : 'ไม่สามารถคำนวณฐานงานที่ถึงรอบได้';
  const inspExplanation =
    availability.inspection.state === 'complete'
      ? inspection.denominator.value === 0
        ? 'ยังไม่มีพื้นที่ที่มีจุดถึงรอบให้ติดตามการตรวจในกะนี้'
        : 'นับพื้นที่ที่ตรวจงานส่งล่าสุดครบทุกจุดแล้ว (รวมผลต้องแก้ไข — งานค้างแก้แสดงแยกในรายการติดตาม)'
      : formatAvailabilityReasons(availability.inspection) || 'ข้อมูลการตรวจยังไม่ครบ';

  return [
    {
      title: 'พื้นที่ที่ลงเวลาเข้าแล้ว',
      metric: {
        text: entryRatio.text,
        ringValueText: entryRatio.ringValueText,
        denominatorText: entryDenomText,
        explanation: entryExplanation,
        segments: buildRingSegments(entry.denominator, [
          { key: 'entered', count: groups.entered, tone: 'passed' },
          { key: 'covered', count: groups.covered, tone: 'pending' },
          { key: 'unassigned', count: groups.unassigned, tone: 'overdue' },
        ]),
      },
      groups: [
        { key: 'entered', label: 'ลงเวลาแล้ว', text: formatCountValue(groups.entered), selected: selectedGroup === 'entered', tone: 'passed' },
        { key: 'covered', label: 'มอบหมายทำแทนแล้ว', text: formatCountValue(groups.covered), selected: selectedGroup === 'covered', tone: 'pending' },
        { key: 'unassigned', label: 'ยังไม่มอบหมายทำแทน', text: formatCountValue(groups.unassigned), selected: selectedGroup === 'unassigned', tone: 'overdue' },
      ],
      onGroup: () => {},
    },
    {
      title: 'พื้นที่ที่ส่งงานครบ',
      metric: {
        text: workRatio.text,
        ringValueText: workRatio.ringValueText,
        denominatorText: workDenomText,
        explanation: workExplanation,
        segments: buildRingSegments(submission.denominator, [
          { key: 'submitted', count: submission.numerator, tone: 'passed' },
        ]),
      },
      groups: [
        { key: 'submitted', label: 'ส่งงานครบ', text: formatCountValue(groups.submitted), selected: selectedGroup === 'submitted', tone: 'passed' },
        { key: 'incomplete', label: 'ส่งงานยังไม่ครบ', text: formatCountValue(groups.incomplete), selected: selectedGroup === 'incomplete', tone: 'overdue' },
        { key: 'upcoming', label: 'ยังไม่ถึงรอบ', text: formatCountValue(groups.upcoming), selected: selectedGroup === 'upcoming', tone: 'idle' },
        { key: 'noRounds', label: 'ยังไม่ได้ตั้งรอบในกะนี้', text: formatCountValue(groups.noRounds), selected: selectedGroup === 'noRounds', tone: 'idle' },
      ],
      onGroup: () => {},
    },
    {
      title: 'พื้นที่ที่ตรวจครบ',
      metric: {
        text: inspRatio.text,
        ringValueText: inspRatio.ringValueText,
        denominatorText: inspDenomText,
        explanation: inspExplanation,
        segments: buildRingSegments(inspection.denominator, [
          { key: 'inspected', count: inspection.numerator, tone: 'pending' },
        ]),
      },
      groups: [
        { key: 'inspected', label: 'ตรวจครบแล้ว', text: formatCountValue(groups.inspected), selected: selectedGroup === 'inspected', tone: 'pending' },
        { key: 'pendingInspection', label: 'มีงานรอตรวจ', text: formatCountValue(groups.pendingInspection), selected: selectedGroup === 'pendingInspection', tone: 'pending' },
        { key: 'noSubmission', label: 'ยังไม่มีงานให้ตรวจ', text: formatCountValue(groups.noSubmission), selected: selectedGroup === 'noSubmission', tone: 'idle' },
      ],
      onGroup: () => {},
    },
  ];
}

function buildFollowUp(summary: OverviewSummary, selectedGroup: OverviewGroup): FollowUpGroupsProps {
  const { phase } = summary.context;
  const { groups } = summary;
  const title =
    phase === 'closed'
      ? 'รายการที่พบในกะที่เลือก'
      : phase === 'notStarted'
        ? 'รายการติดตามในกะนี้'
        : 'พื้นที่ที่ต้องตามตอนนี้';
  return {
    title,
    subtitle: 'กดรายการเพื่อดูพื้นที่ — พื้นที่เดียวอาจอยู่หลายกลุ่ม จึงไม่นำยอดแต่ละกลุ่มมาบวกรวมกัน',
    items: [
      { key: 'notEntered', label: 'ยังไม่ลงเวลา', text: formatCountValue(groups.notEntered), selected: selectedGroup === 'notEntered', tone: 'overdue' },
      { key: 'overdue', label: 'มีงานเลยเวลา', text: formatCountValue(groups.overdue), selected: selectedGroup === 'overdue', tone: 'overdue' },
      { key: 'pendingInspection', label: 'มีงานรอตรวจ', text: formatCountValue(groups.pendingInspection), selected: selectedGroup === 'pendingInspection', tone: 'pending' },
      { key: 'rework', label: 'มีงานต้องแก้', text: formatCountValue(groups.rework), selected: selectedGroup === 'rework', tone: 'rework' },
      { key: 'issue', label: 'แม่บ้านแจ้งปัญหา', text: formatCountValue(groups.issue), selected: selectedGroup === 'issue', tone: 'rework' },
    ],
    onGroup: () => {},
  };
}

function buildBuildings(
  buildings: readonly OverviewBuilding[],
  selectedBuilding: number | undefined,
  selectedGroup: OverviewGroup,
): BuildingComparisonProps {
  const formatCell = (
    building: OverviewBuilding,
    group: 'entered' | 'submitted' | 'inspected',
    label: string,
    metric: OverviewMetric,
    tone: 'passed' | 'pending',
  ) => {
    const ratio = formatMetricRatio(metric);
    return {
      group,
      label,
      text: ratio.text,
      percent: ratio.percent,
      tone,
      ariaLabel: `ตึก ${building.code} · ${building.name} ${label} ${ratio.text}`,
      selected: selectedBuilding === building.id && selectedGroup === group,
    };
  };

  return {
    title: 'เปรียบเทียบแต่ละตึก',
    subtitle: 'กดตัวเลขเพื่อดูพื้นที่ในตึกนั้น (แต่ละตัวเลขใช้ฐานของหมวดนั้น)',
    rows: buildings.map((building) => ({
      id: building.id,
      label: `${building.code} · ${building.name}`,
      cells: [
        formatCell(building, 'entered', 'ลงเวลาแล้ว', building.entry, 'passed'),
        formatCell(building, 'submitted', 'ส่งงานครบ', building.submission, 'passed'),
        formatCell(building, 'inspected', 'ตรวจครบ', building.inspection, 'pending'),
      ],
    })),
    onSelect: () => {},
  };
}

function buildHistory(summary: OverviewSummary, selectedGroup: OverviewGroup): RoundHistoryProps {
  const { phase } = summary.context;
  const { history } = summary.availability;
  const title = phase === 'closed' ? 'ผลรอบในกะนี้' : 'รอบที่ผ่านมาในกะนี้';
  const subtitle =
    phase === 'closed'
      ? 'สรุปทุกรอบในกะที่ปิดรับข้อมูลแล้ว รวมรอบสุดท้ายของกะ'
      : phase === 'receiving'
        ? 'ผลของรอบก่อนที่จบแล้วในกะนี้ รอบสุดท้ายยังเปิดรับข้อมูลและจะสรุปหลังปิดรับ'
        : phase === 'notStarted'
          ? 'กะนี้ยังไม่เริ่ม จึงยังไม่มีผลรอบในกะ'
          : 'ผลของรอบก่อนที่จบหรือเปลี่ยนแล้วในกะนี้ แม้วันนี้มีรอบใหม่แล้ว';

  const availabilityNote =
    history.state === 'partial' || history.state === 'unavailable'
      ? formatAvailabilityReasons(history) || 'ข้อมูลประวัติรอบของกะนี้ยังไม่ครบถ้วน'
      : undefined;

  return {
    title,
    subtitle,
    availabilityNote,
    groups: [
      {
        key: 'missedCleaning',
        label: 'พื้นที่ที่มีรอบไม่ได้ทำ',
        text: formatCountValue(summary.groups.missedCleaning),
        selected: selectedGroup === 'missedCleaning',
      },
      {
        key: 'missedInspection',
        label: 'พื้นที่ที่มีรอบไม่ได้ตรวจ',
        text: formatCountValue(summary.groups.missedInspection),
        selected: selectedGroup === 'missedInspection',
      },
    ],
    details: [
      {
        title: 'รอบที่ไม่ได้แก้ (Rework)',
        description: 'รอบที่หัวหน้าตรวจแล้วสั่งแก้ไขแต่ยังไม่มีงานแก้ส่งกลับก่อนจบรอบ นับแยกจากพื้นที่ที่มีรอบไม่ได้ตรวจ',
      },
      {
        title: 'รอบที่ถูกเปลี่ยนหรือยกเลิก',
        description: 'รอบที่ถูกแก้เวลา ปิดจุด/พื้นที่ หรือเอาออกจากตารางระหว่างกะ จะเก็บเหตุผลที่เปลี่ยนหรือยกเลิกแยกไว้ในหน้ารายละเอียดของพื้นที่นั้น',
      },
    ],
    onGroup: () => {},
  };
}

function buildResults(
  response: OverviewAreaList,
  selection: OverviewSelection,
  href: (areaId: number, view: OverviewView, roundKey?: string) => string,
  refreshing: boolean,
  onPage: (page: number) => void,
): AreaResultsProps {
  const { summary, group, page, pageSize, total, rows } = response;
  const meta = GROUP_META[group];
  const buildingObj = selection.building !== undefined
    ? summary.buildings.find((b) => b.id === selection.building)
    : undefined;
  const buildingWord = buildingObj
    ? `ตึก ${buildingObj.code} · ${buildingObj.name}`
    : selection.building !== undefined
      ? `ตึก #${selection.building}`
      : 'ทุกตึก';
  const contextText = `${formatShiftDate(summary.context.shiftDate)} · ${shiftShortLabel[summary.context.shift]} · ${buildingWord}`;

  const availabilityWarning =
    total.availability.state === 'partial' && rows.length > 0
      ? `แสดงเฉพาะรายการที่ยืนยันได้ (${formatAvailabilityReasons(total.availability) || 'ข้อมูลยังไม่ครบ'})`
      : undefined;

  const first = (page - 1) * pageSize + 1;
  const last = first + rows.length - 1;
  const countText =
    rows.length === 0
      ? total.value === null
        ? '— พื้นที่'
        : '0 พื้นที่'
      : total.value !== null
        ? `แสดง ${first}–${last} จาก ${total.value} พื้นที่`
        : `รายการที่ยืนยันได้ ${first}–${last} (พบ ${total.observed} พื้นที่ · ยอดรวม: —)`;

  const totalCount = total.value ?? total.observed;
  const hasPrevious = page > 1;
  const hasNext = rows.length > 0 && page * pageSize < totalCount;

  let emptyState: AreaResultsProps['emptyState'];
  if (rows.length === 0) {
    if (total.availability.state === 'notStarted') {
      emptyState = {
        kind: 'notStarted',
        title: 'กะนี้ยังไม่เริ่ม',
        detail: 'ยังไม่ถึงเวลาเริ่มกะที่เลือก จึงยังไม่มีรายการพื้นที่ในกลุ่มนี้',
      };
    } else if (total.availability.state === 'unavailable') {
      emptyState = {
        kind: 'unavailable',
        title: 'ไม่มีข้อมูลของหมวดนี้',
        detail: formatAvailabilityReasons(total.availability) || 'ข้อมูลของกะนี้อยู่นอกช่วงที่ระบบจัดเก็บแล้ว',
      };
    } else if (total.availability.state === 'partial') {
      emptyState = {
        kind: 'partial',
        title: 'ไม่พบรายการที่ยืนยันได้ (ยอดรวม: —)',
        detail: formatAvailabilityReasons(total.availability) || 'ข้อมูลของกะนี้ยังไม่ครบ จึงไม่สามารถสรุปจำนวนทั้งหมดได้',
      };
    } else {
      emptyState = {
        kind: 'empty',
        title: 'ไม่มีพื้นที่ในกลุ่มนี้',
        detail: 'ข้อมูลครบถ้วนแล้ว ไม่พบพื้นที่ที่ตรงกับหมวดหมู่ วันที่ กะ และตึกที่เลือก',
      };
    }
  }

  const workHidden = summary.availability.work.state === 'notStarted' || summary.availability.work.state === 'unavailable';
  const entryHidden = summary.availability.entry.state === 'notStarted' || summary.availability.entry.state === 'unavailable';

  return {
    title: meta.heading,
    contextText,
    countText,
    availabilityWarning,
    rows: rows.map((row) => {
      const entryBadge: AreaResultBadge = entryHidden
        ? { label: 'ไม่มีข้อมูลลงเวลา', tone: 'idle' }
        : ENTRY_BADGES[row.entry];
      const reasonBadges: AreaResultBadge[] = row.reasons
        .filter((reason) => !ENTRY_REASON_KEYS.has(reason))
        .map((reason) => {
          const reasonMeta = GROUP_META[reason as OverviewGroup];
          return reasonMeta ? { label: reasonMeta.label, tone: reasonMeta.tone } : { label: reason, tone: 'idle' };
        });

      let pointsText: string;
      if (workHidden) {
        pointsText = '—';
      } else if (row.work.duePoints === 0) {
        pointsText = row.reasons.includes('upcoming')
          ? 'ยังไม่ถึงรอบ'
          : row.reasons.includes('noRounds')
            ? 'ยังไม่มีรอบในกะนี้'
            : '—';
      } else {
        pointsText = `ส่ง ${row.work.submittedPoints}/${row.work.duePoints} · ตรวจ ${row.work.inspectedPoints}/${row.work.duePoints} จุด`;
      }

      const roundLinks =
        row.roundKeys.length > 1
          ? row.roundKeys.map((key, index) => {
              const label = formatRoundTimeLabel(key, index);
              return {
                key,
                label,
                ariaLabel: `${label} ${row.code} ${row.name}`,
                href: href(row.areaId, meta.view, key),
              };
            })
          : [];

      const singleRoundKey = row.roundKeys.length === 1 ? row.roundKeys[0] : undefined;
      return {
        areaId: row.areaId,
        code: row.code,
        name: row.name,
        buildingText: `${row.buildingCode} · ${row.buildingName}`,
        entryBadge,
        reasonBadges,
        originsIncomplete: Boolean(row.originsIncomplete),
        pointsText,
        href: href(row.areaId, meta.view, singleRoundKey),
        viewAriaLabel: `ดูพื้นที่ ${row.code} ${row.name}`,
        singleRoundLabel: singleRoundKey ? formatRoundTimeLabel(singleRoundKey, 0) : undefined,
        roundLinks,
      };
    }),
    emptyState,
    pagination: {
      page,
      hasPrevious,
      hasNext,
      disabled: refreshing,
    },
    onPage,
  };
}

const EMPTY_RESULTS: AreaResultsProps = {
  title: 'รายการพื้นที่',
  contextText: '',
  countText: '',
  rows: [],
  pagination: { page: 1, hasPrevious: false, hasNext: false, disabled: true },
  onPage: () => {},
};

const EMPTY_FOLLOWUP: FollowUpGroupsProps = {
  title: 'พื้นที่ที่ต้องตามตอนนี้',
  subtitle: '',
  items: [],
  onGroup: () => {},
};

const EMPTY_BUILDINGS: BuildingComparisonProps = {
  title: 'เปรียบเทียบแต่ละตึก',
  subtitle: '',
  rows: [],
  onSelect: () => {},
};

const EMPTY_HISTORY: RoundHistoryProps = {
  title: 'รอบที่ผ่านมาในกะนี้',
  subtitle: '',
  groups: [],
  details: [],
  onGroup: () => {},
};

/** Selection, strict URL sync, single query state, and prepared view models for OverviewPage. */
export function useOverviewPage(): OverviewPageModel {
  const [searchParams, setSearchParams] = useSearchParams();
  const contextResult = useMemo(() => readOverviewContext(searchParams), [searchParams]);
  const selection: OverviewSelection = contextResult.ok ? contextResult.context : { group: 'notEntered', page: 1 };

  const queryArgs: OverviewListArgs | typeof skipToken = contextResult.ok
    ? {
        shiftDate: selection.date,
        shift: selection.shift,
        buildingId: selection.building,
        group: selection.group,
        page: selection.page,
        pageSize: 5,
      }
    : skipToken;

  const query = useGetOverviewAreasQuery(queryArgs, { ...freshOnOpen, refetchOnFocus: true });
  const rawResponse = contextResult.ok ? query.currentData : undefined;
  const argsKey = contextResult.ok ? overviewParams(selection).toString() : '';

  // Wait for a fresh settlement before canonicalizing when RTK Query surfaces a cached hit with isFetching=false on Render 1.
  const [seenRequest, setSeenRequest] = useState<{ argsKey: string; staleRequestId?: string }>({
    argsKey,
    staleRequestId: undefined,
  });
  if (seenRequest.argsKey !== argsKey) {
    setSeenRequest({
      argsKey,
      staleRequestId: rawResponse ? query.requestId : undefined,
    });
  } else if (
    seenRequest.staleRequestId !== undefined &&
    !query.isFetching &&
    !query.error &&
    rawResponse &&
    query.requestId !== seenRequest.staleRequestId
  ) {
    setSeenRequest({ argsKey, staleRequestId: undefined });
  }

  const awaitingFreshCache = seenRequest.argsKey === argsKey && seenRequest.staleRequestId !== undefined;
  const isUncanonicalSlot = contextResult.ok && (!selection.date || !selection.shift);
  const freshRawResponse = isUncanonicalSlot && awaitingFreshCache ? undefined : rawResponse;

  const canonicalSelection: OverviewSelection | null = useMemo(() => {
    if (!contextResult.ok || !freshRawResponse || awaitingFreshCache || query.isFetching || query.error) {
      return null;
    }
    const current = contextResult.context;
    const effectivePage = Number.isInteger(freshRawResponse.page) && freshRawResponse.page >= 1 ? freshRawResponse.page : current.page;
    if (current.date && current.shift && effectivePage === current.page) {
      return null;
    }
    return {
      ...current,
      date: current.date ?? freshRawResponse.summary.context.shiftDate,
      shift: current.shift ?? freshRawResponse.summary.context.shift,
      page: effectivePage,
    };
  }, [awaitingFreshCache, contextResult, freshRawResponse, query.error, query.isFetching]);

  const canonicalKey = canonicalSelection ? overviewParams(canonicalSelection).toString() : '';
  const [canonicalSeed, setCanonicalSeed] = useState<{ key: string; data: OverviewAreaList } | null>(null);
  if (freshRawResponse && canonicalSelection && canonicalKey !== argsKey) {
    if (!canonicalSeed || canonicalSeed.key !== canonicalKey || canonicalSeed.data !== freshRawResponse) {
      setCanonicalSeed({ key: canonicalKey, data: freshRawResponse });
    }
  } else if (canonicalSeed && (canonicalSeed.key !== argsKey || freshRawResponse !== undefined)) {
    setCanonicalSeed(null);
  }

  const isCanonicalizing = freshRawResponse === undefined && canonicalSeed !== null && canonicalSeed.key === argsKey;
  const response = freshRawResponse ?? (canonicalSeed && canonicalSeed.key === argsKey ? canonicalSeed.data : undefined);

  // Replace server-default slot once and replace stale server-clamped page without a request loop or loading flash.
  useEffect(() => {
    if (!canonicalSelection) {
      return;
    }
    setSearchParams(overviewParams(canonicalSelection), { replace: true });
  }, [canonicalSelection, setSearchParams]);

  const [seenData, setSeenData] = useState<{ argsKey: string; digest: string; changed: boolean }>({
    argsKey,
    digest: '',
    changed: false,
  });
  if (seenData.argsKey !== argsKey) {
    setSeenData({ argsKey, digest: '', changed: false });
  } else if (response) {
    const nextDigest = responseDigest(response);
    if (seenData.digest === '') {
      setSeenData({ argsKey, digest: nextDigest, changed: false });
    } else if (seenData.digest !== nextDigest) {
      setSeenData({ argsKey, digest: nextDigest, changed: true });
    }
  }
  const changed = seenData.argsKey === argsKey && seenData.changed;

  const effectiveSelection: OverviewSelection = contextResult.ok
    ? {
        ...selection,
        date: selection.date ?? response?.summary.context.shiftDate,
        shift: selection.shift ?? response?.summary.context.shift,
      }
    : { group: 'notEntered', page: 1 };

  // Retain full building options from unfiltered reads of the same slot so the picker keeps all choices after selecting one building.
  const activeSlotKey =
    effectiveSelection.date && effectiveSelection.shift
      ? `${effectiveSelection.date}:${effectiveSelection.shift}`
      : '';
  const [buildingCache, setBuildingCache] = useState<{ slotKey: string; options: BuildingOption[] } | null>(null);
  if (response && selection.building === undefined && activeSlotKey) {
    const nextOptions = response.summary.buildings.map((b) => ({ id: b.id, label: `${b.code} · ${b.name}` }));
    if (!buildingCache || buildingCache.slotKey !== activeSlotKey || !sameBuildingOptions(buildingCache.options, nextOptions)) {
      setBuildingCache({ slotKey: activeSlotKey, options: nextOptions });
    }
  }

  const loading = contextResult.ok && response === undefined && query.isFetching;
  const refreshing = contextResult.ok && response !== undefined && query.isFetching && !isCanonicalizing;
  const error = !contextResult.ok
    ? contextResult.message
    : query.error
      ? getErrorMessage(query.error)
      : undefined;

  const updateSelection = (next: OverviewSelection) => {
    setSearchParams(overviewParams(next));
  };

  const onDate = (date: string) => {
    if (!contextResult.ok || !isRealIsoDate(date)) {
      return;
    }
    const shift = effectiveSelection.shift;
    if (!shift) {
      return;
    }
    updateSelection({ ...effectiveSelection, date, shift, page: 1 });
  };

  const onShift = (shift: Shift) => {
    if (!contextResult.ok) {
      return;
    }
    const date = effectiveSelection.date;
    if (!date) {
      return;
    }
    updateSelection({ ...effectiveSelection, date, shift, page: 1 });
  };

  const onBuilding = (id?: number) => {
    if (!contextResult.ok) {
      return;
    }
    updateSelection({ ...effectiveSelection, building: id, page: 1 });
  };

  const onGroup = (group: OverviewGroup) => {
    if (!contextResult.ok) {
      return;
    }
    updateSelection({ ...effectiveSelection, group, page: 1 });
  };

  const onBuildingGroup = (buildingId: number, group: OverviewGroup) => {
    if (!contextResult.ok) {
      return;
    }
    updateSelection({ ...effectiveSelection, building: buildingId, group, page: 1 });
  };

  const onPage = (page: number) => {
    if (!contextResult.ok || !Number.isInteger(page) || page < 1) {
      return;
    }
    updateSelection({ ...effectiveSelection, page });
  };

  const onResetToCurrent = () => {
    if (!contextResult.ok) {
      return;
    }
    updateSelection({
      building: effectiveSelection.building,
      group: effectiveSelection.group,
      page: 1,
    });
  };

  const retry = () => {
    if (contextResult.ok) {
      void query.refetch();
    }
  };

  const href = (areaId: number, view: OverviewView, roundKey?: string) =>
    areaHref(effectiveSelection, areaId, view, roundKey);

  const cachedOptions = buildingCache && buildingCache.slotKey === activeSlotKey ? buildingCache.options : [];
  const currentBuildingOptions: BuildingOption[] = response
    ? response.summary.buildings.map((b) => ({ id: b.id, label: `${b.code} · ${b.name}` }))
    : [];
  const buildingOptions = cachedOptions.length >= currentBuildingOptions.length ? cachedOptions : currentBuildingOptions;

  const readAtText = response ? formatTime(response.summary.context.readAtUtc) : undefined;
  const results = response ? buildResults(response, selection, href, refreshing, onPage) : EMPTY_RESULTS;
  const panels = response
    ? buildPanels(response.summary, response.group).map((panel) => ({ ...panel, onGroup }))
    : [];
  const followUp = response ? { ...buildFollowUp(response.summary, response.group), onGroup } : EMPTY_FOLLOWUP;
  const buildings = response
    ? { ...buildBuildings(response.summary.buildings, selection.building, response.group), onSelect: onBuildingGroup }
    : EMPTY_BUILDINGS;
  const history = response ? { ...buildHistory(response.summary, response.group), onGroup } : EMPTY_HISTORY;

  const statusAnnouncement = response
    ? `${results.title} · ${results.countText} · อ่านข้อมูลเมื่อ ${readAtText} น.${changed ? ' (อัปเดตข้อมูลแล้ว)' : ''}`
    : '';

  return {
    selection,
    response,
    loading,
    refreshing,
    error,
    changed,
    onDate,
    onShift,
    onBuilding,
    onGroup,
    onPage,
    retry,
    href,

    invalidContext: !contextResult.ok,
    periodText: buildPeriodText(response),
    readAtText,
    updatedNotice: changed && readAtText ? `อัปเดตข้อมูลชุดล่าสุดแล้วเมื่อ ${readAtText} น.` : undefined,
    notStartedMessage:
      response?.summary.context.phase === 'notStarted'
        ? 'ยังไม่ถึงเวลาเริ่มกะที่เลือก จึงยังไม่มีข้อมูลการเข้าพื้นที่ การส่งงาน หรือการตรวจ'
        : undefined,
    dateInputValue: effectiveSelection.date ?? '',
    dateMaxValue: response ? thaiToday(new Date(response.summary.context.readAtUtc)) : thaiToday(),
    dateHelpText: buildDateHelpText(response),
    shiftValue: effectiveSelection.shift,
    shiftOptions: SHIFT_OPTIONS,
    buildingValue: selection.building !== undefined ? String(selection.building) : '',
    buildingOptions,
    controlsDisabled: !contextResult.ok || !effectiveSelection.date,
    canResetToCurrent: Boolean(response && !response.summary.context.isClockCurrent),
    onResetToCurrent,
    onBuildingGroup,
    panels,
    followUp,
    buildings,
    history,
    results,
    statusAnnouncement,
  };
}
