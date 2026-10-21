import type { DateRange } from '../../shared/utils/dateRange';

/** ExportSet on the server (facility-0053). */
export type ExportSet = 'Attendance' | 'Submissions' | 'Inspections';

/** ExportRules.MaxDays. */
export const EXPORT_MAX_DAYS = 31;

/** The order of the sheets in the file and of the choices on the page. */
export const exportSets: readonly ExportSet[] = ['Attendance', 'Submissions', 'Inspections'];

/** Wireframe D8's choices, word for word. */
export const setTitle: Record<ExportSet, string> = {
  Attendance: 'การลงเวลา',
  Submissions: 'การส่งงาน',
  Inspections: 'การตรวจงาน',
};

export const setDetail: Record<ExportSet, string> = {
  Attendance: 'คน · Area · กะ · เวลา 4 ครั้ง · ที่มา (สแกนเอง / แก้โดย Admin) · ธง GPS',
  Submissions: 'จุด · รอบ · เวลา · ผล (ตรงเวลา / ส่งช้า / นอกรอบ) · Issue · ทำแทนหรือไม่ · ธง GPS · รอบที่ไม่ได้ทำ',
  Inspections: 'หัวหน้า · จุด · รอบ · ผล · ข้อบกพร่อง · ธง GPS · รอบที่ไม่ได้ตรวจ / ไม่ได้แก้',
};

/** AdminExportEndpoints names the file facility_<from>_<to>.xlsx. */
export function fileNameFor({ from, to }: DateRange): string {
  return `facility_${from}_${to}.xlsx`;
}

/** The name in "attachment; filename=…; filename*=UTF-8''…", or the server's pattern when there is none. */
export function fileNameOf(disposition: string | null, range: DateRange): string {
  const match = disposition ? /filename="?([^";]+)"?/.exec(disposition) : null;
  return match ? match[1] : fileNameFor(range);
}
