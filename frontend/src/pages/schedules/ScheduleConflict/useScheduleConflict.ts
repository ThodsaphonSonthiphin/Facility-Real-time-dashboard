import type { ScheduleConflictProps } from './type';

export function useScheduleConflict({ draft, latest }: ScheduleConflictProps) {
  const current = new Map(latest?.map((line) => [line.id, line.before]));
  return draft.map((line) => ({
    ...line,
    current: current.get(line.id) ?? 'ไม่มีข้อมูลล่าสุดที่ดูได้',
  }));
}
