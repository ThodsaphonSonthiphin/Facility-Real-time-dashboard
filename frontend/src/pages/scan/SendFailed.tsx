import type { PointCard } from '../../shared/api/types';
import { Panel } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { noteClass } from '../../shared/components/styles';
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
    <main className="flex flex-col gap-4 text-base text-text">
      <PointHeading point={point} />
      {/* A region with its own heading (wireframe M12), in the error note's colours */}
      <section aria-labelledby="scan-failed-title" className={`flex flex-col gap-0.5 ${noteClass('error')}`}>
        <h2 id="scan-failed-title" className="text-lg font-bold">
          ยังไม่ได้บันทึก
        </h2>
        <span>{`ส่งงานไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
      </section>
      <Panel title="สิ่งที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)">
        <p>{describeDraft(draft)}</p>
      </Panel>
      <Panel title="ทำอย่างไรต่อ">
        <ol className="m-0 list-decimal pl-5">
          <li>อย่าปิดหน้านี้</li>
          <li>เดินไปตรงที่มีสัญญาณ</li>
          <li>กด "ส่งใหม่"</li>
        </ol>
        <p className="text-md text-muted">
          เวลาที่บันทึกคือเวลาที่ส่งสำเร็จ ถ้าพ้นเวลารอบจะนับเป็นส่งช้า ถ้าเดินออกไกลจากป้าย การส่งงานจะขึ้นว่าไกลป้าย
        </p>
        <p className="text-md text-muted">ระบบไม่เก็บงานไว้ในมือถือแล้วส่งเองทีหลัง</p>
      </Panel>
      <Button variant="primary" size="main" disabled={sending} onClick={onResend}>
        {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
      </Button>
    </main>
  );
}
