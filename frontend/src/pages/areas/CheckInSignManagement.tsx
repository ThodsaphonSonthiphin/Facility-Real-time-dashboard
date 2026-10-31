import { Link } from 'react-router';
import { MoreInfo } from '../../shared/components/MoreInfo';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { buttonClass, linkClass } from '../../shared/components/styles';
import { signPlaceText } from './areaText';
import { SignPanel } from './SignPanel';

/** The Area editing Drawer keeps every check-in sign action together. */
export function CheckInSignManagement({ detail }: { detail: AreaDetail }) {
  const { area, checkInSign } = detail;
  return (
    <section
      className="flex flex-col gap-4 border-t border-border pt-5"
      aria-labelledby="checkin-panel-title"
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h3 id="checkin-panel-title" className="text-base font-bold">{`ป้ายลงเวลา · ${area.code} ${area.name}`}</h3>
      </div>
      <SignPanel key={checkInSign.id} areaId={area.id} sign={checkInSign} label="ป้ายลงเวลา" canIssueQr={area.isActive} />
      <MoreInfo summary="พิมพ์และรายละเอียดป้ายลงเวลา">
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
      <p className="text-text-2">{`${checkInSign.code} · เข้างาน ออกพัก กลับจากพัก เลิกงาน · ${signPlaceText(checkInSign)}`}</p>
      </MoreInfo>
    </section>
  );
}
