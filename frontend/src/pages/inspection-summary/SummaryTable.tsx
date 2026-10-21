import type { SummaryRow } from './summaryApi';
import { inspectedShare, placeText, pointsText } from './summaryRules';

/** Wireframe D7's table, in the server's order, plus "ตรวจทั้งหมด" (facility-0048 rule 5). Names are React text only. */
export function SummaryTable({ rows }: { rows: SummaryRow[] }) {
  if (rows.length === 0) {
    return <p className="app-note app-note--info">ไม่มีหัวหน้าที่เปิดใช้งานในตึกที่เลือก</p>;
  }
  return (
    <div className="app-table-scroll">
      <table className="app-table summary-table" aria-label="การตรวจของหัวหน้าแต่ละคน">
        <thead>
          <tr>
            <th scope="col">หัวหน้า</th>
            <th scope="col">ตึก · กะ</th>
            <th scope="col">จุดที่ตรวจแล้ว</th>
            <th scope="col">ตรวจทั้งหมด</th>
            <th scope="col">งานที่แม่บ้านส่ง</th>
            <th scope="col">ตรวจแล้ว</th>
            <th scope="col">สัดส่วนที่ตรวจ</th>
            <th scope="col">ไม่ได้ตรวจ</th>
            <th scope="col">ให้แก้ไข</th>
            <th scope="col">ตรวจที่ติดธง GPS</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.supervisorId}>
              <th scope="row">
                {row.displayName}
                <span className="summary-sub">{row.employeeId}</span>
              </th>
              <td>{placeText(row)}</td>
              <td className={row.pointsInspected < row.pointsTotal ? 'summary-missed' : undefined}>{pointsText(row)}</td>
              <td className="summary-number">{row.inspections}</td>
              <td className="summary-number">{row.submitted}</td>
              <td className="summary-number">{row.submittedInspected}</td>
              <td>
                <Share row={row} />
              </td>
              <td className="summary-number">{row.notInspected}</td>
              <td className="summary-number">{row.reworks}</td>
              <td className="summary-number">{row.gpsFlagged}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** A <progress> bar draws the share without an inline width (facility-0063). */
function Share({ row }: { row: SummaryRow }) {
  const share = inspectedShare(row);
  if (share == null) {
    return <>–</>;
  }
  return (
    <span className="summary-share">
      <progress max={100} value={share} aria-label={`ตรวจ ${share}% ของงานที่ส่ง`} />
      {`${share}%`}
    </span>
  );
}
