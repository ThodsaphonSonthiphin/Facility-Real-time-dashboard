import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { areaLine } from './areaText';
import { SignTable } from './SignTable';

/** Wireframe D4, right: the open Area and its signs. AreasPage keys it by Area id. */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const { area } = detail;

  return (
    <section className="areas-detail" aria-labelledby="areas-detail-title">
      <header className="areas-detail__head">
        <span className="app-muted">{`Area ที่เลือก · ตึก ${area.buildingCode}`}</span>
        <h2 id="areas-detail-title">{`${area.code} ${area.name}`}</h2>
        <p>{areaLine(detail)}</p>
        <div className="areas-detail__links">
          <Link className="app-button" to={`/admin/areas/${area.id}/edit`}>
            แก้ไข Area
          </Link>
          {area.isActive && <Link to={`/admin/print?area=${area.id}`}>พิมพ์ป้ายของ Area นี้ ›</Link>}
          {area.isActive && <Link to={`/admin/areas/${area.id}/capture`}>เก็บพิกัดบนมือถือ ›</Link>}
        </div>
      </header>
      {!area.isActive && (
        <p className="app-note app-note--warn">Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"</p>
      )}
      <SignTable detail={detail} />
    </section>
  );
}
