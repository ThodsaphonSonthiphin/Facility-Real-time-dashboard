import { useState } from 'react';
import { Link } from 'react-router';
import type { AdminPoint, AreaDetail } from '../../shared/api/adminSetupApi';
import { areaLine } from './areaText';
import { PointEditor } from './PointEditor';
import { SignPanel } from './SignPanel';
import { SignTable, type EditTarget } from './SignTable';

/**
 * Wireframe D4, right: the open Area, its signs, and the editor of the row being edited. AreasPage keys it by Area
 * id, so opening another Area closes the editor.
 */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [editing, setEditing] = useState<EditTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const open = (target: EditTarget) => {
    setEditing(target);
    setNotice(null);
  };
  // A new point becomes the point being edited; until the refreshed Area arrives, no editor shows
  const saved = (point: AdminPoint, message: string) => {
    setEditing({ kind: 'point', pointId: point.id });
    setNotice(message);
  };
  const editedPoint = editing?.kind === 'point' ? detail.points.find((point) => point.id === editing.pointId) : undefined;

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
      <SignTable detail={detail} onEdit={open} />
      {notice && (
        <p className="app-note app-note--info" role="status">
          {notice}
        </p>
      )}
      {editing?.kind === 'new-point' && (
        <PointEditor key="new" area={area} onSaved={saved} onClose={() => setEditing(null)} />
      )}
      {editedPoint && (
        <PointEditor
          key={editedPoint.id}
          area={area}
          point={editedPoint}
          onSaved={saved}
          onClose={() => setEditing(null)}
        />
      )}
      {editing?.kind === 'check-in' && (
        <section className="areas-editor app-panel" aria-labelledby="checkin-editor">
          <h3 id="checkin-editor">{`ป้ายลงเวลา · ${area.code} ${area.name}`}</h3>
          <SignPanel
            key={detail.checkInSign.id}
            areaId={area.id}
            sign={detail.checkInSign}
            label="ป้ายลงเวลา"
            canIssueQr={area.isActive}
          />
          <div className="areas-editor__buttons">
            <button type="button" className="app-button" onClick={() => setEditing(null)}>
              ปิด
            </button>
          </div>
        </section>
      )}
    </section>
  );
}
