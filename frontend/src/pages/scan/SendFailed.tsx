import type { PointCard } from '../../shared/api/types';
import { formatTime } from '../../shared/utils/thaiTime';
import { PointHeading } from './PointHeading';
import { describeDraft, type ScanDraft } from './scanRules';

interface SendFailedProps {
  point: PointCard;
  draft: ScanDraft;
  triedAt: Date;
  sending: boolean;
  onResend: () => void;
}

/**
 * Wireframe M12 (facility-0052): nothing is kept on the phone to send later. The choices stay on this page; the person
 * walks to where there is signal and presses ส่งใหม่. The time recorded is when a send succeeds.
 */
export function SendFailed({ point, draft, triedAt, sending, onResend }: SendFailedProps) {
  return (
    <main className="scan-page">
      <PointHeading point={point} />
      <section className="app-note app-note--error" aria-labelledby="scan-failed-title">
        <h2 id="scan-failed-title">ยังไม่ได้บันทึก</h2>
        <span>{`ส่งงานไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
      </section>
      <section className="app-panel">
        <h2>สิ่งที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)</h2>
        <p>{describeDraft(draft)}</p>
      </section>
      <section className="app-panel">
        <h2>ทำอย่างไรต่อ</h2>
        <ol>
          <li>อย่าปิดหน้านี้</li>
          <li>เดินไปตรงที่มีสัญญาณ</li>
          <li>กด "ส่งใหม่"</li>
        </ol>
        <p className="scan-muted">เวลาที่บันทึกคือเวลาที่ส่งสำเร็จ ถ้าพ้นเวลารอบจะนับเป็นส่งช้า ถ้าเดินออกไกลจากป้าย การส่งงานอาจติดธง</p>
        <p className="scan-muted">ระบบไม่เก็บงานไว้ในมือถือแล้วส่งเองทีหลัง</p>
      </section>
      <button type="button" className="scan-submit" disabled={sending} onClick={onResend}>
        {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
      </button>
    </main>
  );
}
