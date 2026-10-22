import { Link } from 'react-router';
import type { AdminSign, AreaDetail } from '../../shared/api/adminSetupApi';
import { roundsText, signPlaceLabel } from '../../shared/utils/labels';

/** Which row's editor is open below the table. */
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' } | { kind: 'check-in' };

const placeText = (sign: AdminSign) => `${signPlaceLabel(sign)} · รัศมี ${sign.radiusM} ม.`;

/**
 * Wireframe D4's table: the Area's check-in sign first, then its points in the server's order, deactivated ones
 * marked. Signs of a deactivated point or Area cannot be scanned, so they are not offered for printing.
 */
export function SignTable({ detail, onEdit }: { detail: AreaDetail; onEdit: (target: EditTarget) => void }) {
  const { area, checkInSign } = detail;
  const printLink = (sign: AdminSign) => `/admin/print?area=${area.id}&sign=${sign.id}`;

  return (
    <>
      <table className="app-table areas-signs" aria-label="ป้ายของพื้นที่นี้">
        <thead>
          <tr>
            <th scope="col">ป้าย</th>
            <th scope="col">ช่วงเวลารอบ</th>
            <th scope="col">พิกัด · ความแม่นยำ</th>
            <th scope="col">จัดการ</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <th scope="row">
              <strong>{checkInSign.code}</strong> ป้ายลงเวลา
              <span className="app-muted areas-kind">ป้ายลงเวลา 1 ป้ายต่อพื้นที่</span>
            </th>
            <td>เข้างาน ออกพัก กลับจากพัก เลิกงาน</td>
            <td>{placeText(checkInSign)}</td>
            <td className="areas-actions">
              <button
                type="button"
                className="app-link-button"
                aria-label={`แก้ไข ${checkInSign.code}`}
                onClick={() => onEdit({ kind: 'check-in' })}
              >
                แก้ไข
              </button>
              {area.isActive && <Link to={printLink(checkInSign)}>พิมพ์</Link>}
            </td>
          </tr>
          {detail.points.map((point) => (
            <tr key={point.id} className={point.isActive ? undefined : 'app-row--inactive'}>
              <th scope="row">
                <strong>{point.sign.code}</strong> {point.name}
                <span className="app-muted areas-kind">
                  {point.isActive ? 'จุดทำความสะอาด' : 'จุดทำความสะอาด · ปิดใช้งาน'}
                </span>
              </th>
              <td>{roundsText(point.roundWindows)}</td>
              <td>{placeText(point.sign)}</td>
              <td className="areas-actions">
                <button
                  type="button"
                  className="app-link-button"
                  aria-label={`แก้ไข ${point.sign.code}`}
                  onClick={() => onEdit({ kind: 'point', pointId: point.id })}
                >
                  แก้ไข
                </button>
                {area.isActive && point.isActive && <Link to={printLink(point.sign)}>พิมพ์</Link>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {detail.points.length === 0 && <p className="app-muted">พื้นที่นี้ยังไม่มีจุดทำความสะอาด</p>}
      {area.isActive && (
        <button type="button" className="app-button areas-add-point" onClick={() => onEdit({ kind: 'new-point' })}>
          + เพิ่มจุดทำความสะอาดในพื้นที่นี้
        </button>
      )}
    </>
  );
}
