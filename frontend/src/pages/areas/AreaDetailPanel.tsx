import { useState, type KeyboardEvent } from 'react';
import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { boxClass, buttonClass, linkClass, tabClass, tabListClass } from '../../shared/components/styles';
import { Toast, type Notice } from '../../shared/components/Toast';
import { useBuildingNames, buildingText } from '../../shared/api/buildingNames';
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
  const names = useBuildingNames();
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

  // The count lives in the tab, so the list needs no heading of its own (owner, 2026-10-23)
  const tabs: { id: Tab; label: string }[] = [
    { id: 'points', label: `จุดทำความสะอาด (${detail.points.length})` },
    { id: 'check-in', label: 'ป้ายลงเวลา' },
  ];
  // A tablist's keyboard: arrows move between the tabs, Home/End to the ends (the tab that is on is the only stop)
  const onTabKey = (event: KeyboardEvent<HTMLDivElement>) => {
    const at = tabs.findIndex((one) => one.id === tab);
    const to =
      event.key === 'ArrowRight'
        ? (at + 1) % tabs.length
        : event.key === 'ArrowLeft'
          ? (at - 1 + tabs.length) % tabs.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? tabs.length - 1
              : -1;
    if (to === -1) return;
    event.preventDefault();
    setTab(tabs[to].id);
    document.getElementById(`area-tab-${tabs[to].id}`)?.focus();
  };

  return (
    <section className="flex flex-col gap-3" aria-labelledby="areas-detail-title">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <span className="text-muted">{`พื้นที่ที่เลือก · ตึก ${buildingText(area.buildingCode, names)}`}</span>
          <h2 id="areas-detail-title" className="text-xl font-bold">{`${area.code} ${area.name}`}</h2>
          <p className="text-muted">{areaLine(detail)}</p>
        </div>
        {/* The page's actions sit away from the name, on the right: they are not part of it (owner, 2026-10-23) */}
        <div className="flex flex-wrap items-center gap-2">
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
        </div>
      </header>
      {!area.isActive && (
        <Note tone="warn">พื้นที่นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไขพื้นที่"</Note>
      )}
      <div role="tablist" aria-label="ส่วนของพื้นที่" className={tabListClass} onKeyDown={onTabKey}>
        {tabs.map((one) => (
          <button
            key={one.id}
            id={`area-tab-${one.id}`}
            type="button"
            role="tab"
            data-tap=""
            aria-selected={tab === one.id}
            aria-controls={tab === one.id ? `area-panel-${one.id}` : undefined}
            tabIndex={tab === one.id ? 0 : -1}
            className={tabClass}
            onClick={() => setTab(one.id)}
          >
            {one.label}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`area-panel-${tab}`} aria-labelledby={`area-tab-${tab}`} className="flex flex-col gap-3">
        {tab === 'points' ? <PointTable detail={detail} onEdit={open} /> : <CheckInSign detail={detail} />}
      </div>
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
          <div className="flex flex-wrap items-center gap-3">
            <Link className={linkClass} to={`/admin/areas/${area.id}/capture`}>
              เก็บพิกัดบนมือถือ ›
            </Link>
            <Link className={buttonClass('secondary', 'desk')} data-tap="" to={`/admin/print?area=${area.id}&sign=${checkInSign.id}`}>
              พิมพ์ป้ายลงเวลา
            </Link>
          </div>
        )}
      </div>
      <p className="text-muted">{`${checkInSign.code} · เข้างาน ออกพัก กลับจากพัก เลิกงาน · ${signPlaceText(checkInSign)}`}</p>
      <SignPanel key={checkInSign.id} areaId={area.id} sign={checkInSign} label="ป้ายลงเวลา" canIssueQr={area.isActive} />
    </section>
  );
}
