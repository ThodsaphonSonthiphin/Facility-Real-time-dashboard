import { Badge } from '../../../shared/components/Badge';
import type { ScheduleHistoryProps } from './type';
import { useScheduleHistory } from './useScheduleHistory';

export function ScheduleHistory(props: ScheduleHistoryProps) {
  const entries = useScheduleHistory(props);
  if (!entries.length) return null;

  return (
    <section aria-label="ประวัติการแก้ไขแผน" className="space-y-2">
      <h3 className="text-lg font-semibold">ประวัติการแก้ไขแผน</h3>
      <ul className="space-y-2">
        {entries.map((entry) => (
          <li key={entry.key} className="space-y-1 rounded-lg border border-border p-3">
            <Badge kind="edited">แก้ไขแผนย้อนหลัง</Badge>
            <p>{entry.date} · {entry.actor}</p>
            <p>{entry.label}ในแผนเดิม · {entry.before ?? 'ตามตารางประจำ'}</p>
            <p>{entry.label}ในแผนที่แก้ · {entry.after ?? 'ตามตารางประจำ'}</p>
            <p>เหตุผล · {entry.reason}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
