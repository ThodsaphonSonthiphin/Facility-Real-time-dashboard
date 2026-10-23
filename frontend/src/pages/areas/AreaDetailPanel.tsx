import { useState } from 'react';
import { Link } from 'react-router';
import type { AdminPoint, AreaDetail } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { boxClass, buttonClass, linkClass } from '../../shared/components/styles';
import { Toast, type Notice } from '../../shared/components/Toast';
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
  const [notice, setNotice] = useState<Notice | null>(null);

  const open = (target: EditTarget) => {
    setEditing(target);
    setNotice(null);
  };
  // A new point becomes the point being edited; until the refreshed Area arrives, no editor shows. The answer to the
  // save floats (Toast), as on D2 and D3 (owner, #78)
  const saved = (point: AdminPoint, message: string) => {
    setEditing({ kind: 'point', pointId: point.id });
    setNotice({ kind: 'ok', text: message });
  };
  const editedPoint = editing?.kind === 'point' ? detail.points.find((point) => point.id === editing.pointId) : undefined;

  return (
    <section className="flex flex-col gap-3" aria-labelledby="areas-detail-title">
      <header className="flex flex-col gap-1">
        <span className="text-muted">{`พื้นที่ที่เลือก · ตึก ${area.buildingCode}`}</span>
        <h2 id="areas-detail-title" className="text-xl font-bold">{`${area.code} ${area.name}`}</h2>
        <p className="text-muted">{areaLine(detail)}</p>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <Link className={buttonClass('secondary', 'desk')} data-tap="" to={`/admin/areas/${area.id}/edit`}>
            แก้ไขพื้นที่
          </Link>
          {area.isActive && (
            <Link className={linkClass} to={`/admin/print?area=${area.id}`}>
              พิมพ์ป้ายของพื้นที่นี้ ›
            </Link>
          )}
          {area.isActive && (
            <Link className={linkClass} to={`/admin/areas/${area.id}/capture`}>
              เก็บพิกัดบนมือถือ ›
            </Link>
          )}
        </div>
      </header>
      {!area.isActive && (
        <Note tone="warn">พื้นที่นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไขพื้นที่"</Note>
      )}
      <SignTable detail={detail} onEdit={open} />
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
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
        <section className={`flex flex-col gap-3 ${boxClass}`} aria-labelledby="checkin-editor">
          <h3 id="checkin-editor" className="text-lg font-semibold">{`ป้ายลงเวลา · ${area.code} ${area.name}`}</h3>
          <SignPanel
            key={detail.checkInSign.id}
            areaId={area.id}
            sign={detail.checkInSign}
            label="ป้ายลงเวลา"
            canIssueQr={area.isActive}
          />
          <Button size="desk" onClick={() => setEditing(null)}>
            ปิด
          </Button>
        </section>
      )}
    </section>
  );
}
