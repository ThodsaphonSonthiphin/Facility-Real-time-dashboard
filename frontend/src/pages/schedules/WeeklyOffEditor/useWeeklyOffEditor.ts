import { useState } from 'react';
import type { RowProblem, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { offsetDate, type CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import { schedulePoll, useGetManagedScheduleQuery } from '../schedulesApi';
import type { WeeklyOffDraft, WeeklyOffEditorProps } from './type';

function offDates(person: SchedulePerson) {
  return person.days.filter((day) => day.edit?.weeklyOff).map((day) => day.date);
}

function savedDraft(week: ScheduleWeek): WeeklyOffDraft {
  return {
    weekStart: week.weekStart,
    choices: Object.fromEntries(week.rows.filter((person) => person.hasWeeklyChoice)
      .map((person) => [person.userId, offDates(person)])),
  };
}

export function copyOffChoices(
  target: WeeklyOffDraft, source: WeeklyOffDraft, roster: readonly SchedulePerson[],
): { draft: WeeklyOffDraft; missingSourceIds: number[] } {
  const choices = { ...target.choices };
  const missingSourceIds: number[] = [];
  for (const person of roster) {
    if (choices[person.userId] !== undefined) continue;
    const previous = source.choices[person.userId];
    const mapped = previous?.map((date) => offsetDate(target.weekStart,
      (Date.parse(date) - Date.parse(source.weekStart)) / 86_400_000));
    const allowed = person.days.filter((day) => day.edit?.canChange).map((day) => day.date);
    if (!mapped || (mapped.length === 0 && allowed.length !== 7) || mapped.some((day) => !allowed.includes(day))) {
      missingSourceIds.push(person.userId);
      continue;
    }
    choices[person.userId] = mapped;
  }
  return { draft: { ...target, choices }, missingSourceIds };
}

function describeDates(dates: string[] | undefined) {
  return dates === undefined ? 'ยังไม่กำหนด'
    : dates.length === 0 ? 'ไม่มีวันหยุดที่เลือก'
      : dates.map((day) => `${formatWeekday(day)} ${formatShiftDate(day)}`).join(' · ');
}

export function useWeeklyOffEditor(props: WeeklyOffEditorProps) {
  const [base, setBase] = useState(props.week);
  const [draft, setDraft] = useState(() => savedDraft(props.week));
  const [reason, setReason] = useState('');
  const [missing, setMissing] = useState<number[]>([]);
  const previousWeek = offsetDate(base.weekStart, -7);
  const source = useGetManagedScheduleQuery({ ...props.selection, weekStart: previousWeek }, schedulePoll);
  const copySource = source.isSuccess && !source.isFetching && !source.isError
    && source.currentData?.weekStart === previousWeek ? source.currentData : undefined;
  const saved = savedDraft(base).choices;
  const submitted = base.rows.flatMap((person) => {
    const choices = draft.choices[person.userId];
    return choices !== undefined && JSON.stringify(choices) !== JSON.stringify(saved[person.userId])
      ? [{ userId: person.userId, offDates: choices }] : [];
  });
  const today = clockSlot(new Date()).startDate;
  const needsReason = submitted.some((row) =>
    [...row.offDates, ...(saved[row.userId] ?? [])].some((date) => date < today));
  const problems: RowProblem[] = needsReason && !reason.trim()
    ? [{ rowId: 'reason', code: 'row-invalid', message: 'ระบุเหตุผลสำหรับการแก้ไขวันหยุดย้อนหลัง' }] : [];

  const keyOf = (id: number) => `week:${id}:${base.weekStart}`;
  const viewedRows = base.rows.map((person) => {
    const current = draft.choices[person.userId];
    const editable = (person.weeklyReplaceAllowed ?? person.days.length === 7)
      && person.days.some((day) => day.edit?.canChange);
    const allDatesEditable = person.days.filter((day) => day.edit?.canChange).length === 7;
    if (submitted.some((row) => row.userId === person.userId)
      && (!editable || (!current?.length && !person.hasWeeklyChoice && !allDatesEditable))) {
      problems.push({ rowId: keyOf(person.userId), code: 'row-invalid', message: `ตรวจวันหยุดของ ${person.displayName}` });
    }

    return {
      person, current, editable, allDatesEditable,
      missingSource: missing.includes(person.userId),
      incomplete: current === undefined || current.length !== 1
        || person.days.filter((day) => !current.includes(day.date) && !day.isLeave
          && !day.edit?.baselineDayOff && day.area.value != null && day.breakGroup.value != null
          && !day.problems.length).length < 6,
      setChoice: (date: string, checked: boolean) => {
        const choices = new Set(current ?? []);
        if (checked) choices.add(date);
        else choices.delete(date);
        setDraft({ ...draft, choices: { ...draft.choices, [person.userId]: [...choices].sort() } });
      },
      confirmNoOff: () => setDraft({ ...draft, choices: { ...draft.choices, [person.userId]: [] } }),
    };
  });

  function lines(week: ScheduleWeek) {
    const current = savedDraft(week).choices;
    return week.rows.filter((person) => submitted.some((entry) => entry.userId === person.userId))
      .map((person) => ({
        id: keyOf(person.userId), label: person.displayName,
        before: describeDates(current[person.userId]), after: describeDates(draft.choices[person.userId]),
      }));
  }

  const command: CommandDraft = {
    kind: 'set-weekly-days-off',
    expectedRevisions: Object.fromEntries(base.rows.filter((person) => submitted.some((entry) => entry.userId === person.userId))
      .map((person) => [keyOf(person.userId), person.weeklyRevision])),
    payload: { weekStart: base.weekStart, rows: submitted, reason: reason.trim() || undefined },
  };
  function scopeUnchanged(week: ScheduleWeek | undefined) {
    if (!week || week.weekStart !== base.weekStart || week.revision !== base.revision) return false;
    return submitted.every(({ userId, offDates }) => {
      const person = week.rows.find((row) => row.userId === userId);
      if (!person || person.weeklyReplaceAllowed === false) return false;
      const writable = person.days.filter((day) => day.edit?.canChange);
      return (person.hasWeeklyChoice || !!offDates.length || writable.length === 7)
        && offDates.every((date) => writable.some((day) => day.date === date));
    });
  }
  const editor = useScheduleReview({
    command, dirty: submitted.length > 0, problems, beforeAfter: lines(base),
    phone: props.phone, onClose: props.onClose, changing: !scopeUnchanged(props.latest),
    verifyRetryScope: async () => scopeUnchanged(await props.reloadEditorWeek()),
  });
  return {
    ...editor, draft, reason, setReason, needsReason, viewedRows, previousWeek, targetWeek: base.weekStart,
    sourceLoading: source.isFetching,
    copyAvailable: !!copySource?.rows.length,
    latest: props.latest && (editor.ui !== 'conflict' || props.latest.revision !== base.revision)
      ? lines(props.latest) : undefined,
    useLatest: () => {
      if (!props.latest) return;
      const current = savedDraft(props.latest);
      const choices = { ...current.choices };
      for (const row of submitted) {
        if (props.latest.rows.some((person) => person.userId === row.userId)) choices[row.userId] = row.offDates;
      }
      setDraft({ ...current, choices });
      setBase(props.latest);
      editor.backToEdit();
    },
    copy: () => {
      if (!copySource?.rows.length) return;
      const completeSource = {
        ...copySource,
        rows: copySource.rows.filter((person) => person.weeklyReplaceAllowed ?? person.days.length === 7),
      };
      const result = copyOffChoices(draft, savedDraft(completeSource), viewedRows.filter((row) => row.editable).map((row) => row.person));
      setDraft(result.draft);
      setMissing(result.missingSourceIds);
    },
  };
}
