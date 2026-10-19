import { useState } from 'react';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useSetAreaActiveMutation } from './areaFormApi';

/** Wireframe D10's lower half: the Area's signs, and deactivation (no delete; history stays). */
export function AreaActiveSection({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const [setAreaActive, { isLoading }] = useSetAreaActiveMutation();
  const [note, setNote] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);
  const activePoints = detail.points.filter((point) => point.isActive).length;

  const toggle = async () => {
    try {
      await setAreaActive({ areaId: area.id, active: !area.isActive }).unwrap();
      setNote({ kind: 'ok', text: `${area.isActive ? 'ปิด' : 'เปิด'}ใช้งาน ${area.code} แล้ว` });
    } catch (error) {
      setNote({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <>
      <section className="app-panel" aria-labelledby="areaform-signs">
        <h2 id="areaform-signs">ป้ายของ Area นี้</h2>
        <p>{`ป้ายลงเวลา ${detail.checkInSign.code} ระบบสร้างให้เองตอนเพิ่ม Area`}</p>
        <p>{`จุดทำความสะอาด ${activePoints} จุด · เพิ่มหรือแก้ที่หน้า จัดการ Area และป้าย`}</p>
      </section>
      <section className="app-panel" aria-labelledby="areaform-active">
        <h2 id="areaform-active">{area.isActive ? 'ปิดใช้งาน Area' : 'เปิดใช้งาน Area'}</h2>
        {area.isActive ? (
          <p>Area และทุกป้ายหายจาก Dashboard และสแกนไม่ได้ ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
        ) : (
          <p className="app-note app-note--warn">Area นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้</p>
        )}
        <button
          type="button"
          className={area.isActive ? 'app-button app-button--warn' : 'app-button'}
          disabled={isLoading}
          onClick={() => void toggle()}
        >
          {area.isActive ? 'ปิดใช้งาน Area นี้' : 'เปิดใช้งาน Area นี้'}
        </button>
        {note && (
          <p
            className={note.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
            role={note.kind === 'ok' ? 'status' : 'alert'}
          >
            {note.text}
          </p>
        )}
      </section>
    </>
  );
}
