import { useRef, useState } from 'react';
import { useParams } from 'react-router';
import { freshOnOpen } from '../../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../../shared/api/scanAnswers';
import type { AttendanceEvent, AttendanceState } from '../../../shared/api/types';
import { readLocation } from '../../../shared/geo/readLocation';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { eventLabel } from '../../../shared/utils/labels';
import { formatTime } from '../../../shared/utils/thaiTime';
import { useCheckSignQuery, useConfirmTransferArrivalMutation, useRecordAttendanceMutation } from '../checkInApi';
import type { CheckInOutcome, CheckInPageView, CheckInRetry } from './type';

export function useCheckInPage(): CheckInPageView {
  const { token = '' } = useParams();
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const work = workQuery.data;
  const attendance = work?.attendance;
  const transfer = attendance?.transfer ?? undefined;
  const transferPending = attendance?.dutySource === 'transfer-pending';
  const signQuery = useCheckSignQuery({ token, transferId: transferPending ? transfer?.id : undefined }, freshOnOpen);
  const [record] = useRecordAttendanceMutation();
  const [confirmArrival] = useConfirmTransferArrivalMutation();
  const [busy, setBusy] = useState<CheckInPageView['busy']>(null);
  const sending = useRef(false);
  const retryingRead = useRef(false);
  const [outcome, setOutcome] = useState<CheckInOutcome | null>(null);
  const [proposed, setProposed] = useState<Extract<CheckInRetry, { kind: 'action' }> | null>(null);
  const [retryReadStatus, setRetryReadStatus] = useState<CheckInPageView['retryReadStatus']>('idle');

  const signGone = statusOf(signQuery.error) === 404;
  const loadError = workQuery.error ?? (signGone ? undefined : signQuery.error);
  const live = signQuery.isSuccess && signQuery.currentData !== undefined
    && !signQuery.isFetching && !workQuery.isFetching && loadError == null ? attendance : undefined;
  const choices = !live || transferPending || (live.lifecycleVersion === 'Numbered6' && !live.stateRevision)
    ? []
    : live.nextActions?.map(({ action, requiredGaps }) => ({ eventType: action, gaps: requiredGaps }))
      ?? live.nextEvents.map((eventType) => ({ eventType, gaps: [] }));
  const arrivalJustConfirmed = outcome?.kind === 'transfer-confirmed' && outcome.result.id === transfer?.id;
  const confirmable = !!live && transferPending && !!transfer && !arrivalJustConfirmed
    && signQuery.currentData?.canConfirmTransfer === true;
  const revision = live?.lifecycleVersion === 'Numbered6' ? live.stateRevision : undefined;
  const gapConfirmation = proposed && revision === proposed.revision
    && choices.some(({ eventType }) => eventType === proposed.eventType) ? proposed : null;

  const refresh = () => {
    void workQuery.refetch();
    void signQuery.refetch();
  };

  const retryRead = async () => {
    if (retryingRead.current) return;
    retryingRead.current = true;
    setRetryReadStatus('pending');
    try {
      await Promise.all([workQuery.refetch(), signQuery.refetch()]);
    } finally {
      retryingRead.current = false;
      setRetryReadStatus('complete');
    }
  };

  const submit = async (request: CheckInRetry) => {
    if (sending.current) return;
    sending.current = true;
    setBusy(request.kind === 'action' ? request.eventType : 'transfer');
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind, retry: request });
        return;
      }
      if (request.kind === 'transfer') {
        const answer = await confirmArrival({
          transferId: request.transferId,
          expectedRevision: request.revision,
          qrToken: token,
          ...reading.fix,
        });
        if (answer.data) {
          setOutcome({ kind: 'transfer-confirmed', result: answer.data });
          refresh();
        } else {
          setOutcome({
            kind: 'failed',
            message: isNotSent(answer.error)
              ? `ยังไม่ได้ยืนยันเข้าพื้นที่ใหม่ ${getErrorMessage(answer.error)}`
              : getErrorMessage(answer.error),
          });
          if (statusOf(answer.error) === 409 || statusOf(answer.error) === 404) refresh();
        }
        return;
      }

      const { eventType, gaps } = request;
      const answer = await record({
        qrToken: token,
        eventType,
        ...reading.fix,
        ...(request.revision ? { lifecycleVersion: 'Numbered6', stateRevision: request.revision } : {}),
        ...(gaps.length ? { confirmGaps: gaps } : {}),
      });
      if (answer.data) {
        setOutcome(attendanceAnswer(eventType, answer.data, reading.fix.accuracyM));
        return;
      }
      const blocked = blockedScanOf(answer.error);
      if (blocked) {
        setOutcome({ kind: 'blocked', blocked });
        return;
      }
      setOutcome({
        kind: 'failed',
        message: isNotSent(answer.error)
          ? `ยังไม่ได้บันทึกเวลา ${getErrorMessage(answer.error)}`
          : getErrorMessage(answer.error),
      });
      if (statusOf(answer.error) === 409) refresh();
    } finally {
      sending.current = false;
      setBusy(null);
    }
  };

  const press = (eventType: AttendanceEvent) => {
    const choice = choices.find((item) => item.eventType === eventType);
    if (!choice) return;
    const request = { kind: 'action' as const, ...choice, revision };
    if (request.gaps.length) {
      setProposed(request);
    } else {
      void submit(request);
    }
  };

  const confirmGaps = () => {
    if (!gapConfirmation) return;
    setProposed(null);
    void submit(gapConfirmation);
  };

  const confirmDestination = () => {
    if (!confirmable || !transfer) return;
    void submit({ kind: 'transfer', transferId: transfer.id, revision: transfer.revision });
  };

  const retryLocation = () => {
    if (outcome?.kind !== 'location') return;
    const { retry } = outcome;
    if (retry.kind === 'transfer') {
      if (confirmable && transfer?.id === retry.transferId && transfer.revision === retry.revision) {
        void submit(retry);
        return;
      }
    } else if (choices.some(({ eventType }) => eventType === retry.eventType)
      && retry.revision === revision) {
      void submit(retry);
      return;
    }
    setOutcome(null);
  };

  const finished = !!live?.shift && !transferPending && (live.nextActions ?? live.nextEvents).length === 0;
  const incompleteActions = live?.lifecycleVersion === 'Numbered6'
    && live.actions?.some((action) => action.state === 'skipped' || action.state === 'missing');

  return {
    attendance,
    ownArea: work?.areas.find((area) => !area.isCover),
    ownShift: work?.ownShift,
    signGone,
    loadError: loadError == null ? undefined : getErrorMessage(loadError),
    loading: workQuery.isLoading || signQuery.isLoading,
    choices,
    gapConfirmation,
    gapLabel: gapConfirmation?.gaps.map((eventType) => eventLabel[eventType]).join(' และ '),
    completionMessage: finished ? (incompleteActions ? 'สิ้นสุดกะแล้ว' : 'ลงเวลาครบแล้วสำหรับกะนี้') : undefined,
    busy,
    outcome,
    transfer,
    confirmable,
    transferPending: transferPending && !arrivalJustConfirmed,
    press,
    confirmGaps,
    cancelGaps: () => setProposed(null),
    confirmDestination,
    retryLocation,
    retryRead,
    retryingRead: workQuery.isFetching || signQuery.isFetching,
    retryReadStatus,
  };
}

function attendanceAnswer(
  eventType: AttendanceEvent, state: AttendanceState, accuracyM: number,
): Extract<CheckInOutcome, { kind: 'answered' }> {
  const label = eventLabel[eventType];
  const recorded = state.events.find((event) => event.eventType === eventType);
  const time = recorded ? formatTime(recorded.occurredAt) : '';

  if (state.outcome === 'TooSoon') {
    return { kind: 'answered', tone: 'warn',
      message: `ยังไม่ได้ลงเวลา${label} รอให้ครบ 5 นาทีจากครั้งก่อน แล้วกด “${label}” อีกครั้ง` };
  }
  if (state.outcome === 'AlreadyRecorded') {
    return { kind: 'answered', tone: 'info', message: `${label}: ลงเวลาไว้แล้วตอน ${time} ระบบเก็บเวลาแรกไว้` };
  }
  if (recorded?.withinRadius === false) {
    return { kind: 'answered', tone: 'warn', message: `บันทึกเวลา${label}แล้ว ${time} แต่อยู่ไกลป้าย`,
      accuracyM: Math.round(accuracyM) };
  }
  return { kind: 'answered', tone: 'success', message: `บันทึกเวลา${label}แล้ว ${time}` };
}
