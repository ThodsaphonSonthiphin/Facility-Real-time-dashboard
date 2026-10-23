import { useState } from 'react';
import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { boxClass, buttonClass, linkClass, toggleClass } from '../../shared/components/styles';
import { Toast, type Notice } from '../../shared/components/Toast';
import { AreaEditorDrawer } from './AreaEditorDrawer';
import { areaLine, signPlaceText } from './areaText';
import { PointEditor } from './PointEditor';
import { PointTable, type EditTarget } from './PointTable';
import { SignPanel } from './SignPanel';
import { pointEditorTitleId } from './pointRules';

type Tab = 'points' | 'check-in';

/**
 * Wireframe D4, right (owner, 2026-10-23): the open Area's name, shifts and Cleaners with its two main actions, then
 * two tabs — its points, and the check-in sign's place and QR — so the page is no longer one long column. Both
 * editors open in the right-hand Drawer, so closing one brings the Admin back to the same table row. AreasPage keys
 * it by Area id, so opening another Area closes everything.
 */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [tab, setTab] = useState<Tab>('points');
  const [editing, setEditing] = useState<EditTarget | null>(null);
  const [areaOpen, setAreaOpen] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const editedPoint = editing?.kind === 'point' ? detail.points.find((point) => point.id === editing.pointId) : undefined;
  // A new point has no saved details yet; an edited one is looked up again, so a change from another device shows
  const drawerPoint = editing?.kind === 'new-point' ? undefined : editedPoint;
  const drawerId = pointEditorTitleId(drawerPoint?.id);
  const drawerOpen = editing !== null && (editing.kind === 'new-point' || editedPoint !== undefined);
  const closeEditor = () => setEditing(null);

  const open = (target: EditTarget) => {
    setEditing(target);
    setNotice(null);
  };
  // A save closes the Drawer: the answer floats over the page, not under the Drawer's backdrop (owner, #78)
  const saved = (text: string) => {
    setEditing(null);
    setAreaOpen(false);
    setNotice({ kind: 'ok', text });
  };

  return (
    <section className="flex flex-col gap-3" aria-labelledby="areas-detail-title">
      <header className="flex flex-col gap-1">
        <span className="text-muted">{`พื้นที่ที่เลือก · ตึก ${area.buildingCode}`}</span>
        <h2 id="areas-detail-title" className="text-xl font-bold">{`${area.code} ${area.name}`}</h2>
        <p className="text-muted">{areaLine(detail)}</p>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <Button
            size="desk"
            onClick={() => {
              setAreaOpen(true);
              setNotice(null);
            }}
          >
            แก้ไขพื้นที่
          </Button>
          {area.isActive && (
            <Link className={buttonClass('secondary', 'desk')} data-tap="" to={`/admin/print?area=${area.id}`}>
              พิมพ์ป้าย
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
      <div className="flex flex-wrap gap-2" role="group" aria-label="ส่วนของพื้นที่">
        <button type="button" data-tap="" className={toggleClass} aria-pressed={tab === 'points'} onClick={() => setTab('points')}>
          {`จุดทำความสะอาด (${detail.points.length})`}
        </button>
        <button type="button" data-tap="" className={toggleClass} aria-pressed={tab === 'check-in'} onClick={() => setTab('check-in')}>
          ป้ายลงเวลา
        </button>
      </div>
      {tab === 'points' ? (
        <PointTable detail={detail} onEdit={open} />
      ) : (
        <CheckInSign detail={detail} />
      )}
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
      {drawerOpen && (
        <Drawer key={drawerId} labelledBy={drawerId} onClose={closeEditor}>
          <PointEditor area={area} point={drawerPoint} onSaved={saved} onClose={closeEditor} />
        </Drawer>
      )}
      {areaOpen && <AreaEditorDrawer detail={detail} onClose={() => setAreaOpen(false)} onSaved={saved} />}
    </section>
  );
}

/** D4's Tab 2 (owner, 2026-10-23): the Area's one check-in sign — its place, radius and QR — beside the point list. */
function CheckInSign({ detail }: { detail: AreaDetail }) {
  const { area, checkInSign } = detail;
  return (
    <section className={`flex flex-col gap-3 ${boxClass}`} aria-labelledby="checkin-panel-title">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h3 id="checkin-panel-title" className="text-base font-bold">{`ป้ายลงเวลา · ${area.code} ${area.name}`}</h3>
        {area.isActive && (
          <Link className={buttonClass('secondary', 'desk')} data-tap="" to={`/admin/print?area=${area.id}&sign=${checkInSign.id}`}>
            พิมพ์ป้ายลงเวลา
          </Link>
        )}
      </div>
      <p className="text-muted">{`${checkInSign.code} · เข้างาน ออกพัก กลับจากพัก เลิกงาน · ${signPlaceText(checkInSign)}`}</p>
      <SignPanel key={checkInSign.id} areaId={area.id} sign={checkInSign} label="ป้ายลงเวลา" canIssueQr={area.isActive} />
    </section>
  );
}
