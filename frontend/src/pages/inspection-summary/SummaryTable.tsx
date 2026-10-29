import { Table } from '../../shared/components/Boxes';
import { PageEmpty } from '../../shared/components/PageState';
import { tableCell } from '../../shared/components/styles';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { SummaryRow } from './summaryApi';
import { inspectedShare, placeText, pointsText } from './summaryRules';

/** Wireframe D7's table, in the server's order, plus "จำนวนครั้งที่ตรวจ" (facility-0048 rule 5). Names are React text only. */
export function SummaryTable({ rows }: { rows: SummaryRow[] }) {
  const names = useBuildingNames();
  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-surface">
        <PageEmpty title="ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก">เลือกตึกอื่น หรือเลือกทุกตึกเพื่อดูทุกคน</PageEmpty>
      </div>
    );
  }
  return (
    <Table label="การตรวจของหัวหน้าแต่ละคน">
      <thead>
        <tr>
          <th scope="col">หัวหน้า</th>
          <th scope="col">ตึก · กะ</th>
          <th scope="col">จุดที่ตรวจแล้ว</th>
          <th scope="col" className={tableCell.num}>
            จำนวนครั้งที่ตรวจ
          </th>
          <th scope="col" className={tableCell.num}>
            งานที่แม่บ้านส่ง
          </th>
          <th scope="col" className={tableCell.num}>
            ตรวจแล้ว
          </th>
          <th scope="col">สัดส่วนที่ตรวจ</th>
          <th scope="col" className={tableCell.num}>
            ไม่ได้ตรวจ
          </th>
          <th scope="col" className={tableCell.num}>
            ให้แก้ไข
          </th>
          <th scope="col" className={tableCell.num}>
            ตรวจตอนอยู่ไกลป้าย
          </th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.supervisorId}>
            <th scope="row">
              {row.displayName}
              <span className="block text-xs font-normal text-text-2">{row.employeeId}</span>
            </th>
            <td>{placeText(row, names)}</td>
            <td
              className={
                row.pointsInspected < row.pointsTotal
                  ? 'font-semibold whitespace-nowrap text-overdue tabular-nums'
                  : 'tabular-nums'
              }
            >
              {pointsText(row)}
            </td>
            <td className={tableCell.num}>{row.inspections}</td>
            <td className={tableCell.num}>{row.submitted}</td>
            <td className={tableCell.num}>{row.submittedInspected}</td>
            <td>
              <Share row={row} />
            </td>
            <td className={tableCell.num}>{row.notInspected}</td>
            <td className={tableCell.num}>{row.reworks}</td>
            <td className={tableCell.num}>{row.gpsFlagged}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

/**
 * A <progress> bar draws the share without an inline width. Grey, not primary: blue means "can be pressed", and the
 * page judges nothing (plan 12 decision 4). `w-30` is the old 120 px bar.
 */
function Share({ row }: { row: SummaryRow }) {
  const share = inspectedShare(row);
  if (share == null) {
    return <>–</>;
  }
  return (
    <span className="inline-flex items-center gap-2 text-xs tabular-nums">
      <progress className="h-2 w-30 accent-text-2" max={100} value={share} aria-label={`ตรวจ ${share}% ของงานที่ส่ง`} />
      {`${share}%`}
    </span>
  );
}
