import { Table } from '../../shared/components/Boxes';
import { Note } from '../../shared/components/Note';
import { formatDayTime } from '../../shared/utils/thaiTime';
import type { AuditEntry } from './historyApi';
import { actionText, changeLines } from './historyRules';

interface HistoryTableProps {
  entries: AuditEntry[];
  /** What to say when there is no row. */
  emptyText: string;
}

/** Wireframe D9's table, newest first. Summaries, names and reasons are React text only (facility-0066). */
export function HistoryTable({ entries, emptyText }: HistoryTableProps) {
  if (entries.length === 0) {
    return <Note tone="info">{emptyText}</Note>;
  }
  return (
    <Table label="ประวัติการแก้ไขของแอดมิน">
      <thead>
        <tr>
          <th scope="col">เวลา</th>
          <th scope="col">ผู้ทำ</th>
          <th scope="col">ประเภท</th>
          <th scope="col">รายการที่ถูกแก้</th>
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
              <strong>{actionText(entry.action)}</strong>
            </td>
            <td>{entry.summary}</td>
            <td>
              <Changes entry={entry} />
            </td>
            <td className="text-text-2">{entry.reason ?? '–'}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function Changes({ entry }: { entry: AuditEntry }) {
  const lines = changeLines(entry);
  if (lines.length === 0) {
    return <>–</>;
  }
  return (
    <ul className="m-0 list-disc pl-4.5">
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  );
}
