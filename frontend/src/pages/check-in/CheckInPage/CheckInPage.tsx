import { ChevronUp, Clock } from 'lucide-react';
import { useEffect, useId, useRef } from 'react';
import type { AttendanceEvent, OwnShift, Shift } from '../../../shared/api/types';
import { BlockedScanScreen } from '../../../shared/components/BlockedScanScreen';
import { Button } from '../../../shared/components/Button';
import { LocationHelp } from '../../../shared/components/LocationHelp';
import { DialogHeader, Sheet } from '../../../shared/components/Modal';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../../shared/components/PhoneScreen';
import { StatusScreen } from '../../../shared/components/StatusScreen';
import { attendanceEventLabel, departureLeaveMismatchLabel, shiftLabel } from '../../../shared/utils/labels';
import { formatClock, formatTime } from '../../../shared/utils/thaiTime';
import { AttendanceTimeline } from '../AttendanceTimeline';
import { useCheckInPage } from './useCheckInPage';

export function CheckInPage() {
  const {
    attendance, ownArea, ownShift, signGone, loadError, loading, choices, gapConfirmation, gapLabel,
    completionMessage, busy, outcome, transfer, confirmable, canShiftOutOrigin, transferPending,
    otherActionsOpen, openOtherActions, closeOtherActions, press, confirmGaps, cancelGaps,
    confirmDestination, retryLocation, retryRead, retryingRead, retryReadStatus,
  } = useCheckInPage();
  const heading = useRef<HTMLHeadingElement>(null);
  const gapNote = useRef<HTMLDivElement>(null);
  const shift = attendance?.shift ?? undefined;
  const primaryChoice = choices[0];
  const secondaryChoices = choices.slice(1);

  useEffect(() => {
    if (retryReadStatus === 'complete' && !loadError && !signGone) heading.current?.focus();
  }, [retryReadStatus, loadError, signGone]);

  useEffect(() => {
    if (!gapConfirmation) return;
    gapNote.current?.scrollIntoView?.({ block: 'nearest' });
    gapNote.current?.focus();
  }, [gapConfirmation]);

  if (signGone) {
    return (
      <StatusScreen
        title="ป้ายนี้ใช้ไม่ได้แล้ว"
        detail="ยังไม่ได้ลงเวลา ลองสแกนป้ายลงเวลาที่ติดอยู่ล่าสุด ถ้ายังใช้ไม่ได้ ให้แจ้งแอดมิน"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (outcome?.kind === 'location') {
    return (
      <LocationHelp
        problem={outcome.problem}
        action={outcome.retry.kind === 'transfer' ? 'ยืนยันเข้าพื้นที่ใหม่' : 'ลงเวลา'}
        busy={busy !== null}
        onRetry={retryLocation}
      />
    );
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="check-in" ownAreaName={ownArea?.areaName} />;
  }

  return (
    <PhoneScreen
      navMode="none"
      header={
        <PhoneHeader back={{ to: '/my-work', label: 'งานของฉัน' }}>
          <span className="text-sm font-semibold text-text-2">ป้ายลงเวลา</span>
          <h1 ref={heading} tabIndex={-1} className="text-xl font-extrabold focus:outline-2 focus:outline-offset-2 focus:outline-accent">ลงเวลา</h1>
          {attendance?.shift && <p className="text-sm font-semibold tabular-nums">{shiftLabel[attendance.shift]}</p>}
          {ownArea && (
            <p className="text-sm text-text-2">{`พื้นที่ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด`}</p>
          )}
        </PhoneHeader>
      }
      bottom={
        loadError ? (
          <Button variant="primary" size="main" disabled={retryingRead} onClick={retryRead}>
            {retryingRead ? 'กำลังโหลด...' : 'ลองอีกครั้ง'}
          </Button>
        ) : confirmable || busy === 'transfer' ? (
          <Button variant="primary" size="main" disabled={busy !== null} onClick={confirmDestination}>
            {busy === 'transfer' ? 'กำลังยืนยัน...' : 'ยืนยันเข้าพื้นที่ใหม่'}
          </Button>
        ) : gapConfirmation ? (
          <>
            <Button variant="primary" size="main" disabled={busy !== null} onClick={confirmGaps}>ยืนยันและบันทึก</Button>
            <Button variant="secondary" size="phone" className="w-full" onClick={cancelGaps}>ยกเลิก</Button>
          </>
        ) : primaryChoice ? (
          <>
            <Button
              variant="primary"
              size="main"
              disabled={busy !== null}
              onClick={() => press(primaryChoice.eventType)}
            >
              <Clock aria-hidden="true" className="size-6" />
              {busy === primaryChoice.eventType
                ? 'กำลังบันทึก...'
                : attendanceEventLabel(primaryChoice.eventType, shift)}
            </Button>
            {secondaryChoices.length > 0 && (
              <Button
                variant="secondary"
                size="phone"
                className="w-full"
                disabled={busy !== null}
                aria-haspopup="dialog"
                aria-expanded={otherActionsOpen}
                onClick={openOtherActions}
              >
                <span>{`ลงเวลาขั้นตอนอื่น (${secondaryChoices.length})`}</span>
                <ChevronUp aria-hidden="true" className="size-5" />
              </Button>
            )}
          </>
        ) : undefined
      }
    >
      <p role="status" aria-live="polite" aria-atomic="true" className="sr-only">
        {retryReadStatus === 'pending'
          ? 'กำลังลองเชื่อมต่ออีกครั้ง'
          : retryReadStatus === 'complete'
            ? loadError ? 'ยังเชื่อมต่อไม่ได้ ลองอีกครั้ง' : 'เชื่อมต่อสำเร็จแล้ว'
            : ''}
      </p>
      {loading && <PageLoading lines={4}>กำลังโหลด...</PageLoading>}
      {loadError && <Note tone="error">{loadError}</Note>}
      {attendance && !attendance.shift && <OffShiftNote ownShift={ownShift} />}
      {attendance?.shift && <AttendanceTimeline attendance={attendance} next={choices[0]?.eventType} />}
      {attendance?.isDayOff && <Note tone="warn">กะนี้ตรงกับวันหยุดตามแผน</Note>}
      {attendance?.isLeave && <Note tone="warn">มีการลาที่บันทึกไว้ในกะนี้</Note>}
      {attendance?.departureLeaveMismatch && <Note tone="warn">{departureLeaveMismatchLabel}</Note>}
      {gapConfirmation && (
        <div
          ref={gapNote}
          tabIndex={-1}
          className="rounded-md focus:outline-2 focus:outline-offset-2 focus:outline-accent"
        >
          <Note
            tone="warn"
            status
            title={gapConfirmation.confirmPendingTransfer ? 'ยืนยันเลิกงานโดยไม่ย้ายพื้นที่' : 'ยืนยันขั้นตอนที่ข้าม'}
          >
            {gapConfirmation.confirmPendingTransfer
              ? gapLabel
                ? `มีคำสั่งย้ายพื้นที่ที่ยังไม่ได้ยืนยัน หากเลิกงานตอนนี้จะปิดกะโดยไม่ย้ายพื้นที่ และต้องยืนยันรายการที่ข้ามก่อน: ${gapLabel}`
                : 'มีคำสั่งย้ายพื้นที่ที่ยังไม่ได้ยืนยัน หากเลิกงานตอนนี้จะปิดกะโดยไม่ย้ายพื้นที่'
              : `ต้องยืนยันรายการที่ข้ามก่อน: ${gapLabel}`}
          </Note>
        </div>
      )}
      {transferPending && !gapConfirmation?.confirmPendingTransfer && (
        <Note tone="warn" title="มีคำสั่งย้ายพื้นที่ที่ยังไม่ได้ยืนยัน">
          {canShiftOutOrigin
            ? 'ยืนยันที่ป้ายของพื้นที่ปลายทางก่อนลงเวลา หรือกด “เลิกงาน” ด้านล่างหากต้องการปิดกะที่พื้นที่เดิม'
            : 'ยืนยันที่ป้ายของพื้นที่ปลายทางก่อนลงเวลา'}
        </Note>
      )}
      {transfer?.state === 'instructed' && !transferPending && outcome?.kind !== 'transfer-confirmed' && (
        <Note tone="info" title="มีคำสั่งย้ายพื้นที่">{`มีผลเวลา ${formatTime(transfer.effectiveAt)}`}</Note>
      )}
      {outcome?.kind === 'transfer-confirmed' ? (
        <TransferConfirmedNote confirmedAt={outcome.result.confirmedAt} status />
      ) : transfer?.state === 'confirmed' ? (
        <TransferConfirmedNote confirmedAt={transfer.confirmedAt} />
      ) : null}
      {outcome?.kind === 'answered' && (
        outcome.accuracyM === undefined ? (
          <Note tone={outcome.tone} status>{outcome.message}</Note>
        ) : (
          <Note tone={outcome.tone} status title={outcome.message}>
            <span>{`ความแม่นยำของตำแหน่ง ±${outcome.accuracyM} ม.`}</span>
            <span>แอดมินจะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้งแอดมินได้เลย</span>
          </Note>
        )
      )}
      {outcome?.kind === 'failed' && <Note tone="error">{outcome.message}</Note>}
      {completionMessage && <Note tone="success">{completionMessage}</Note>}
      {otherActionsOpen && (
        <OtherActionsSheet
          choices={secondaryChoices}
          shift={shift}
          busy={busy}
          onSelect={press}
          onClose={closeOtherActions}
        />
      )}
    </PhoneScreen>
  );
}

function TransferConfirmedNote({ confirmedAt, status }: { confirmedAt?: string | null; status?: boolean }) {
  return (
    <Note tone="success" status={status} title="ยืนยันถึงพื้นที่ใหม่แล้ว">
      {confirmedAt && formatTime(confirmedAt)}
    </Note>
  );
}

function OtherActionsSheet({
  choices,
  shift,
  busy,
  onSelect,
  onClose,
}: {
  choices: { eventType: AttendanceEvent }[];
  shift?: Shift;
  busy: AttendanceEvent | 'transfer' | null;
  onSelect: (eventType: AttendanceEvent) => void;
  onClose: () => void;
}) {
  const titleId = useId();
  return (
    <Sheet labelledBy={titleId} onClose={onClose}>
      <DialogHeader
        id={titleId}
        title="เลือกขั้นตอนอื่น"
        detail="ใช้เมื่อลืมลงเวลารอบก่อนหน้า ระบบจะให้ยืนยันขั้นตอนที่ข้าม"
        onClose={onClose}
      />
      <div className="flex flex-col gap-2">
        {choices.map(({ eventType }) => (
          <Button
            key={eventType}
            variant={eventType === 'ShiftOut' ? 'danger' : 'secondary'}
            size="phone"
            className="w-full"
            disabled={busy !== null}
            onClick={() => onSelect(eventType)}
          >
            {attendanceEventLabel(eventType, shift)}
          </Button>
        ))}
      </div>
    </Sheet>
  );
}

/** facility-0077: outside the attendance window the Cleaner is told their own shift and when it can be recorded. */
function OffShiftNote({ ownShift }: { ownShift?: OwnShift }) {
  return (
    <Note tone="warn" title="ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ">
      {ownShift && <span>{`กะของคุณ: ${shiftLabel[ownShift.shift]}`}</span>}
      {ownShift && (
        <span>{`ลงเวลาได้ตั้งแต่ ${formatClock(ownShift.opensAt)} ถึง ${formatClock(ownShift.closesAt)}`}</span>
      )}
    </Note>
  );
}
