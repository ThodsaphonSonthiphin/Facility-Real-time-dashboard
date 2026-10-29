import type { ReactNode } from 'react';
import { Table } from '../../shared/components/Boxes';
import { Note } from '../../shared/components/Note';
import { useBuildingNames } from '../../shared/api/buildingNames';
import type { SummaryRow } from './summaryApi';
import { inspectedShare, placeText, pointsText } from './summaryRules';

/** Wireframe D7's table, in the server's order, plus "จำนวนครั้งที่ตรวจ" (facility-0048 rule 5). Names are React text only. */
export function SummaryTable({ rows }: { rows: SummaryRow[] }) {
  const names = useBuildingNames();
  if (rows.length === 0) {
    return <Note tone="info">ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก</Note>;
  }
  return (
    <Table label="การตรวจของหัวหน้าแต่ละคน">
      <thead>
        <tr>
          <th scope="col">หัวหน้า</th>
          <th scope="col">ตึก · กะ</th>
          <th scope="col">จุดที่ตรวจแล้ว</th>
          <th scope="col">
            <Count>จำนวนครั้งที่ตรวจ</Count>
          </th>
          <th scope="col">
            <Count>งานที่แม่บ้านส่ง</Count>
          </th>
          <th scope="col">
            <Count>ตรวจแล้ว</Count>
          </th>
          <th scope="col">สัดส่วนที่ตรวจ</th>
          <th scope="col">
            <Count>ไม่ได้ตรวจ</Count>
          </th>
          <th scope="col">
            <Count>ให้แก้ไข</Count>
          </th>
          <th scope="col">
            <Count>ตรวจตอนอยู่ไกลป้าย</Count>
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
            <td>
              <Count>{row.inspections}</Count>
            </td>
            <td>
              <Count>{row.submitted}</Count>
            </td>
            <td>
              <Count>{row.submittedInspected}</Count>
            </td>
            <td>
              <Share row={row} />
            </td>
            <td>
              <Count>{row.notInspected}</Count>
            </td>
            <td>
              <Count>{row.reworks}</Count>
            </td>
            <td>
              <Count>{row.gpsFlagged}</Count>
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

/**
 * A count, or its column's heading, on the right of its cell, so the heading sits over the numbers (pages/admin.md);
 * Table sets every cell's alignment, so the span carries its own.
 */
function Count({ children }: { children: ReactNode }) {
  return <span className="block text-right tabular-nums">{children}</span>;
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
