import { ArrowRight, ArrowRightLeft, Check, Inbox } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import type { Alert, CoverSuggestion } from '../../shared/api/alertListApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { TextArea } from '../../shared/components/Field';
import type { Notice } from '../../shared/components/Toast';
import { linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel } from '../../shared/utils/labels';
import { addDays } from '../../shared/utils/dateRange';
import { formatDayTime, formatTime, thaiToday } from '../../shared/utils/thaiTime';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { alertDetail, alertResult, alertRole, alertTitle, coverNotice, fillLink, type ReviewTab } from './alertRules';
import { useAssignCoverMutation, useReviewAlertMutation } from '../../shared/api/alertsApi';

const MAX_NOTE = 500;

const keyOf = (alert: Alert) => `${alert.kind}-${alert.recordId}`;

interface AlertTableProps {
  alerts: readonly Alert[];
  /** Today's Thai date, to say when an alert was closed. */
  today: string;
  review: ReviewTab;
  onNotice: (notice: Notice) => void;
}

/**
 * The design file's D3 table of one kind of alert: when, who, what, what the system did, and what the Admin can do.
 * "คุยแล้ว ปิดเตือน" opens a note under its row, one row at a time. Server text is React text only.
 */
export function AlertTable({ alerts, today, review, onNotice }: AlertTableProps) {
  const [noting, setNoting] = useState<string | null>(null);

  if (alerts.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
        <Inbox aria-hidden="true" className="size-8 text-border-control" />
        <strong className="text-base">
          {review === 'reviewed' ? 'ยังไม่มีรายการที่คุยแล้ว' : 'ไม่มีรายการที่ต้องคุยในแท็บนี้'}
        </strong>
        <span className="text-text-2">
          {review === 'reviewed'
            ? 'กด "คุยแล้ว ปิดเตือน" ที่รายการ แล้วจะย้ายมาอยู่ที่นี่'
            : 'ดูรายการที่ปิดไปแล้วได้ที่ "คุยแล้ว"'}
        </span>
      </div>
    );
  }

  return (
    <Table framed={false} minWidth="min-w-225">
      <thead>
        <tr>
          <th scope="col" className="w-16">
            เวลา
          </th>
          <th scope="col" className="w-42.5">
            ผู้เกี่ยวข้อง
          </th>
          <th scope="col">เตือน</th>
          <th scope="col" className="w-32">
            ผลของระบบ
          </th>
          <th scope="col" className="w-82.5">
            สถานะ / การกระทำ
          </th>
        </tr>
      </thead>
      <tbody>
        {alerts.map((alert) => (
          // The same record id can come with two kinds (a far Shift-In and its missing Shift-Out)
          <AlertRow
            key={keyOf(alert)}
            alert={alert}
            today={today}
            noting={noting === keyOf(alert)}
            onNoting={(open) => setNoting(open ? keyOf(alert) : null)}
            onNotice={onNotice}
          />
        ))}
      </tbody>
    </Table>
  );
}

interface AlertRowProps {
  alert: Alert;
  today: string;
  noting: boolean;
  onNoting: (open: boolean) => void;
  onNotice: (notice: Notice) => void;
}

