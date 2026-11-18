import { useRef, useState, type FormEvent } from 'react';
import { livePoll } from '../../../shared/api/baseApi';
import type { AttendanceActionState, AttendanceEvent } from '../../../shared/api/types';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { eventLabel } from '../../../shared/utils/labels';
import { formatShiftDate, formatTime } from '../../../shared/utils/thaiTime';
import { useScheduleNavigation } from '../ScheduleReview/useScheduleReview';
import { useAddMissingTimeMutation, useGetTeamSlotQuery } from '../schedulesApi';
import type { TeamMissingTimeProps } from './type';

const stateLabels: Record<AttendanceActionState, string> = {
  recorded: 'ลงเวลาแล้ว', skipped: 'ข้ามรายการนี้', missing: 'ไม่ได้สแกน',
  'not-applicable': 'ไม่ต้องลงเวลาเนื่องจากลา', pending: 'รอลงเวลา',
};

export function useTeamMissingTime({ slot, onClose }: TeamMissingTimeProps) {
  const read = useGetTeamSlotQuery(slot, livePoll);
  const [write] = useAddMissingTimeMutation();
  const [eventType, setEventType] = useState<AttendanceEvent | ''>('');
  const [draftRevision, setDraftRevision] = useState<string>();
  const [time, setTime] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [saved, setSaved] = useState(false);
  const running = useRef(false);
  const guard = useScheduleNavigation(busy || Boolean(eventType || time || reason));

  const state = read.isError ? undefined : read.currentData;
  const actual = new Map(state?.events.map((entry) => [entry.eventType, entry.occurredAt]));
  const choices = (state?.actions ?? []).map((action) => {
    const at = actual.get(action.eventType) ?? (action.state === 'recorded' ? action.occurredAt : null);
    return {
      eventType: action.eventType,
      eligible: !at && action.state !== 'recorded' && action.state !== 'not-applicable'
        && (state?.fillableActions?.includes(action.eventType) ?? action.state === 'missing'),
      label: `${eventLabel[action.eventType]} · ${at ? `${stateLabels.recorded} ${formatTime(at)}` : stateLabels[action.state]}`,
    };
  });
  const selected = choices.some((choice) => choice.eventType === eventType && choice.eligible) ? eventType : '';
  const currentRevision = state?.stateRevision;
  const revisionChanged = !!(draftRevision && currentRevision && draftRevision !== currentRevision);
  const canSelect = !!currentRevision && !busy && !read.isFetching;
  const canSave = !!(selected && time && reason.trim() && draftRevision && !revisionChanged && canSelect);

  function chooseAction(value: string) {
    if (!canSelect) return;
    const action = choices.find((choice) => choice.eventType === value && choice.eligible);
    setEventType(action?.eventType ?? '');
    setDraftRevision(action ? currentRevision ?? undefined : undefined);
  }

  function acknowledgeRevision() {
    if (!selected || !revisionChanged || !canSelect) return;
    setDraftRevision(currentRevision ?? undefined);
    setError(undefined);
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSave || !draftRevision || !selected || running.current) return;
    running.current = true;
    setBusy(true);
    setError(undefined);
    setSaved(false);
    try {
      await write({ ...slot, eventType: selected, time, reason: reason.trim(), expectedRevision: draftRevision }).unwrap();
      setSaved(true);
      setEventType('');
      setDraftRevision(undefined);
      setTime('');
      setReason('');
    } catch (error) {
      setError(getErrorMessage(error));
    } finally {
      // A competing scan or a response lost in transit can change the eligible actions even when this request fails.
      await read.refetch();
      running.current = false;
      setBusy(false);
    }
  }

  return {
    dateLabel: formatShiftDate(slot.shiftDate), busy, canSave, choices, hasMissing: choices.some((choice) => choice.eligible),
    loading: read.isLoading, readError: read.isError ? getErrorMessage(read.error) : undefined,
    error, saved, selected, chooseAction, canSelect, revisionChanged, acknowledgeRevision,
    departureLeaveMismatch: state?.departureLeaveMismatch,
    time, setTime, reason, setReason, save, refetch: read.refetch,
    guard, close: () => { if (!running.current) guard.leave(onClose, true); },
  };
}
