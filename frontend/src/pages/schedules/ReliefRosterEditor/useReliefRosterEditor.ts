import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import type { RowProblem, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { offsetDate, weekdayOf, weekdays, type CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import { schedulePoll, useGetManagedScheduleQuery } from '../schedulesApi';
import type { ReliefCell, ReliefRosterEditorProps, ReliefWeekday } from './type';

const baselineKey = (id: number) => `baseline:${id}:relief`;
const cellValue = (cell?: { baselineDayOff: boolean; baselineAreaId?: number }) =>
  cell?.baselineDayOff ? 'off' : String(cell?.baselineAreaId ?? '');

export function useReliefRosterEditor(props: ReliefRosterEditorProps) {
  const tomorrow = offsetDate(clockSlot(new Date()).startDate, 1);
  const [effectiveFrom, setEffectiveFrom] = useState(
    props.week.rows.flatMap((row) => row.cleanerKind === 'Relief' ? row.days : [])
      .map((day) => day.date).filter((date) => date >= tomorrow).sort()[0] ?? tomorrow,
  );
  const [changed, setChanged] = useState<Record<number, Partial<Record<ReliefWeekday, string>>>>({});
  const [versions, setVersions] = useState<Record<number, string>>({});
  const monday = effectiveFrom && offsetDate(effectiveFrom, -((weekdays.indexOf(weekdayOf(effectiveFrom)) + 6) % 7));
  const nextMonday = monday && offsetDate(monday, 7);
  const first = useGetManagedScheduleQuery(monday && monday !== props.week.weekStart
    ? { ...props.selection, weekStart: monday } : skipToken, schedulePoll);
  const next = useGetManagedScheduleQuery(nextMonday && weekdayOf(effectiveFrom) !== 'Monday'
    ? { ...props.selection, weekStart: nextMonday } : skipToken, schedulePoll);
  const current = monday === props.week.weekStart ? props.latest : first.error ? undefined : first.currentData;
  const following = next.error ? undefined : next.currentData;
  const loading = first.isFetching || next.isFetching;
  const renderWeek = current ?? (monday === props.week.weekStart ? props.week : undefined);
  const dates = effectiveFrom ? Array.from({ length: 7 }, (_, index) => offsetDate(effectiveFrom, index)) : [];
  const problems: RowProblem[] = [];
  const problem = (id: number, message: string) => problems.push({ rowId: baselineKey(id), code: 'row-invalid', message });
  function effectiveRow(row: SchedulePerson, week: ScheduleWeek | undefined) {
    return week?.rows.find((entry) => entry.userId === row.userId && entry.cleanerKind === 'Relief'
      && entry.days.some((day) => day.date === effectiveFrom && day.edit?.canChange));
  }
  const initialRows = props.week.rows.filter((row) => row.cleanerKind === 'Relief');
  const visibleRows = initialRows.flatMap((original) => {
    const row = effectiveRow(original, renderWeek);
    if (!row) return changed[original.userId] ? [original] : [];
    return [row];
  });
  const rows = visibleRows.map((row) => {
    const day = row.days.find((entry) => entry.date === effectiveFrom);
    const areas = renderWeek?.destinations?.filter((slot) => slot.date === effectiveFrom && slot.shift === day?.shift)
      .flatMap((slot) => slot.areas.map((area) => ({ id: area.id, label: `${slot.buildingCode} · ${area.label}` }))) ?? [];
    const cells = dates.map((date) => {
      const weekday = weekdayOf(date);
      const facts = [...row.days, ...(following?.rows.find((entry) => entry.userId === row.userId)?.days ?? [])]
        .find((entry) => entry.date === date)?.edit;
      const before = cellValue(facts);
      const value = changed[row.userId]?.[weekday] ?? before;
      return { date, weekday, before, value, known: !!facts?.canChange };
    });
    const workDays = cells.filter((cell) => !!cell.value && cell.value !== 'off').length;
    return { row, areas, cells, workDays, allKnown: cells.every((cell) => cell.known) };
  });
  const submitted = rows.flatMap(({ row, areas, cells }) => {
    const pending = changed[row.userId];
    if (!pending) return [];
    const days = cells.flatMap(({ weekday, value, before, known }): ReliefCell[] => {
      if (!(weekday in pending) || value === before) return [];
      if (!known || (value && value !== 'off' && !areas.some((area) => String(area.id) === value))) {
        problem(row.userId, 'เลือกวันที่และพื้นที่ในขอบเขตที่จัดตารางได้');
      }
      return [{ weekday, areaId: value && value !== 'off' ? Number(value) : undefined, isDayOff: value === 'off' }];
    });
    return days.length ? [{ userId: row.userId, effectiveFrom, days }] : [];
  });
  const command: CommandDraft = { kind: 'set-relief-baseline', payload: { rows: submitted },
    expectedRevisions: Object.fromEntries(submitted.map(({ userId }) => [baselineKey(userId), versions[userId]])) };
  const dirty = Object.keys(changed).length > 0;
  const changing = dirty && Object.entries(versions).some(([id, revision]) =>
    !current?.rows.some((row) => row.userId === Number(id) && effectiveRow(row, current)?.reliefBaselineRevision === revision));
  const list = rows.filter(({ row }) => changed[row.userId]);
  const line = (entry: typeof rows[number], side: 'before' | 'value') => entry.cells
    .filter(({ weekday }) => weekday in changed[entry.row.userId])
    .map((cell) => {
      const value = cell[side];
      const label = !value ? 'ยังไม่กำหนด' : value === 'off' ? 'วันหยุด'
        : entry.areas.find((area) => String(area.id) === value)?.label ?? `พื้นที่ ${value}`;
      return `${formatWeekday(cell.date)} · ${label}`;
    }).join(' / ');
  const beforeAfter = list.map((entry) => ({
    id: baselineKey(entry.row.userId),
    label: `${entry.row.displayName} · เริ่ม ${effectiveFrom ? formatShiftDate(effectiveFrom) : 'ยังไม่เลือกวัน'}`,
    before: line(entry, 'before'), after: line(entry, 'value'),
  }));
  const editor = useScheduleReview({
    command, beforeAfter, problems, dirty, changing, phone: props.phone, onClose: props.onClose,
    ready: !!effectiveFrom && effectiveFrom >= tomorrow && !!current && !loading && submitted.length > 0,
    verifyRetryScope: async () => {
      const week = monday === props.week.weekStart ? await props.reloadEditorWeek() : await first.refetch().unwrap();
      const later = next.isUninitialized ? undefined : await next.refetch().unwrap();
      return submitted.every((row) => {
        const person = week.rows.find((entry) => entry.userId === row.userId);
        const date = person?.days.find((day) => day.date === effectiveFrom);
        return person && effectiveRow(person, week)?.reliefBaselineRevision === versions[row.userId]
          && row.days.every((selection) => [...person.days, ...(later?.rows.find((entry) => entry.userId === row.userId)?.days ?? [])]
            .some((day) => day.date >= effectiveFrom && day.date < offsetDate(effectiveFrom, 7)
              && weekdayOf(day.date) === selection.weekday && day.edit?.canChange))
          && row.days.every((cell) => cell.areaId == null || week.destinations?.some((slot) =>
            slot.date === effectiveFrom && slot.shift === date?.shift && slot.areas.some((area) => area.id === cell.areaId)));
      });
    },
  });
  return {
    ...editor, effectiveFrom, tomorrow, setEffectiveFrom, rows, loading, error: first.error ?? next.error,
    setCell: (person: SchedulePerson, weekday: ReliefWeekday, value: string) => {
      setVersions((currentVersions) => ({ ...currentVersions, [person.userId]: currentVersions[person.userId] ?? person.reliefBaselineRevision }));
      setChanged((currentDraft) => ({ ...currentDraft, [person.userId]: { ...currentDraft[person.userId], [weekday]: value } }));
    },
    compare: () => {
      props.onCompare();
      if (!first.isUninitialized) void first.refetch();
      if (!next.isUninitialized) void next.refetch();
    },
    useLatest: () => {
      if (!current || !list.every(({ row }) => effectiveRow(row, current))) return;
      setVersions(Object.fromEntries(list.map(({ row }) => [row.userId, effectiveRow(row, current)!.reliefBaselineRevision])));
      editor.backToEdit();
    },
    latest: beforeAfter.map((item) => ({ ...item, after: item.before })),
  };
}
