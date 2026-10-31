import { ConfirmationDialog } from '../../shared/components/ConfirmationDialog';
import { useAreaActiveSection } from './useAreaActiveSection';
import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
import { Toast } from '../../shared/components/Toast';
import { cleanersLeft } from './areaText';

/** Wireframe D10's lower half: the Area's signs, and deactivation (no delete; history stays). */
export function AreaActiveSection({ detail }: { detail: AreaDetail }) {
  const { area } = detail;
  const { isLoading, notice, setNotice, confirmation, requestToggle } = useAreaActiveSection(detail);
  const activePoints = detail.points.filter((point) => point.isActive).length;
  const left = area.isActive ? cleanersLeft(area) : null;

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
          <Button size="desk" disabled={isLoading || left !== null} onClick={requestToggle}>
            {area.isActive ? 'ปิดใช้งานพื้นที่นี้' : 'เปิดใช้งานพื้นที่นี้'}
          </Button>
        </div>
      </section>
      {confirmation.question && <ConfirmationDialog
          kind="area"
          title={`ปิดใช้งานพื้นที่ ${area.code} ${area.name}?`}
          body="แม่บ้านและหัวหน้าจะสแกนป้ายทั้งหมดของพื้นที่นี้ไม่ได้"
          impacts={[`${activePoints} จุด + ป้ายลงเวลา 1 ป้าย หยุดใช้`, 'หายจากหน้าสถานะจุด · ประวัติยังอยู่ เปิดกลับได้']}
          code={area.code}
          confirmLabel="ปิดใช้งานพื้นที่"
          onCancel={confirmation.cancel}
          onConfirm={confirmation.confirm}
        />}
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
    </>
  );
}
