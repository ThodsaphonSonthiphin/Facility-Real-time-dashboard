import { ArrowRightLeft, CircleCheck, Users, UserX } from 'lucide-react';
import type { ScheduleWeek } from '../../../shared/api/scheduleTypes';
import { KpiCard } from '../../../shared/components/KpiCard/KpiCard';

export interface ScheduleSummaryCardsProps {
  week: ScheduleWeek;
  businessDate: string;
}

/**
 * Four real-data KPI summary cards for the management schedule roster on `businessDate`
 * (or the first visible date when viewing another week).
 */
export function ScheduleSummaryCards({ week, businessDate }: ScheduleSummaryCardsProps) {
  const hasBusinessDate = week.rows.some((row) => row.days.some((day) => day.date === businessDate));
  const targetDate = hasBusinessDate ? businessDate : (week.rows[0]?.days[0]?.date ?? week.weekStart);

  let working = 0;
  let leaveOrOff = 0;
  let needsAttention = 0;

  for (const row of week.rows) {
    const day = row.days.find((one) => one.date === targetDate);
    if (!day) continue;
    if (day.availability === 'available') {
      working += 1;
    } else if (day.availability === 'leave' || day.availability === 'day-off') {
      leaveOrOff += 1;
    }
    if (day.problems.length > 0 || day.availability === 'unset') {
      needsAttention += 1;
    }
  }

  return (
    <ul aria-label="สรุปตารางงาน" className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <KpiCard as="li" label="พนักงานทั้งหมด" value={week.rows.length} icon={Users} tone="blue" />
      <KpiCard as="li" label="กำลังทำงาน" value={working} icon={CircleCheck} tone="green" />
      <KpiCard as="li" label="ลา / หยุด" value={leaveOrOff} icon={UserX} tone="amber" />
      <KpiCard as="li" label="มีปัญหา / รอจัดคนแทน" value={needsAttention} icon={ArrowRightLeft} tone="purple" />
    </ul>
  );
}
