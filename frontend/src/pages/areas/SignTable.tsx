import { Link } from 'react-router';
import type { AdminSign, AreaDetail } from '../../shared/api/adminSetupApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { buttonClass } from '../../shared/components/styles';
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
  // One row's actions sit on one line; a deactivated row's words are muted, its links keep a link's colour
  const actionsClass = 'flex items-center gap-3 whitespace-nowrap';

  return (
    <>
      <Table label="ป้ายของพื้นที่นี้">
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
            <th scope="row" className="font-normal">
              <strong>{checkInSign.code}</strong> ป้ายลงเวลา
              <span className="block text-muted">ป้ายลงเวลา 1 ป้ายต่อพื้นที่</span>
            </th>
            <td>เข้างาน ออกพัก กลับจากพัก เลิกงาน</td>
            <td>{placeText(checkInSign)}</td>
            <td>
              <div className={actionsClass}>
                <Button
                  variant="link"
                  size="desk"
                  aria-label={`แก้ไข ${checkInSign.code}`}
                  onClick={() => onEdit({ kind: 'check-in' })}
                >
                  แก้ไข
                </Button>
                {area.isActive && (
                  <Link className={buttonClass('link', 'desk')} data-tap="" to={printLink(checkInSign)}>
                    พิมพ์
                  </Link>
                )}
              </div>
            </td>
          </tr>
          {detail.points.map((point) => (
            <tr key={point.id} className={point.isActive ? undefined : 'text-muted'}>
              <th scope="row" className="font-normal">
                <strong>{point.sign.code}</strong> {point.name}
                <span className="block text-muted">
                  {point.isActive ? 'จุดทำความสะอาด' : 'จุดทำความสะอาด · ปิดใช้งาน'}
                </span>
              </th>
              <td>{roundsText(point.roundWindows)}</td>
              <td>{placeText(point.sign)}</td>
              <td>
                <div className={actionsClass}>
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`แก้ไข ${point.sign.code}`}
                    onClick={() => onEdit({ kind: 'point', pointId: point.id })}
                  >
                    แก้ไข
                  </Button>
                  {area.isActive && point.isActive && (
                    <Link className={buttonClass('link', 'desk')} data-tap="" to={printLink(point.sign)}>
                      พิมพ์
                    </Link>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </Table>
      {detail.points.length === 0 && <p className="text-muted">พื้นที่นี้ยังไม่มีจุดทำความสะอาด</p>}
      {area.isActive && (
        <div>
          <Button size="desk" onClick={() => onEdit({ kind: 'new-point' })}>
            + เพิ่มจุดทำความสะอาดในพื้นที่นี้
          </Button>
        </div>
      )}
    </>
  );
}
