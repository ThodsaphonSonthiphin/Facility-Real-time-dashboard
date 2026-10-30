import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';
import {
  checkRange, lastSevenDays, matchesPreset, presetRange, rangePresets, rangeTexts,
  readBuildingId, readRange, toRangeParams, type DateRange, type RangePreset,
} from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { useGetInspectionSummaryQuery } from './summaryApi';
import { SUMMARY_MAX_DAYS } from './summaryRules';

const rangeKey = (range: DateRange | undefined) => range ? `${range.from}_${range.to}` : '';

/** Query/date handlers live beside the page; the server remains the authority for every tally. */
export function useInspectionSummaryPage() {
  const [params, setParams] = useSearchParams();
  const asked = readRange(params);
  const buildingId = readBuildingId(params);
  const { currentData: summary, error, isFetching, refetch } = useGetInspectionSummaryQuery({ ...asked, buildingId }, freshOnOpen);
  const today = thaiToday();
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(today)));
  const [problem, setProblem] = useState<string | null>(null);
  const [shift, setShift] = useState<Shift | ''>('');
  const [custom, setCustom] = useState(false);
  const [controlsOpen, setControlsOpen] = useState(Boolean(buildingId || asked));
  const [seenRange, setSeenRange] = useState(rangeKey(asked));
  if (seenRange !== rangeKey(asked)) {
    setSeenRange(rangeKey(asked));
    setTexts(rangeTexts(asked ?? lastSevenDays(today)));
    setProblem(null);
    setCustom(false);
  }
  const go = (range: DateRange | undefined, building: number | undefined) => setParams(toRangeParams(range, building));
  const show = () => {
    const range = checkRange(texts, SUMMARY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    setCustom(false);
    if (asked && rangeKey(asked) === rangeKey(range)) {
      void refetch();
      return;
    }
    go(range, buildingId);
  };
  const selectPreset = (value: string) => {
    if (value === 'custom') {
      setCustom(true);
      setControlsOpen(true);
      return;
    }
    setCustom(false);
    setProblem(null);
    go(presetRange(value as RangePreset, today), buildingId);
  };
  const appliedTexts = rangeTexts(asked ?? lastSevenDays(today));
  const preset = custom ? 'custom' : rangePresets.find((value) => matchesPreset(appliedTexts, value, today)) ?? 'custom';
  const period = summary ? `${formatShiftDate(summary.from)}–${formatShiftDate(summary.to)}` :
    `${formatShiftDate((asked ?? lastSevenDays(today)).from)}–${formatShiftDate((asked ?? lastSevenDays(today)).to)}`;
  const week = lastSevenDays(today);
  return {
    summary, error, isFetching, refetch, texts, setTexts, problem, shift, setShift, preset, selectPreset,
    controlsOpen, setControlsOpen, buildingId, show,
    selectBuilding: (building: number | undefined) => go(asked, building),
    rows: summary?.rows.filter((row) => !shift || row.shift === shift) ?? [],
    tableKey: `${rangeKey(asked)}_${buildingId ?? ''}_${shift}_${summary?.from}_${summary?.to}`,
    rangeLine: `แอดมินเท่านั้น · ${asked ? 'ช่วงที่เลือก' : '7 วันล่าสุด'} (${period})`,
    weekLabel: `7 วันล่าสุด (${formatShiftDate(week.from)}–${formatShiftDate(week.to)})`,
    customLabel: preset === 'custom' && !custom ? `กำหนดเอง (${period})` : 'กำหนดเอง',
  };
}
