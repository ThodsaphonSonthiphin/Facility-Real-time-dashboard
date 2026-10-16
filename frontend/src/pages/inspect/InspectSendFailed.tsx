import { formatTime } from '../../shared/utils/thaiTime';
import { InspectHeading } from './InspectHeading';
import type { InspectionVisit } from './inspectApi';
import { describeDraft, type InspectDraft } from './inspectRules';

interface InspectSendFailedProps {
  visit: InspectionVisit;
  draft: InspectDraft;
  triedAt: Date;
  sending: boolean;
  onResend: () => void;
}

/**
 * facility-0052's rule for the Supervisor too (wireframe M12's layout): nothing waits on the phone to send later. The
 * verdict stays on this page; the person walks to where there is signal and presses ส่งใหม่.
 */
export function InspectSendFailed({ visit, draft, triedAt, sending, onResend }: InspectSendFailedProps) {
  return (
    <main className="inspect-page">
      <InspectHeading visit={visit} />
      <section className="app-note app-note--error" aria-labelledby="inspect-failed-title">
        <h2 id="inspect-failed-title">ยังไม่ได้บันทึกผลตรวจ</h2>
        <span>{`บันทึกไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
      </section>
      <section className="app-panel">
        <h2>ผลที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)</h2>
        <p>{describeDraft(draft)}</p>
      </section>
      <section className="app-panel">
        <h2>ทำอย่างไรต่อ</h2>
        <ol>
          <li>อย่าปิดหน้านี้</li>
          <li>เดินไปตรงที่มีสัญญาณ</li>
          <li>กด "ส่งใหม่"</li>
        </ol>
        <p className="inspect-muted">ระบบไม่เก็บผลตรวจไว้ในมือถือแล้วส่งเองทีหลัง</p>
      </section>
      <button type="button" className="inspect-submit" disabled={sending} onClick={onResend}>
        {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
      </button>
    </main>
  );
}
