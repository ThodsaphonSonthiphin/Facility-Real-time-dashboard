import type { LocationProblem } from '../geo/readLocation';
import { Panel } from './Boxes';
import { Button } from './Button';
import { Note } from './Note';
import { helpScreenClass } from './styles';

interface LocationHelpProps {
  problem: LocationProblem;
  /** What waits for the location: "ลงเวลา" or "ส่งงาน". */
  action: string;
  busy: boolean;
  onRetry: () => void;
}

/**
 * Wireframe M2 (facility-0036): without a location nothing is recorded, so the page says how to turn it on and
 * repeats the same press. On plain http no browser gives a location, so there is nothing to retry.
 */
export function LocationHelp({ problem, action, busy, onRetry }: LocationHelpProps) {
  if (problem === 'insecure') {
    return (
      <main className={helpScreenClass.main}>
        <h1 className={helpScreenClass.title}>อ่านตำแหน่งไม่ได้</h1>
        <Note tone="error" alert={false}>
          {`หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้${action} แจ้งแอดมิน`}
        </Note>
      </main>
    );
  }
  return (
    <main className={helpScreenClass.main}>
      {problem === 'denied' ? (
        <>
          <h1 className={helpScreenClass.title}>{`เปิดตำแหน่งก่อน${action}`}</h1>
          <p>ระบบต้องรู้ตำแหน่งตอนสแกน ถ้าปิดไว้จะยังไม่บันทึก</p>
          <Panel title="Android">
            <ol className="m-0 list-decimal pl-5">
              <li>ปัดแถบด้านบนของจอลงมา</li>
              <li>แตะปุ่ม "ตำแหน่ง" ให้เปิด</li>
              <li>ถ้าเบราว์เซอร์ถาม ให้กด "อนุญาต"</li>
            </ol>
          </Panel>
          <Panel title="iPhone">
            <ol className="m-0 list-decimal pl-5">
              <li>เปิด การตั้งค่า › ความเป็นส่วนตัว › บริการหาตำแหน่ง</li>
              <li>เปิดบริการหาตำแหน่ง แล้วเลือก Safari ให้ "ขณะใช้งาน"</li>
              <li>กลับมาหน้านี้</li>
            </ol>
          </Panel>
        </>
      ) : (
        <>
          <h1 className={helpScreenClass.title}>อ่านตำแหน่งไม่ได้</h1>
          <p>{`มือถือยังหาตำแหน่งไม่เจอ ยังไม่ได้${action} ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วกดลองอีกครั้ง`}</p>
        </>
      )}
      <Button variant="primary" size="main" disabled={busy} onClick={onRetry}>
        {problem === 'denied' ? 'เปิดแล้ว ลองอีกครั้ง' : 'ลองอีกครั้ง'}
      </Button>
    </main>
  );
}
