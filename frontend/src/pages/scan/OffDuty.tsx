import { Clock, List } from 'lucide-react';
import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import { Note } from '../../shared/components/Note';
import { ResultScreen } from '../../shared/components/ResultScreen';
import { buttonClass } from '../../shared/components/styles';
import type { OffDutyReason } from './scanRules';

const text: Record<OffDutyReason, { title: string; detail: string }> = {
  'not-checked-in': { title: 'ยังไม่ได้ลงเวลาเข้างาน', detail: 'ต้องสแกนป้ายลงเวลาของพื้นที่ก่อน แล้วค่อยสแกนจุดนี้' },
  'checked-out': { title: 'ลงเวลาเลิกงานแล้ว', detail: 'ส่งงานได้ตั้งแต่ลงเวลาเข้างานจนถึงลงเวลาเลิกงาน' },
  'outside-shift': { title: 'ตอนนี้ไม่อยู่ในกะของคุณ', detail: 'ส่งงานได้เฉพาะในกะของตัวเอง หลังลงเวลาเข้างาน' },
};

/** Wireframe M11: nothing is sent, because the server would refuse it (409). No link to the check-in sign (facility-0059). */
export function OffDuty({ reason, point }: { reason: OffDutyReason; point: PointCard }) {
  return (
    <ResultScreen
      tone="warn"
      icon={<Clock aria-hidden="true" className="size-14" strokeWidth={2.25} />}
      title={text[reason].title}
      bottom={
        <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-work">
          <List aria-hidden="true" className="size-6" />
          ดูงานของฉัน
        </Link>
      }
    >
      <span className="text-lg text-text-3">{`ป้ายจุดทำความสะอาด · ${point.name} · ${point.areaName}`}</span>
      <div className="flex w-full flex-col gap-4 text-left">
        <Note tone="error" alert={false} title="ไม่ได้บันทึกงาน">
          <span>{text[reason].detail}</span>
        </Note>
        {reason === 'not-checked-in' && (
          <>
            <p>เดินไปที่ป้ายลงเวลา สแกนด้วยกล้องมือถือ กด "เข้างาน" แล้วกลับมาสแกนจุดนี้ใหม่</p>
            <p className="text-sm text-text-2">
              ถ้าลงเวลาไม่ได้ (มือถือแบตหมด ไม่มีเน็ต) แจ้งแอดมินให้เพิ่มเวลาเข้างานให้ แอดมินต้องใส่เหตุผลทุกครั้ง
            </p>
          </>
        )}
      </div>
    </ResultScreen>
  );
}
