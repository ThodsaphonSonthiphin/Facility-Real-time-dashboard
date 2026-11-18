import type { ScheduleHistoryProps } from './type';

const dateFormatter = new Intl.DateTimeFormat('th-TH', {
  timeZone: 'Asia/Bangkok', dateStyle: 'medium', timeStyle: 'short',
});

export function useScheduleHistory({ corrections }: ScheduleHistoryProps) {
  return corrections.map((entry, index) => ({
    ...entry,
    key: `${entry.at}:${entry.field}:${index}`,
    date: dateFormatter.format(new Date(entry.at)),
    label: entry.field === 'areaId' ? 'พื้นที่' : 'กลุ่มพัก',
  }));
}
