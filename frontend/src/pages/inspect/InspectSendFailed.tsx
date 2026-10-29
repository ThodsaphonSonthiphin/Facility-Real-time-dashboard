import { RefreshCw, WifiOff } from 'lucide-react';
import { Link } from 'react-router';
import { Panel } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { ResultScreen } from '../../shared/components/ResultScreen';
import { buttonClass, noteClass } from '../../shared/components/styles';
import { formatTime } from '../../shared/utils/thaiTime';
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
 * verdict stays on this page; the person walks to where there is signal and presses ส่งใหม่. A full red screen, as the
 * Cleaner's.
 */
export function InspectSendFailed({ visit, draft, triedAt, sending, onResend }: InspectSendFailedProps) {
  return (
    <ResultScreen
      tone="err"
      icon={<WifiOff aria-hidden="true" className="size-14" strokeWidth={2.25} />}
      title="บันทึกไม่สำเร็จ"
      bottom={
        <>
          <Button variant="primary" size="main" disabled={sending} onClick={onResend}>
            <RefreshCw aria-hidden="true" className="size-6" />
            {sending ? 'กำลังส่ง...' : 'ส่งใหม่'}
          </Button>
          <Link className={buttonClass('secondary', 'phone')} data-tap="" to="/my-building">
            กลับไปตึกของฉัน
          </Link>
        </>
      }
    >
      <span className="text-xl font-semibold">{visit.point.name}</span>
      <div className="flex w-full flex-col gap-4 text-left">
        {/* A region with its own heading, in the error note's colours, as on the scan page */}
        <section aria-labelledby="inspect-failed-title" className={`flex flex-col gap-0.5 ${noteClass('error')}`}>
          <h2 id="inspect-failed-title" className="text-lg font-bold">
            ยังไม่ได้บันทึกผลตรวจ
          </h2>
          <span>{`บันทึกไม่สำเร็จ เพราะไม่มีสัญญาณหรือเซิร์ฟเวอร์ไม่ตอบ · ลองล่าสุด ${formatTime(triedAt)}`}</span>
        </section>
        <Panel title="ผลที่คุณเลือกไว้ (ยังอยู่ในหน้านี้)">
          <p>{describeDraft(draft)}</p>
        </Panel>
        <Panel title="ทำอย่างไรต่อ">
          <ol className="m-0 list-decimal pl-5">
            <li>อย่าปิดหน้านี้</li>
            <li>เดินไปตรงที่มีสัญญาณ</li>
            <li>กด "ส่งใหม่"</li>
          </ol>
          <p className="text-sm text-text-2">ระบบไม่เก็บผลตรวจไว้ในมือถือแล้วส่งเองทีหลัง</p>
        </Panel>
      </div>
    </ResultScreen>
  );
}
