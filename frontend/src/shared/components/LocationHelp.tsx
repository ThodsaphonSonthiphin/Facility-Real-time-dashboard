import { MapPinOff, RefreshCw } from 'lucide-react';
import type { LocationProblem } from '../geo/readLocation';
import { Panel } from './Boxes';
import { Button } from './Button';
import { Note } from './Note';
import { ResultScreen } from './ResultScreen';

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
  const insecure = problem === 'insecure';
  const denied = problem === 'denied';
  return (
    <ResultScreen
      tone="err"
      icon={<MapPinOff aria-hidden="true" className="size-14" strokeWidth={2.25} />}
      title={denied ? `เปิดตำแหน่งก่อน${action}` : 'อ่านตำแหน่งไม่ได้'}
      // On plain http no browser gives a location, so there is nothing to retry
      bottom={
        insecure ? undefined : (
          <Button variant="primary" size="main" disabled={busy} onClick={onRetry}>
            <RefreshCw aria-hidden="true" className="size-6" />
            {denied ? 'เปิดแล้ว ลองอีกครั้ง' : 'ลองอีกครั้ง'}
          </Button>
        )
      }
    >
      <div className="flex w-full flex-col gap-4 text-left">
        {insecure && (
          <Note tone="error" alert={false}>
            {`หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้${action} แจ้งแอดมิน`}
          </Note>
        )}
        {denied && (
          <>
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
        )}
        {!insecure && !denied && (
          <p>{`มือถือยังหาตำแหน่งไม่เจอ ยังไม่ได้${action} ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วกดลองอีกครั้ง`}</p>
        )}
      </div>
    </ResultScreen>
  );
}
