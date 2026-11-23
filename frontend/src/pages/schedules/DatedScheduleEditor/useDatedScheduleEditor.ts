import { useState } from 'react';
import type { RowProblem, ScheduleDay } from '../../../shared/api/scheduleTypes';
import { formatThaiDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { offsetDate, type CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { clockSlot } from '../scheduleRules';
import type { DatedAction, DatedScheduleDraft, DatedScheduleEditorProps } from './type';

const fieldNames: Record<DatedAction, string> = {
  area: 'พื้นที่', breakGroup: 'กลุ่มพัก', leave: 'บันทึกลา', cancelLeave: 'ยกเลิกลา',
};

function utcFromThai(value: string) {
  const time = Date.parse(`${value}:00+07:00`);
  return Number.isFinite(time) ? new Date(time).toISOString() : '';
}

function leaveLabel(leave: NonNullable<ScheduleDay['edit']>['leave']) {
  if (!leave) return 'ไม่มีรายการลา';
  const formatter = new Intl.DateTimeFormat('th-TH', {
    timeZone: 'Asia/Bangkok', dateStyle: 'short', timeStyle: 'short',
  });
  return `${formatter.format(new Date(leave.startsAt))} – ${formatter.format(new Date(leave.endsAt))}`;
}

function draftFor(day: ScheduleDay, action: DatedAction): DatedScheduleDraft {
  const dayShift = day.shift === 'Day';
  return {
    action, value: action === 'area' ? String(day.area.value ?? '') : String(day.breakGroup.value ?? ''),
    startsAt: `${day.date}T${dayShift ? '07:00' : '19:00'}`,
    endsAt: `${dayShift ? day.date : offsetDate(day.date, 1)}T${dayShift ? '19:00' : '07:00'}`,
    reason: '',
  };
}

export function useDatedScheduleEditor(props: DatedScheduleEditorProps) {
  const [base, setBase] = useState(props.day);
  const [draft, setDraft] = useState(() => draftFor(props.day, 'area'));
  const { date, shift, edit } = base;
  const { action, reason, value, startsAt, endsAt } = draft;
  const correction = date < clockSlot(new Date()).startDate;
  const area = action === 'area';
  const leave = action === 'leave' || action === 'cancelLeave';
  const setField = (field: keyof DatedScheduleDraft, value: string) => setDraft({ ...draft, [field]: value });
  const current = props.latest?.edit;
  const availableActions: DatedAction[] = ['area', 'breakGroup', edit?.leave ? 'cancelLeave' : 'leave'];
  const key = leave ? `leave:${props.person.userId}:${date}:${shift}`
    : `exception:${props.person.userId}:${date}:${shift}:${area ? 'area' : 'breakgroup'}`;
  const scopeUnchanged = (day: ScheduleDay | undefined) => {
    const current = day?.edit;
    return !!current?.canChange && day?.shift === shift
      && current.areaRevision === edit?.areaRevision && current.breakRevision === edit?.breakRevision
      && current.leave?.revision === edit?.leave?.revision
      && current.baselineAreaId === edit?.baselineAreaId && current.baselineBreakGroup === edit?.baselineBreakGroup;
  };
  const changes = !scopeUnchanged(props.latest);
  const newValue = value ? Number(value) : null;
  const problem = (message: string): RowProblem => ({ rowId: key, code: 'row-invalid', message });
  const problems = !availableActions.includes(action) ? [problem('รายการลาของวันนี้เปลี่ยนแล้ว')] : [];
  const reasonRequired = correction && !leave;
  if (reasonRequired && !reason.trim()) problems.push(problem('ระบุเหตุผลที่ต้องแก้ไขแผนย้อนหลัง'));
  if (action === 'leave' && (!utcFromThai(startsAt) || !utcFromThai(endsAt) || startsAt >= endsAt)) {
    problems.push(problem('เวลาเริ่มลาต้องอยู่ก่อนเวลาสิ้นสุด'));
  }
  if (area && value && !edit?.areas.some((option) => String(option.id) === value) && Number(value) !== base.area.value) {
    problems.push(problem('เลือกพื้นที่ที่ยังใช้งานในตึกของแม่บ้าน'));
  }

  const numberLabel = (id: number | null | undefined) => area
    ? edit?.areas.find((option) => option.id === id)?.label ?? (id == null ? 'ยังไม่กำหนด' : `พื้นที่ ${id}`)
    : (id == null ? 'ยังไม่กำหนด' : `กลุ่ม ${id}`);
  const line = (day: ScheduleDay) => ({
    id: key,
    label: `${props.person.displayName} · ${formatWeekday(date)} ${formatThaiDate(date)} · ${fieldNames[action]}`,
    before: leave ? leaveLabel(day.edit?.leave) : numberLabel(area ? day.area.value : day.breakGroup.value),
    after: action === 'leave' && utcFromThai(startsAt) && utcFromThai(endsAt)
      ? leaveLabel({ revision: '0', startsAt: utcFromThai(startsAt), endsAt: utcFromThai(endsAt) })
      : leave ? 'ไม่มีรายการลา'
        : newValue === null ? 'ใช้ค่าจากตารางประจำ' : numberLabel(newValue),
  });
  const field = area ? base.area : base.breakGroup;
  const fieldRevision = area ? edit?.areaRevision : edit?.breakRevision;
  const dirty = leave || (newValue === null ? fieldRevision !== '0' : newValue !== (field.value ?? null))
    || reason.length > 0;
  const command: CommandDraft = {
    kind: action === 'leave' ? 'record-leave' : action === 'cancelLeave' ? 'cancel-leave'
      : correction ? 'correct-plan' : area ? 'set-dated-area' : 'set-dated-break',
    expectedRevisions: { [key]: (leave ? edit?.leave?.revision : fieldRevision) ?? '0' },
    payload: {
      userId: props.person.userId, date, shift,
      reason: reason.trim() || undefined,
      ...(leave ? (action === 'leave' ? { startsAt: utcFromThai(startsAt), endsAt: utcFromThai(endsAt) } : {})
        : { [area ? 'areaId' : 'breakGroup']: newValue }),
    },
  };
  const beforeAfter = [line(base)];
  const latest = current && props.latest ? [line(props.latest)] : undefined;
  const editor = useScheduleReview({
    command, dirty, problems, beforeAfter, phone: props.phone, onClose: props.onClose, changing: changes,
    verifyRetryScope: async () => {
      const week = await props.reloadEditorWeek();
      const person = week.rows.find((row) => row.userId === props.person.userId);
      return scopeUnchanged(person?.days.find((day) => day.date === date));
    },
  });
  return {
    ...editor, draft, correction, reasonRequired, setField,
    latest: editor.ui !== 'conflict' || changes ? latest : undefined,
    areas: edit?.areas ?? [],
    corrections: current?.corrections ?? [],
    dateLabel: `${formatWeekday(date)} ${formatThaiDate(date)}`,
    choices: availableActions.map((value) => ({ value, label: fieldNames[value] })),
    setAction: (action: DatedAction) => setDraft(draftFor(base, action)),
    useLatest: () => {
      if (!props.latest?.edit?.canChange) return;
      setBase(props.latest);
      editor.backToEdit();
    },
  };
}
