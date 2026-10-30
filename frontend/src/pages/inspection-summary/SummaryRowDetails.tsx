import { MoreInfo } from '../../shared/components/MoreInfo';
import { pointsText } from './summaryRules';
import type { SummaryRowDetailsProps } from './type';

export function SummaryRowDetails({ row, id, expanded, place, share }: SummaryRowDetailsProps) {
  return (
    <tr hidden={!expanded}>
      <td colSpan={8} className="bg-surface-2 px-4 py-3">
        <div id={id} className="flex flex-col gap-2" role="region" aria-label={`รายละเอียดของ ${row.displayName}`}>
          <strong>จุดที่ไม่ได้ตรวจเลยในช่วงนี้ · {row.displayName}</strong>
          {row.uninspectedPoints?.length > 0 ? (
            <ul className="m-0 flex list-none flex-wrap gap-2 p-0" aria-label={`จุดที่ไม่ได้ตรวจของ ${row.displayName}`}>
              {row.uninspectedPoints.map((point) => (
                <li key={point.pointId} title={point.signCode} className="inline-flex min-h-7 items-center gap-1 rounded-md border border-border bg-surface px-2.5">
                  <span>{point.areaCode}</span> {point.pointName}
                </li>
              ))}
            </ul>
          ) : <span className="text-text-2">{row.pointsTotal > row.pointsInspected ? 'ยังไม่มีรายชื่อจุด กรุณาโหลดข้อมูลใหม่' : 'ไม่มีจุดที่ไม่ได้ตรวจในช่วงนี้'}</span>}
          <MoreInfo summary="ข้อมูลรวมเพิ่มเติม">
            <dl className="grid grid-cols-2 gap-x-6 gap-y-1">
              <dt>รหัสพนักงาน</dt><dd>{row.employeeId}</dd>
              <dt>ตึก · กะ</dt><dd>{place}</dd>
              <dt>จุดที่ตรวจแล้ว</dt><dd>{pointsText(row)}</dd>
              <dt>งานที่แม่บ้านส่ง</dt><dd className="tabular-nums">{row.submitted}</dd>
              <dt>ตรวจแล้ว</dt><dd className="tabular-nums">{row.submittedInspected}</dd>
              <dt>สัดส่วนที่ตรวจ</dt>
              <dd>{share == null ? '–' : <span className="inline-flex items-center gap-2 tabular-nums"><progress className="h-2 w-30 accent-text-2" max={100} value={share} aria-label={`ตรวจ ${share}% ของงานที่ส่ง`} />{share}%</span>}</dd>
            </dl>
          </MoreInfo>
        </div>
      </td>
    </tr>
  );
}
