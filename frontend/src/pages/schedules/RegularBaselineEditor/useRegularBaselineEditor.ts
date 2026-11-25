import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import type { RowProblem, ScheduleDay, SchedulePerson, ScheduleWeek } from '../../../shared/api/scheduleTypes';
import type { Shift } from '../../../shared/api/types';
import { shiftShortLabel } from '../../../shared/utils/labels';
import { formatThaiDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { offsetDate, weekdayOf, weekdays, type CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import { schedulePoll, useGetManagedScheduleQuery } from '../schedulesApi';
import type { RegularBaselineDraft, RegularBaselineEditorProps } from './type';

function areasFor(
  targetWeek: ScheduleWeek | undefined,
  current: ScheduleDay | undefined,
  effectiveFrom: string,
  shift: Shift | '',
  homeBuildingId: number | null | undefined,
) {
  if (shift && targetWeek?.destinations) {
    return targetWeek.destinations
      .filter(
        (dest) =>
          dest.date === effectiveFrom &&
          dest.shift === shift &&
          (homeBuildingId == null || dest.buildingId === homeBuildingId),
      )
      .flatMap((dest) => dest.areas);
  }
  return current?.edit?.areas ?? [];
}

export function useRegularBaselineEditor(props: RegularBaselineEditorProps) {
  const today = clockSlot(new Date()).startDate;
  const hasBaseline = props.person.days.some((day) => day.edit?.baselineAreaId != null);
  const initialDay = props.person.days.find((day) => day.date >= today && day.edit?.canChange);
  const initial: RegularBaselineDraft = {
    effectiveFrom: initialDay?.date ?? today,
    shift: initialDay?.shift ?? '',
    areaId: String(initialDay?.edit?.baselineAreaId ?? ''),
    breakGroup: String(initialDay?.edit?.baselineBreakGroup ?? ''),
  };
  const [version, setVersion] = useState(props.person.regularBaselineRevision);
  const [base, setBase] = useState(props.person);
  const [draft, setDraft] = useState(initial);
  const [edited, setEdited] = useState(false);
  const [reviewed, setReviewed] = useState<SchedulePerson | undefined>();
  const { effectiveFrom, shift, areaId, breakGroup } = draft;
  const targetMonday = effectiveFrom && offsetDate(effectiveFrom, -((weekdays.indexOf(weekdayOf(effectiveFrom)) + 6) % 7));
  const needsTargetFetch = Boolean(targetMonday && (targetMonday !== props.week.weekStart || props.selection.shift));
  const target = useGetManagedScheduleQuery(
    needsTargetFetch ? { weekStart: targetMonday, buildingId: props.selection.buildingId } : skipToken,
    schedulePoll,
  );
  const following = useGetManagedScheduleQuery(targetMonday && weekdayOf(effectiveFrom) !== 'Monday'
    ? { weekStart: offsetDate(targetMonday, 7), buildingId: props.selection.buildingId } : skipToken, schedulePoll);
  const live = props.latest?.rows.find((row) => row.userId === props.person.userId);
  const targetWeek = !needsTargetFetch
    ? props.latest : target.isSuccess && !target.isFetching ? target.currentData : undefined;
  const effectiveRow = targetWeek?.rows.find((row) => row.userId === props.person.userId);
  const current = effectiveRow?.days.find((day) => day.date === effectiveFrom);
  const incoming = effectiveRow ?? live;
  const changes = incoming?.regularBaselineRevision !== version || !live || !!target.error;
  const areas = areasFor(targetWeek, current, effectiveFrom, shift, props.person.homeBuildingId);
  const selectedArea = areas.find((area) => String(area.id) === areaId);
  const key = `baseline:${props.person.userId}:regular`;
  const problems: RowProblem[] = [];
  const problem = (message: string) => problems.push({ rowId: key, code: 'row-invalid', message });
  if (effectiveFrom < today) problem('เลือกวันเริ่มใช้ตั้งแต่วันนี้ขึ้นไป');
  if (effectiveFrom && !target.isFetching && !current?.edit?.canChange) problem('ไม่พบวันที่แก้ได้ในขอบเขตปัจจุบัน');
  if (areaId && current?.edit?.canChange && !selectedArea) problem('เลือกพื้นที่ที่ยังใช้งานในตึกของแม่บ้าน');
  if (edited && (!shift || !areaId)) problem('เลือกกะและพื้นที่ก่อนตรวจสอบ');

  const days = Array.from({ length: 7 }, (_, offset) => {
    const weekday = weekdays[(offset + 1) % 7];
    return { weekday, areaId: Number(areaId), breakGroup: breakGroup ? Number(breakGroup) : null };
  });
  const command: CommandDraft = {
    kind: 'set-regular-baseline',
    expectedRevisions: { [key]: version },
    payload: { userId: props.person.userId, effectiveFrom, shift, days },
  };
  const line = (person: SchedulePerson) => {
    const knownDay = person.days.find((day) => day.date === effectiveFrom);
    const shiftText = shift ? shiftShortLabel[shift] : 'ยังไม่เลือกกะ';
    const breakText = breakGroup ? `กลุ่มพัก ${breakGroup}` : 'ยังไม่กำหนดกลุ่มพัก';
    return {
      id: key,
      label: `${person.displayName} · เริ่ม ${effectiveFrom ? formatThaiDate(effectiveFrom) : 'ยังไม่เลือกวัน'}`,
      before: knownDay?.edit?.baselineAreaId == null
        ? 'ยังไม่พบพื้นที่ในตารางประจำของวันที่เลือก'
        : `พื้นที่ ${knownDay.edit.baselineAreaId} · กลุ่มพัก ${knownDay.edit.baselineBreakGroup ?? 'ยังไม่กำหนด'}`,
      after: `${shiftText} · ${selectedArea?.label ?? 'ยังไม่เลือกพื้นที่'} · ${breakText} ทุกวัน`,
    };
  };
  const beforeAfter = [line(reviewed
    ?? (effectiveRow?.regularBaselineRevision === version && effectiveRow ? effectiveRow : base))];
  const liveLines = incoming && live ? [line(incoming)] : undefined;
  const editor = useScheduleReview({
    command, dirty: edited, problems, beforeAfter, phone: props.phone, onClose: props.onClose,
    changing: changes || Boolean(effectiveFrom && !target.isFetching && !current?.edit?.canChange),
    verifyRetryScope: async () => {
      const week = !needsTargetFetch
        ? await props.reloadEditorWeek() : await target.refetch().unwrap();
      const person = week.rows.find((row) => row.userId === props.person.userId);
      return person?.regularBaselineRevision === version
        && !!person.days.find((day) => day.date === effectiveFrom)?.edit?.canChange;
    },
  });
  const affected = [
    ...(effectiveRow?.days ?? []),
    ...(following.currentData?.rows.find((row) => row.userId === props.person.userId)?.days ?? []),
  ].filter((day) => effectiveFrom && day.date >= effectiveFrom && day.date < offsetDate(effectiveFrom, 7))
    .flatMap((day) => day.problems.map((entry) => ({
      ...entry, label: `${formatWeekday(day.date)} ${formatThaiDate(day.date)} · ${entry.message}`,
    })));
  return {
    ...editor, draft, areas, today, hasBaseline, affected,
    latest: editor.ui !== 'conflict' || changes ? liveLines : undefined,
    loading: target.isFetching,
    review: () => {
      if (!editor.canReview || !effectiveRow) return;
      setReviewed(effectiveRow);
      editor.review();
    },
    setField: (field: keyof RegularBaselineDraft, value: string) => {
      if (field === 'shift') {
        const nextShift = value as Shift | '';
        const nextAreas = areasFor(targetWeek, current, effectiveFrom, nextShift, props.person.homeBuildingId);
        const keepArea = nextAreas.some((area) => String(area.id) === draft.areaId);
        setDraft({ ...draft, shift: nextShift, areaId: keepArea ? draft.areaId : '' });
      } else {
        setDraft({ ...draft, [field]: value });
      }
      if (field === 'effectiveFrom') setReviewed(undefined);
      setEdited(true);
    },
    compare: () => {
      props.onCompare();
      if (needsTargetFetch) void target.refetch();
    },
    useLatest: () => {
      if (!incoming || !live) return;
      setBase(incoming);
      setVersion(incoming.regularBaselineRevision);
      setReviewed(undefined);
      editor.backToEdit();
    },
  };
}
