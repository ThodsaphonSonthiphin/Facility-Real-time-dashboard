import type { LocationProblem } from '../geo/readLocation';
import './shared.css';

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
      <main className="app-help">
        <h1 className="app-help__title">อ่านตำแหน่งไม่ได้</h1>
        <p className="app-note app-note--error">
          {`หน้านี้ต้องเปิดผ่าน https มือถือจึงยอมให้อ่านตำแหน่ง ยังไม่ได้${action} แจ้งแอดมิน`}
        </p>
      </main>
    );
  }
  return (
    <main className="app-help">
      {problem === 'denied' ? (
        <>
          <h1 className="app-help__title">{`เปิดตำแหน่งก่อน${action}`}</h1>
          <p>ระบบต้องรู้ตำแหน่งตอนสแกน ถ้าปิดไว้จะยังไม่บันทึก</p>
          <section className="app-panel">
            <h2>Android</h2>
            <ol>
              <li>ปัดแถบด้านบนของจอลงมา</li>
              <li>แตะปุ่ม "ตำแหน่ง" ให้เปิด</li>
              <li>ถ้าเบราว์เซอร์ถาม ให้กด "อนุญาต"</li>
            </ol>
          </section>
          <section className="app-panel">
            <h2>iPhone</h2>
            <ol>
              <li>เปิด การตั้งค่า › ความเป็นส่วนตัว › บริการหาตำแหน่ง</li>
              <li>เปิดบริการหาตำแหน่ง แล้วเลือก Safari ให้ "ขณะใช้งาน"</li>
              <li>กลับมาหน้านี้</li>
            </ol>
          </section>
        </>
      ) : (
        <>
          <h1 className="app-help__title">อ่านตำแหน่งไม่ได้</h1>
          <p>{`มือถือยังหาตำแหน่งไม่เจอ ยังไม่ได้${action} ลองเดินไปใกล้หน้าต่างหรือที่โล่ง แล้วกดลองอีกครั้ง`}</p>
        </>
      )}
      <button type="button" className="app-button app-button--primary" disabled={busy} onClick={onRetry}>
        {problem === 'denied' ? 'เปิดแล้ว ลองอีกครั้ง' : 'ลองอีกครั้ง'}
      </button>
    </main>
  );
}
