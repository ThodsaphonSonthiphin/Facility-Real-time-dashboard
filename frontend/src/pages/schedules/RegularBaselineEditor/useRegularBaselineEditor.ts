import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import type { RowProblem, SchedulePerson } from '../../../shared/api/scheduleTypes';
import { formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { offsetDate, weekdayOf, weekdays, type CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import { schedulePoll, useGetManagedScheduleQuery } from '../schedulesApi';
import type { RegularBaselineDraft, RegularBaselineEditorProps } from './type';

export function useRegularBaselineEditor(props: RegularBaselineEditorProps) {
  const tomorrow = offsetDate(clockSlot(new Date()).startDate, 1);
  const initialDay = props.person.days.find((day) => day.date >= tomorrow && day.edit?.canChange);
  const initial: RegularBaselineDraft = {
    effectiveFrom: initialDay?.date ?? tomorrow,
    areaId: String(initialDay?.edit?.baselineAreaId ?? ''),
    breakGroup: String(initialDay?.edit?.baselineBreakGroup ?? ''),
  };
  const [version, setVersion] = useState(props.person.regularBaselineRevision);
  const [base, setBase] = useState(props.person);
  const [draft, setDraft] = useState(initial);
  const [edited, setEdited] = useState(false);
  const { effectiveFrom, areaId, breakGroup } = draft;
  const targetMonday = effectiveFrom && offsetDate(effectiveFrom, -((weekdays.indexOf(weekdayOf(effectiveFrom)) + 6) % 7));
  const target = useGetManagedScheduleQuery(targetMonday && targetMonday !== props.week.weekStart
    ? { ...props.selection, weekStart: targetMonday } : skipToken, schedulePoll);
  const following = useGetManagedScheduleQuery(targetMonday && weekdayOf(effectiveFrom) !== 'Monday'
    ? { ...props.selection, weekStart: offsetDate(targetMonday, 7) } : skipToken, schedulePoll);
  const live = props.latest?.rows.find((row) => row.userId === props.person.userId);
  const effectiveRow = targetMonday === props.week.weekStart
    ? live : target.currentData?.rows.find((row) => row.userId === props.person.userId);
  const current = effectiveRow?.days.find((day) => day.date === effectiveFrom);
  const incoming = effectiveRow ?? live;
  const changes = incoming?.regularBaselineRevision !== version || !live || !!target.error;
  const areas = current?.edit?.areas ?? [];
  const selectedArea = areas.find((area) => String(area.id) === areaId);
  const key = `baseline:${props.person.userId}:regular`;
  const problems: RowProblem[] = [];
  const problem = (message: string) => problems.push({ rowId: key, code: 'row-invalid', message });
  if (effectiveFrom < tomorrow) problem('เลือกวันเริ่มใช้ตั้งแต่วันถัดไป');
  if (effectiveFrom && !current?.edit?.canChange) problem('ไม่พบวันที่แก้ได้ในขอบเขตปัจจุบัน');
  if (areaId && current?.edit?.canChange && !selectedArea) problem('เลือกพื้นที่ที่ยังใช้งานในตึกของแม่บ้าน');
  if (edited && (!areaId || !breakGroup)) problem('เลือกพื้นที่และกลุ่มพักก่อนตรวจสอบ');

  const days = Array.from({ length: 7 }, (_, offset) => {
    const weekday = weekdays[(offset + 1) % 7];
    return { weekday, areaId: Number(areaId), breakGroup: Number(breakGroup) };
  });
  const command: CommandDraft = {
    kind: 'set-regular-baseline',
    expectedRevisions: { [key]: version },
    payload: { userId: props.person.userId, effectiveFrom, days },
  };
  const line = (person: SchedulePerson) => {
    const knownDay = person.days.find((day) => day.date === effectiveFrom);
    return {
      id: key,
      label: `${person.displayName} · เริ่ม ${effectiveFrom ? formatShiftDate(effectiveFrom) : 'ยังไม่เลือกวัน'}`,
      before: knownDay?.edit?.baselineAreaId == null
        ? 'ยังไม่พบพื้นที่ในตารางประจำของวันที่เลือก'
        : `พื้นที่ ${knownDay.edit.baselineAreaId} · กลุ่มพัก ${knownDay.edit.baselineBreakGroup ?? 'ยังไม่กำหนด'}`,
      after: `${selectedArea?.label ?? 'ยังไม่เลือกพื้นที่'} · กลุ่มพัก ${breakGroup || 'ยังไม่กำหนด'} ทุกวัน`,
    };
  };
  const beforeAfter = [line(base)];
  const liveLines = incoming && live ? [line(incoming)] : undefined;
  const editor = useScheduleReview({
    command, dirty: edited, problems, beforeAfter, phone: props.phone, onClose: props.onClose, changing: changes,
  });
  const affected = [
    ...(effectiveRow?.days ?? []),
    ...(following.currentData?.rows.find((row) => row.userId === props.person.userId)?.days ?? []),
  ].filter((day) => effectiveFrom && day.date >= effectiveFrom && day.date < offsetDate(effectiveFrom, 7))
    .flatMap((day) => day.problems.map((entry) => ({
      ...entry, label: `${formatWeekday(day.date)} ${formatShiftDate(day.date)} · ${entry.message}`,
    })));
  return {
    ...editor, draft, areas, tomorrow, affected,
    latest: editor.ui !== 'conflict' || changes ? liveLines : undefined,
    loading: target.isFetching,
    setField: (field: keyof RegularBaselineDraft, value: string) => {
      setDraft({ ...draft, [field]: value });
      setEdited(true);
    },
    compare: () => {
      props.onCompare();
      if (targetMonday && targetMonday !== props.week.weekStart) void target.refetch();
    },
    useLatest: () => {
      if (!incoming || !live) return;
      setBase(incoming);
      setVersion(incoming.regularBaselineRevision);
      editor.backToEdit();
    },
  };
}
