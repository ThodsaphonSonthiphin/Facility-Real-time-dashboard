import { Pencil, Printer } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { ActiveTag } from '../../shared/components/ActiveTag';
import { Button } from '../../shared/components/Button';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { buttonClass } from '../../shared/components/styles';
import { Toast } from '../../shared/components/Toast';
import { AreaEditorDrawer } from './AreaEditorDrawer';
import { areaHeadline } from './areaText';
import { PointEditor } from './PointEditor';
import { PointTable } from './PointTable';
import { SignPlace } from './SignPlace';
import { useAreaDetailPanel } from './useAreaDetailPanel';

/** The selected Area's facts and points share one view; management stays in its Drawers. */
export function AreaDetailPanel({ detail }: { detail: AreaDetail }) {
  const {
    names, area, checkInSign, open, openArea, notice, clearNotice, drawerPoint, drawerId,
    drawerOpen, closeEditor, saved, areaOpen, closeArea,
  } = useAreaDetailPanel(detail);
  return (
    <section className="flex flex-col gap-4" aria-labelledby="areas-detail-title">
      <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface px-6 py-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <div className="flex flex-wrap items-center gap-2">
              <h2 id="areas-detail-title" className="text-xl font-bold">{`${area.code} ${area.name}`}</h2>
              <ActiveTag active={area.isActive} />
            </div>
            <span className="text-text-2">{areaHeadline(detail, names)}</span>
          </div>
          {/* The page's actions sit away from the name, on the right: they are not part of it (owner, 2026-10-23) */}
          <div className="flex flex-wrap items-center gap-2">
            <Button
              size="desk"
              onClick={openArea}
            >
              <Pencil aria-hidden="true" className="size-4" />
              แก้ไขพื้นที่
            </Button>
            {area.isActive && (
              <Link className={buttonClass('secondary', 'desk')} data-tap="" to={`/admin/print?area=${area.id}`}>
                <Printer aria-hidden="true" className="size-4" />
                พิมพ์ป้าย
              </Link>
            )}
          </div>
        </div>
        <dl className="m-0 grid grid-cols-1 sm:grid-cols-3 gap-4 border-t border-border pt-4">
          <Fact label="แม่บ้านกะเช้า" value={area.dayCleaner?.displayName ?? 'ยังไม่มี'} />
          {area.shiftPattern === 'DayAndNight' && (
            <Fact label="แม่บ้านกะดึก" value={area.nightCleaner?.displayName ?? 'ยังไม่มี'} />
          )}
          <Fact label="ป้ายลงเวลา" value={`${checkInSign.code} · รัศมี ${checkInSign.radiusM} ม.`} />
          <Fact label="พิกัดป้ายลงเวลา" value={<SignPlace sign={checkInSign} />} />
        </dl>
      </div>
      {!area.isActive && (
        <Note tone="warn">พื้นที่นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไขพื้นที่"</Note>
      )}
      <PointTable detail={detail} onEdit={open} />
      {notice && <Toast notice={notice} onClose={clearNotice} />}
      {drawerOpen && (
        <Drawer width="xl" key={drawerId} labelledBy={drawerId} onClose={closeEditor}>
          <PointEditor area={area} point={drawerPoint} onSaved={saved} onClose={closeEditor} />
        </Drawer>
      )}
      {areaOpen && <AreaEditorDrawer detail={detail} onClose={closeArea} onSaved={saved} />}
    </section>
  );
}

/** One of the facts under the Area's name (the design file's D4): a grey label over a dark value. */
function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-col">
      <dt className="text-xs text-text-2">{label}</dt>
      <dd className="m-0 font-semibold">{value}</dd>
    </div>
  );
}
