import { useState } from 'react';
import { Link } from 'react-router';
import type { Alert, AlertKind, CoverSuggestion } from '../../shared/api/alertListApi';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import type { Notice } from '../../shared/components/Toast';
import { buttonClass, noteTones } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel } from '../../shared/utils/labels';
import { formatDayTime } from '../../shared/utils/thaiTime';
import { alertDetail, alertTitle, alertWho, coverNotice, fillLink } from './alertRules';
import { useAssignCoverMutation, useReviewAlertMutation } from './alertsApi';

// An alert's kind is not a Point Status, so its chip takes a note's colours, never a status colour (facility-0087 rule
// 3): refused at a sign in the error tone, far from a sign in the warn tone, a missing time plain grey (not a
// control's outline, so it does not look pressable)
const kindChip: Record<AlertKind, string> = {
  BlockedScan: noteTones.error,
  AttendanceGps: noteTones.warn,
  SubmissionGps: noteTones.warn,
  InspectionGps: noteTones.warn,
  MissingAttendance: 'border-border bg-bg text-text',
};

interface AlertItemProps {
  alert: Alert;
  onNotice: (notice: Notice) => void;
}

/** One alert of wireframe D3: when, who, what, where, and what the Admin can do about it. Server text is React text only. */
export function AlertItem({ alert, onNotice }: AlertItemProps) {
  const [reviewing, setReviewing] = useState(false);
  const title = alertTitle(alert);

  return (
    <li>
      <article
        aria-label={`${title} · ${alertWho(alert)}`}
        className="flex flex-col gap-1.5 rounded border border-border px-3.5 py-3"
      >
        <div className="flex flex-wrap items-center gap-2.5">
          <span className="text-muted tabular-nums">{formatDayTime(alert.at)}</span>
          <strong>{alertWho(alert)}</strong>
        </div>
        {/* self-start: its own line in every card, not only when the name is long */}
        <span className={`self-start rounded-full border px-2.5 py-0.5 font-semibold ${kindChip[alert.kind]}`}>{title}</span>
        <span>{alertDetail(alert)}</span>
        <div className="flex flex-wrap items-center gap-2.5">
          {alert.cover && !alert.reviewed && <CoverButton alert={alert} cover={alert.cover} onNotice={onNotice} />}
          {alert.reviewed && (
            <span className="text-muted">{alert.reviewedAt ? `คุยแล้ว ${formatDayTime(alert.reviewedAt)}` : 'คุยแล้ว'}</span>
          )}
          {alert.kind === 'MissingAttendance' && (
            // facility-0051: it closes once the time is filled, on the attendance page
            <Link className={buttonClass('secondary', 'desk')} data-tap="" to={fillLink(alert)}>
              {`เติมเวลา${eventLabel[alert.missingEvents[0] ?? 'ShiftOut']}`}
            </Link>
          )}
          {!alert.reviewed && alert.kind !== 'MissingAttendance' && !reviewing && (
            <Button size="desk" onClick={() => setReviewing(true)}>
              คุยแล้ว ปิดเตือน
            </Button>
          )}
        </div>
        {reviewing && (
          <ReviewForm
            alert={alert}
            onDone={() => {
              setReviewing(false);
              onNotice({ kind: 'ok', text: `ปิดเตือน${title}ของ ${alert.person.displayName} แล้ว` });
            }}
            onError={(text) => onNotice({ kind: 'error', text })}
            onCancel={() => setReviewing(false)}
          />
        )}
      </article>
    </li>
  );
}

interface ReviewFormProps {
  alert: Alert;
  onDone: () => void;
  onError: (text: string) => void;
  onCancel: () => void;
}

/** "คุยแล้ว ปิดเตือน" with an optional note (plan 11 decision 11). One send at a time. */
function ReviewForm({ alert, onDone, onError, onCancel }: ReviewFormProps) {
  const [note, setNote] = useState('');
  const [review, { isLoading }] = useReviewAlertMutation();

  const send = async () => {
    try {
      await review({ kind: alert.kind, recordId: alert.recordId, note: note.trim() === '' ? undefined : note.trim() }).unwrap();
      onDone();
    } catch (error) {
      onError(getErrorMessage(error));
    }
  };

  return (
    <div className="flex max-w-120 flex-col gap-2">
      <Field label="หมายเหตุ (ไม่บังคับ)">
        <TextInput size="desk" value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />
      </Field>
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" size="desk" disabled={isLoading} onClick={() => void send()}>
          ยืนยันปิดเตือน
        </Button>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
      </div>
    </div>
  );
}

interface CoverButtonProps {
  alert: Alert;
  cover: CoverSuggestion;
  onNotice: (notice: Notice) => void;
}

/**
 * facility-0041 rule 4, 0048: the person refused at a sign gets the cover the server suggests, for that shift only, in
 * one press (undone by cancelling it). The server closes this alert with it. One send at a time.
 */
function CoverButton({ alert, cover, onNotice }: CoverButtonProps) {
  const [assign, { isLoading }] = useAssignCoverMutation();

  const give = async () => {
    try {
      const made = await assign({
        userId: cover.userId,
        areaId: cover.areaId,
        buildingId: cover.buildingId,
        shiftDate: cover.shiftDate,
        shift: cover.shift,
        blockedScanId: alert.recordId,
      }).unwrap();
      onNotice({ kind: 'ok', text: coverNotice(made) });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <Button variant="primary" size="desk" disabled={isLoading} onClick={() => void give()}>
      {cover.areaId != null ? `ให้ทำแทน ${alert.areaCode} กะนี้` : 'ให้ตรวจแทนตึกนี้กะนี้'}
    </Button>
  );
}
