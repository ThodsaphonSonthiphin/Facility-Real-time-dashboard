import { useState } from 'react';
import type { ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { livePoll } from '../../../shared/api/baseApi';
import type { AttendanceState, AttendanceTransfer } from '../../../shared/api/types';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { transferStateLabel } from '../../../shared/utils/labels';
import { formatDayTime, formatThaiDate } from '../../../shared/utils/thaiTime';
import type { CommandDraft } from '../commandRules';
import { useScheduleReview } from '../ScheduleReview/useScheduleReview';
import { useGetTeamSlotQuery, type TeamSlotArgs } from '../schedulesApi';
import type { TransferInstructionProps } from './type';

function scopedAreas(week: ScheduleWeek | undefined, { shiftDate, shift }: TeamSlotArgs) {
  return new Map((week?.destinations ?? []).filter((slot) => slot.date === shiftDate && slot.shift === shift)
    .flatMap(({ buildingCode, areas }) => areas.map(({ id, label }) => [id, `ตึก ${buildingCode} · ${label}`] as const)));
}

function transferSnapshot(state: AttendanceState) {
  return JSON.stringify({
    origin: state.dutyAreaId,
    instruction: state.transfer?.id,
    revision: state.transfer?.revision,
    status: state.transfer?.state,
    canCancel: state.canCancelTransfer,
    entered: state.events.find((event) => event.eventType === 'ShiftIn')?.occurredAt,
    left: state.events.some((event) => event.eventType === 'ShiftOut'),
  });
}

export function useTransferInstruction(props: TransferInstructionProps) {
  const { phone, slot, displayName, latest } = props;
  const { userId, shiftDate, shift } = slot;
  const read = useGetTeamSlotQuery(slot, livePoll);
  const state = read.isError ? undefined : read.currentData;
  const [base, setBase] = useState<AttendanceState>();
  const [destination, setDestination] = useState('');
  const [effectiveTime, setEffectiveTime] = useState('');
  const [reason, setReason] = useState('');
  const snapshot = base ?? state;
  const transfer = snapshot?.transfer;
  const hasPending = transfer?.state === 'instructed';
  const cancelling = hasPending && snapshot?.canCancelTransfer !== false;
  const origin = snapshot?.dutyAreaId;
  const areas = scopedAreas(latest, slot);
  const destinations = Array.from(areas, ([id, label]) => ({ id, label })).filter(({ id }) => id !== origin);
  const validDestination = destinations.some(({ id }) => String(id) === destination);
  const planLabels = latest?.rows.find((person) => person.userId === userId)?.days
    .find((day) => day.date === shiftDate && day.shift === shift)?.edit?.areas;
  const areaLabel = (id: number | null | undefined) => id == null ? 'ยังไม่มีพื้นที่'
    : areas.get(id) ?? planLabels?.find((area) => area.id === id)?.label ?? `พื้นที่ ${id}`;
  const entered = snapshot?.events.some(({ eventType }) => eventType === 'ShiftIn');
  const left = snapshot?.events.some(({ eventType }) => eventType === 'ShiftOut');
  const cannotInstruct = hasPending ? 'มีคำสั่งย้ายพื้นที่อยู่แล้ว' : !origin ? 'กะนี้ยังไม่มีพื้นที่ปฏิบัติงาน'
    : !entered ? 'ยังไม่ได้ลงเวลาเข้างาน'
      : left ? 'ลงเวลาเลิกงานแล้ว' : !destinations.length ? 'ไม่มีพื้นที่ปลายทางในกะที่เลือก' : undefined;

  const parsedTime = Date.parse(`${effectiveTime}:00+07:00`);
  const effectiveAt = Number.isFinite(parsedTime) ? new Date(parsedTime).toISOString() : undefined;
  const matchingSlot = (value: AttendanceState | undefined) => value?.shiftDate === shiftDate && value.shift === shift;
  const current = matchingSlot(state) && state;
  const sameSlot = (value: AttendanceState | undefined) =>
    !!snapshot && !!value && matchingSlot(value) && transferSnapshot(value) === transferSnapshot(snapshot);
  const changing = !!base && (!current || !sameSlot(current) || !latest || (!!destination && !validDestination));
  const key = `transfer:${userId}:${shiftDate}:${shift}`;
  const command: CommandDraft = {
    kind: cancelling ? 'cancel-transfer' : 'instruct-transfer',
    expectedRevisions: { [key]: transfer?.revision ?? '0' },
    payload: cancelling
      ? { transferId: transfer.id, reason: reason.trim() }
      : { userId, shiftDate, shift, fromAreaId: origin, toAreaId: Number(destination), effectiveAt, reason: reason.trim() },
  };
  const trimDate = formatThaiDate(shiftDate);
  const line = [{
    id: key, label: `${displayName} · ${trimDate}`,
    before: cancelling ? `${areaLabel(transfer.fromAreaId)} → ${areaLabel(transfer.toAreaId)} · ${transferStateLabel.instructed}` : areaLabel(origin),
    after: cancelling ? 'ยกเลิกคำสั่งย้ายพื้นที่'
      : `${areaLabel(Number(destination))} · ${effectiveAt ? formatDayTime(effectiveAt) : 'ยังไม่ระบุเวลาที่มีผล'}`,
  }];
  const editor = useScheduleReview({
    command, beforeAfter: line, phone, onClose: props.onClose, changing,
    problems: [], dirty: Boolean(destination || effectiveTime || reason),
    ready: !!(current && latest && !read.isFetching && reason.trim()
      && (cancelling || (!cannotInstruct && validDestination && effectiveAt))),
    verifyRetryScope: async () => {
      const [slotState, week] = await Promise.all([read.refetch().unwrap(), props.reloadEditorWeek()]);
      return sameSlot(slotState) && (cancelling || scopedAreas(week, slot).has(Number(destination)));
    },
  });

  const describe = (instruction: AttendanceTransfer) => ({
    id: instruction.id,
    route: `${areaLabel(instruction.fromAreaId)} → ${areaLabel(instruction.toAreaId)}`,
    effective: formatDayTime(instruction.effectiveAt),
    arrival: instruction.confirmedAt ? formatDayTime(instruction.confirmedAt) : undefined,
    status: transferStateLabel[instruction.state],
  });
  const history = state?.transferHistory ?? [];
  const confirmed = state?.transfer?.state === 'confirmed' && !history.some((entry) => entry.id === state.transfer?.id)
    ? [...history, state.transfer] : history;
  const edit = (set: (value: string) => void) => (value: string) => {
    if (!base && current) setBase(current);
    set(value);
  };

  return {
    ...editor, trimDate, cancelling, cannotInstruct, destinations, destination, effectiveTime, reason,
    origin: areaLabel(origin), hasState: !!current, confirmHistory: confirmed.map(describe),
    currentInstruction: state?.transfer && state.transfer.state !== 'confirmed' ? describe(state.transfer) : undefined,
    loading: read.isLoading, readError: read.isError ? getErrorMessage(read.error) : undefined,
    setDestination: edit(setDestination), setEffectiveTime: edit(setEffectiveTime), setReason: edit(setReason),
    latest: current && (changing || editor.ui === 'conflict') ? [{
      id: key, label: `${displayName} · ${trimDate}`, before: areaLabel(current.dutyAreaId),
      after: current.transfer ? transferStateLabel[current.transfer.state] : 'ไม่มีคำสั่งย้ายพื้นที่',
    }] : undefined,
    onCompare: () => { void read.refetch(); void props.reloadEditorWeek().catch(() => undefined); },
    useLatest: () => {
      if (!current || !latest || read.isFetching) return;
      setBase(undefined);
      setDestination('');
      setEffectiveTime('');
      setReason('');
      editor.backToEdit();
    },
  };
}
