import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { buttonClass } from '../../shared/components/styles';
import { roundsText } from '../../shared/utils/labels';
import { signPlaceText } from './areaText';

/** Which point the Drawer holds the editor of. */
export type EditTarget = { kind: 'point'; pointId: number } | { kind: 'new-point' };

/**
 * Wireframe D4's Tab 1 (owner, 2026-10-23): only the Area's points, in the server's order, a deactivated one marked,
 * with "+ เพิ่มจุด" at the table's top right. Signs of a deactivated point or Area cannot be scanned, so they are not
 * offered for printing. The check-in sign has its own tab, so it is not a row here.
 */
export function PointTable({ detail, onEdit }: { detail: AreaDetail; onEdit: (target: EditTarget) => void }) {
  const { area, points } = detail;
  const printLink = (signId: number) => `/admin/print?area=${area.id}&sign=${signId}`;
  // One row's actions sit on one line; a deactivated row's words are muted, its links keep a link's colour
  const actionsClass = 'flex items-center gap-3 whitespace-nowrap';

  return (
    <section className="flex flex-col gap-2" aria-labelledby="points-title">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 id="points-title" className="text-base font-bold">{`จุดทำความสะอาด (${points.length})`}</h3>
        {area.isActive && (
          <Button size="desk" onClick={() => onEdit({ kind: 'new-point' })}>
            + เพิ่มจุด
          </Button>
        )}
      </div>
      {points.length === 0 ? (
        <p className="text-muted">พื้นที่นี้ยังไม่มีจุดทำความสะอาด</p>
      ) : (
        <Table label="จุดทำความสะอาดของพื้นที่นี้">
          <thead>
            <tr>
              <th scope="col">จุด</th>
              <th scope="col">ช่วงเวลารอบ</th>
              <th scope="col">พิกัด · ความแม่นยำ</th>
              <th scope="col">จัดการ</th>
            </tr>
          </thead>
          <tbody>
            {points.map((point) => (
              <tr key={point.id} className={point.isActive ? undefined : 'text-muted'}>
                <th scope="row" className="font-normal">
                  <strong>{point.sign.code}</strong> {point.name}
                  <span className="block text-muted">
                    {point.isActive ? 'จุดทำความสะอาด' : 'จุดทำความสะอาด · ปิดใช้งาน'}
                  </span>
                </th>
                <td>{roundsText(point.roundWindows)}</td>
                <td>{signPlaceText(point.sign)}</td>
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
                      <Link className={buttonClass('link', 'desk')} data-tap="" to={printLink(point.sign.id)}>
                        พิมพ์
                      </Link>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </section>
  );
}
