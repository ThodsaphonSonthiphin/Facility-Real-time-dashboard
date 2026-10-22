import { Link } from 'react-router';
import type { PointCard } from '../../shared/api/types';
import { Note } from '../../shared/components/Note';
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
    <main className="flex flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-1">
        <span className="text-md tracking-wide text-muted">{`ป้ายจุดทำความสะอาด · ${point.name} · ${point.areaName}`}</span>
        <h1 className="text-2xl font-bold">{text[reason].title}</h1>
      </header>
      <Note tone="error" alert={false} title="ไม่ได้บันทึกงาน">
        <span>{text[reason].detail}</span>
      </Note>
      {reason === 'not-checked-in' && (
        <>
          <p>เดินไปที่ป้ายลงเวลา สแกนด้วยกล้องมือถือ กด "เข้างาน" แล้วกลับมาสแกนจุดนี้ใหม่</p>
          <p className="text-md text-muted">
            ถ้าลงเวลาไม่ได้ (มือถือแบตหมด ไม่มีเน็ต) แจ้งแอดมินให้เพิ่มเวลาเข้างานให้ แอดมินต้องใส่เหตุผลทุกครั้ง
          </p>
        </>
      )}
      <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}