function AlertRow({ alert, today, noting, onNoting, onNotice }: AlertRowProps) {
  const [note, setNote] = useState('');
  const [reviewAlert, { isLoading }] = useReviewAlertMutation();
  const title = alertTitle(alert);
  const result = alertResult(alert);
  const missing = alert.kind === 'MissingAttendance';
  const open = !alert.reviewed && !missing;
  const name = alert.person.displayName;

  const send = async () => {
    try {
      await reviewAlert({
        kind: alert.kind,
        recordId: alert.recordId,
        note: note.trim() === '' ? undefined : note.trim(),
      }).unwrap();
      onNoting(false);
      onNotice({ kind: 'ok', text: `ปิดเตือน${title}ของ ${name} แล้ว` });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <>
      <tr className={noting ? '[&>*]:bg-accent-subtle/50' : alert.reviewed ? '[&>*]:bg-surface-2' : undefined}>
        <td className="align-top whitespace-nowrap tabular-nums">{missing ? '–' : formatTime(alert.at)}</td>
        <td className="align-top">
          <span className="flex flex-col">
            <strong>{name}</strong>
            <span className="text-xs text-text-2">{alertRole(alert)}</span>
          </span>
        </td>
        <td className="align-top">
          <span className="flex flex-col">
            <strong>{title}</strong>
            <span className="text-text-3">{alertDetail(alert)}</span>
          </span>
        </td>
        <td className="align-top">
          <span
            className={`inline-flex min-h-6 items-center rounded-sm border px-2 text-xs font-semibold whitespace-nowrap ${
              result.refused
                ? 'border-idle-border bg-idle-bg text-idle'
                : 'border-overdue-border bg-surface text-overdue'
            }`}
          >
            {result.text}
          </span>
        </td>
        <td>
          {open && (
            // The design file's fixed grid: each button keeps its place on every row, with or without a cover
            <div className="grid grid-cols-[172px_140px] items-center justify-end gap-2">
              {alert.cover ? (
                <CoverButton alert={alert} cover={alert.cover} onNotice={onNotice} />
              ) : (
                <span aria-hidden="true" />
              )}
              <Button
                size="desk"
                aria-expanded={noting}
                disabled={isLoading}
                // Open: the design file's pale blue with a 2 px frame, drawn as an inset ring so nothing moves
                className="ring-accent ring-inset aria-expanded:border-accent aria-expanded:bg-accent-subtle aria-expanded:ring-1"
                onClick={() => onNoting(!noting)}
              >
                <Check aria-hidden="true" className="size-4" />
                คุยแล้ว ปิดเตือน
              </Button>
            </div>
          )}
          {missing && !alert.reviewed && (
            // facility-0051: it closes once the time is filled, on the attendance page
            <div className="flex flex-col items-end gap-0.5">
              <Link
                className={`${linkClass} inline-flex min-h-8 items-center gap-1.5 font-semibold no-underline`}
                to={fillLink(alert)}
              >
                {`เติมเวลา${eventLabel[alert.missingEvents[0] ?? 'ShiftOut']}`}
                <ArrowRight aria-hidden="true" className="size-4" />
              </Link>
              <span className="text-xs text-text-2">เตือนปิดเองเมื่อเติมเวลาแล้ว</span>
            </div>
          )}
          {alert.reviewed && (
            <span className="inline-flex items-center gap-1.5 font-semibold text-passed">
              <Check aria-hidden="true" className="size-4" />
              {alert.reviewedAt ? `คุยแล้ว ${closedAt(alert.reviewedAt, today)}` : 'คุยแล้ว'}
            </span>
          )}
        </td>
      </tr>
      {noting && (
        <tr className="[&>*]:bg-accent-subtle/50">
          <td colSpan={5} className="px-4 py-3">
            <div className="flex flex-wrap items-end gap-4">
              <div className="flex flex-[1_1_420px] flex-col gap-1">
                <label className="flex flex-col gap-1">
                  <span className="font-semibold">
                    {`หมายเหตุการคุยกับ ${name} `}
                    <span className="font-normal text-text-2">(ไม่บังคับ)</span>
                  </span>
                  <TextArea
                    size="desk"
                    rows={2}
                    maxLength={MAX_NOTE}
                    placeholder="เช่น โทรคุยแล้ว ป้ายจุดนี้อยู่ใกล้พื้นที่อื่น ทำให้สแกนผิด"
                    value={note}
                    disabled={isLoading}
                    onChange={(event) => setNote(event.target.value)}
                  />
                </label>
                <span className="text-right text-xs text-text-2">{`${note.length} / ${MAX_NOTE}`}</span>
              </div>
              <div className="flex gap-2 pb-6">
                <Button size="desk" disabled={isLoading} onClick={() => onNoting(false)}>
                  ยกเลิก
                </Button>
                <Button variant="primary" size="desk" disabled={isLoading} onClick={() => void send()}>
                  ยืนยัน ปิดเตือน
                </Button>
              </div>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

/** "10:20" today, "เมื่อวาน 16:02" yesterday (the design file's words), the date before that. */
function closedAt(moment: string, today: string): string {
  const date = thaiToday(new Date(moment));
  if (date === today) {
    return formatTime(moment);
  }
  return date === addDays(today, -1) ? `เมื่อวาน ${formatTime(moment)}` : formatDayTime(moment);
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
  const names = useBuildingNames();
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
      onNotice({ kind: 'ok', text: coverNotice(made, names) });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <Button
      variant="primary"
      size="desk"
      className="px-2.5 whitespace-nowrap"
      disabled={isLoading}
      onClick={() => void give()}
    >
      <ArrowRightLeft aria-hidden="true" className="size-4" />
      {cover.areaId != null ? `ให้ทำแทน ${alert.areaCode} กะนี้` : 'ให้ตรวจแทนตึกนี้กะนี้'}
    </Button>
  );
}
