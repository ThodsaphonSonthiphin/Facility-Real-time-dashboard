import { useState } from 'react';
import { Link } from 'react-router';
import type { Alert, CoverSuggestion } from '../../shared/api/alertListApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel } from '../../shared/utils/labels';
import { formatDayTime } from '../../shared/utils/thaiTime';
import { alertDetail, alertTitle, alertWho, coverNotice, fillLink } from './alertRules';
import { useAssignCoverMutation, useReviewAlertMutation } from './alertsApi';

/** A line at the top of the alert page; an action's result outlives the item, which may move tabs. */
export type Notice = { kind: 'ok' | 'error'; text: string };

interface AlertItemProps {
  alert: Alert;
  onNotice: (notice: Notice) => void;
}

/** One alert of wireframe D3: when, who, what, where, and what the Admin can do about it. Server text is React text only. */
export function AlertItem({ alert, onNotice }: AlertItemProps) {
  const [reviewing, setReviewing] = useState(false);
  const title = alertTitle(alert);

  return (
    <li className="alerts-item">
      <article aria-label={`${title} · ${alertWho(alert)}`}>
        <div className="alerts-item__top">
          <span className="alerts-item__time">{formatDayTime(alert.at)}</span>
          <strong>{alertWho(alert)}</strong>
          <span className={`alerts-chip alerts-chip--${alert.kind}`}>{title}</span>
        </div>
        <span className="alerts-item__detail">{alertDetail(alert)}</span>
        <div className="alerts-item__actions">
          {alert.cover && !alert.reviewed && <CoverButton alert={alert} cover={alert.cover} onNotice={onNotice} />}
          {alert.reviewed && (
            <span className="app-muted">{alert.reviewedAt ? `คุยแล้ว ${formatDayTime(alert.reviewedAt)}` : 'คุยแล้ว'}</span>
          )}
          {alert.kind === 'MissingAttendance' && (
            // facility-0051: it closes once the time is filled, on the attendance page
            <Link className="app-button" to={fillLink(alert)}>
              {`เติมเวลา${eventLabel[alert.missingEvents[0] ?? 'ShiftOut']}`}
            </Link>
          )}
          {!alert.reviewed && alert.kind !== 'MissingAttendance' && !reviewing && (
            <button type="button" className="app-button" onClick={() => setReviewing(true)}>
              คุยแล้ว ปิดเตือน
            </button>
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
    <div className="alerts-review">
      <label className="app-field">
        หมายเหตุ (ไม่บังคับ)
        <input className="app-input" value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />
      </label>
      <div className="app-confirm__buttons">
        <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void send()}>
          ยืนยันปิดเตือน
        </button>
        <button type="button" className="app-button" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </button>
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
    <button type="button" className="app-button app-button--primary" disabled={isLoading} onClick={() => void give()}>
      {cover.areaId != null ? `ให้ทำแทน ${alert.areaCode} กะนี้` : 'ให้ตรวจแทนตึกนี้กะนี้'}
    </button>
  );
}
