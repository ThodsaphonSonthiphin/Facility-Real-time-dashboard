import type { ReactNode } from 'react';
import { Table } from '../../shared/components/Boxes';
import { Note } from '../../shared/components/Note';
import { formatDayTime } from '../../shared/utils/thaiTime';
import type { AuditEntry } from './historyApi';
import { actionText } from './historyRules';
import { HistoryChangeValue } from './HistoryChangeValue';

interface HistoryTableProps {
  entries: AuditEntry[];
  /** What to say when there is no row. */
  emptyText: string;
  /** Truthful ranges and previous/next cursor controls below the table. */
  footer?: ReactNode;
}

/** Wireframe D18's table, newest first. Summaries, names and reasons are React text only (facility-0066). */
export function HistoryTable({ entries, emptyText, footer }: HistoryTableProps) {
  if (entries.length === 0) {
    return <div className="overflow-hidden rounded-lg border border-border bg-surface"><Note tone="info">{emptyText}</Note>{footer}</div>;
  }
  return (
    <Table label="ประวัติการแก้ไขของแอดมิน" minWidth="min-w-240" footer={footer}>
      <colgroup>
        <col className="w-32" />
        <col className="w-35" />
        <col className="w-56" />
        <col className="w-85" />
        <col className="w-37" />
      </colgroup>
      <thead>
        <tr>
          <th scope="col">เวลา</th>
          <th scope="col">ผู้ทำ</th>
          <th scope="col">แก้อะไร</th>
          <th scope="col">ก่อน → หลัง</th>
          <th scope="col">เหตุผล</th>
        </tr>
      </thead>
      <tbody>
        {entries.map((entry) => (
          <tr key={entry.id}>
            <td className="whitespace-nowrap tabular-nums">{formatDayTime(entry.occurredAt)}</td>
            <td>{entry.actorName}</td>
            <td>
              <div className="flex flex-col items-start gap-1.5"><span className="rounded-sm border border-idle-border bg-idle-bg px-2 py-0.5 text-xs font-semibold text-text-3">{actionText(entry.action)}</span><span>{entry.summary}</span></div>
            </td>
            <td>
              <HistoryChangeValue entry={entry} />
            </td>
            <td className="text-text-2">{entry.reason ?? '–'}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
