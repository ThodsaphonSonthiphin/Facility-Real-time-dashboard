import { Link } from 'react-router';
import type { BlockedScan } from '../api/types';
import './shared.css';

interface BlockedScanScreenProps {
  blocked: BlockedScan;
  /** A cover never lets a Cleaner check in at another Area's sign (facility-0041 item 4), so the advice differs. */
  sign: 'check-in' | 'point';
  ownAreaName?: string;
}

/** Wireframe M8 (facility-0041): nothing was recorded; the Admin already sees this Blocked Scan and can assign a cover. */
export function BlockedScanScreen({ blocked, sign, ownAreaName }: BlockedScanScreenProps) {
  return (
    <main className="app-help">
      <span className="app-help__kicker">{sign === 'check-in' ? 'ป้ายลงเวลา' : 'ป้ายจุดทำความสะอาด'}</span>
      <h1 className="app-help__title">{blocked.message}</h1>
      <p className="app-note app-note--error">{sign === 'check-in' ? 'ไม่ได้บันทึกเวลา' : 'ไม่ได้บันทึกงาน'}</p>
      <dl className="app-facts">
        <dt>ป้ายที่สแกน</dt>
        <dd>{`${blocked.areaName} (${blocked.areaCode}) · ตึก ${blocked.buildingCode}`}</dd>
        {ownAreaName && (
          <>
            <dt>Area ของคุณ</dt>
            <dd>{ownAreaName}</dd>
          </>
        )}
      </dl>
      {sign === 'point' ? (
        <section className="app-panel">
          <h2>ถ้าได้รับมอบหมายให้ทำแทน</h2>
          <p>
            {`Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน ${blocked.areaName} เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </section>
      ) : (
        <p>ลงเวลาที่ป้ายของ Area ตัวเองเท่านั้น วันที่ทำแทน Area อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ Admin เห็นการสแกนครั้งนี้แล้ว</p>
      )}
      <Link className="app-button app-button--primary" to="/my-work">
        กลับไปงานของฉัน
      </Link>
    </main>
  );
}
