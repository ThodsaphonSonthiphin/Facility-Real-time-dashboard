import { useState } from 'react';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { cleanersLeft } from './areaText';
import { useSetAreaActiveMutation } from './areaFormApi';

/** Wireframe D10's lower half: the Area's signs, and deactivation (no delete; history stays). */
export function AreaActiveSection({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [setAreaActive, { isLoading }] = useSetAreaActiveMutation();
  const [notice, setNotice] = useState<Notice | null>(null);
  const activePoints = detail.points.filter((point) => point.isActive).length;
  const left = area.isActive ? cleanersLeft(area) : null;

  const toggle = async () => {
    try {
      await setAreaActive({ areaId: area.id, active: !area.isActive }).unwrap();
      setNotice({ kind: 'ok', text: `${area.isActive ? 'ปิด' : 'เปิด'}ใช้งาน ${area.code} แล้ว` });
    } catch (error) {
      setNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <>
      <section className={`flex flex-col gap-2 ${boxClass}`} aria-labelledby="areaform-signs">
        <h2 id="areaform-signs" className="text-base font-bold">
          ป้ายของพื้นที่นี้
        </h2>
        <p>{`ป้ายลงเวลา ${detail.checkInSign.code} ระบบสร้างให้เองตอนเพิ่มพื้นที่`}</p>
        <p>{`จุดทำความสะอาด ${activePoints} จุด · เพิ่มหรือแก้ที่หน้า พื้นที่และป้าย`}</p>
      </section>
      <section className={`flex flex-col gap-2 ${boxClass}`} aria-labelledby="areaform-active">
        <h2 id="areaform-active" className="text-base font-bold">
          {area.isActive ? 'ปิดใช้งานพื้นที่' : 'เปิดใช้งานพื้นที่'}
        </h2>
        {!area.isActive && <Note tone="warn">พื้นที่นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้</Note>}
        {left && (
          <Note tone="warn">{`${left} เอาแม่บ้านออกจากกะในฟอร์มด้านบน หรือย้ายไปพื้นที่อื่นที่หน้าบัญชีผู้ใช้ก่อน จึงจะปิดพื้นที่ได้`}</Note>
        )}
        {area.isActive && !left && (
          <p>พื้นที่และทุกป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
        )}
        <div>
          {/* deactivation is undone by the same button, so it is not `danger` (MASTER.md): the words carry it */}
          <Button size="desk" disabled={isLoading || left !== null} onClick={() => void toggle()}>
            {area.isActive ? 'ปิดใช้งานพื้นที่นี้' : 'เปิดใช้งานพื้นที่นี้'}
          </Button>
        </div>
      </section>
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
    </>
  );
}
