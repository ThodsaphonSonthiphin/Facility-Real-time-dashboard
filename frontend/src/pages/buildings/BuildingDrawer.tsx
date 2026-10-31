import { useBuildingDrawer } from './useBuildingDrawer';
import { ConfirmationDialog } from '../../shared/components/ConfirmationDialog';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { DialogHeader, Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';

interface BuildingDrawerProps {
  /** Missing: a new building. */
  building?: ManagedBuilding;
  onClose: () => void;
  /** A save or a switch closes the Drawer and floats its answer. */
  onDone: (message: string) => void;
}

/**
 * facility-0090 decisions 1–4 in the right-hand Drawer, as D4's Area: the code (only when new), the name, and for a
 * building that exists, turning it off or on. Off is offered only when nothing active is left in it; the server checks too.
 */
export function BuildingDrawer({ building, onClose, onDone }: BuildingDrawerProps) {
  const { dismissal, code, setCode, name, setName, problem, switchProblem, sending, left, titleId, submit, confirmation, requestToggle } = useBuildingDrawer(building, onDone, onClose);

  return (
    <Drawer labelledBy={titleId} onClose={dismissal.close}>
      <div className="flex flex-col gap-5">
        <DialogHeader
          id={titleId}
          title={building ? `แก้ไขตึก · ${building.code} ${building.name}` : 'เพิ่มตึก'}
          detail="ตึกเก็บพื้นที่และบัญชีหัวหน้า · ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน"
          onClose={dismissal.close}
          closeDisabled={sending}
        />
        <form className="flex flex-col gap-4" onSubmit={(event) => void submit(event)}>
          {problem && <Note tone="error">{problem}</Note>}
          {building ? (
            <div className="flex flex-col gap-1">
              <span className="font-semibold">รหัสตึก</span>
              <span className="font-mono">{building.code}</span>
              <span className="text-xs text-text-2">แก้ไม่ได้</span>
            </div>
          ) : (
            <Field label="รหัสตึก" help="ตัวอักษรอังกฤษหรือตัวเลข 1–20 ตัว เช่น B2 · แก้ทีหลังไม่ได้">
              <TextInput
                size="desk"
                value={code}
                maxLength={20}
                disabled={sending}
                onChange={(event) => setCode(event.target.value)}
              />
            </Field>
          )}
          <Field label="ชื่อตึก">
            <TextInput
              size="desk"
              value={name}
              maxLength={100}
              disabled={sending}
              onChange={(event) => setName(event.target.value)}
            />
          </Field>
          <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
            <Button size="desk" disabled={sending} onClick={dismissal.close}>
              ยกเลิก
            </Button>
            <Button type="submit" variant="primary" size="desk" disabled={sending}>
              บันทึก
            </Button>
          </div>
        </form>
        {building && (
          <section
            className="flex flex-col gap-2 rounded-md border border-border p-3"
            aria-labelledby="building-active-title"
          >
            <h3 id="building-active-title" className="text-base font-bold">
              {building.isActive ? 'ปิดใช้งานตึก' : 'เปิดใช้งานตึก'}
            </h3>
            {!building.isActive && <Note tone="warn">ตึกนี้ปิดใช้งานอยู่ เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้</Note>}
            {building.isActive && left && (
              <Note tone="warn">{`${left} ปิดพื้นที่ และย้ายหรือปิดบัญชีหัวหน้าก่อน จึงจะปิดตึกได้`}</Note>
            )}
            {building.isActive && !left && (
              <p className="text-text-2">
                ตึกหายจากฟอร์มและหน้าข้อมูลสด ยังเลือกได้ในสรุปการตรวจและส่งออก Excel ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ
              </p>
            )}
            {switchProblem && <Note tone="error">{switchProblem}</Note>}
            <div>
              {/* turning off is undone by the same button, so it is not `danger` (MASTER.md): the words carry it */}
              <Button size="desk" disabled={sending || left !== null} onClick={requestToggle}>
                {building.isActive ? 'ปิดใช้งานตึกนี้' : 'เปิดใช้งานตึกนี้'}
              </Button>
            </div>
          </section>
        )}
        {building && confirmation.question && <ConfirmationDialog
          kind="building"
          title={`ปิดใช้งานตึก ${building.code} ${building.name}?`}
          body="ตึกหายจากฟอร์มและหน้าข้อมูลสด ยังเลือกได้ในสรุปการตรวจและส่งออก Excel"
          impacts={['ประวัติยังอยู่ เปิดกลับได้', 'เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้จนกว่าจะเปิดใช้งานตึก']}
          code={building.code}
          confirmLabel="ปิดใช้งานตึก"
          onCancel={confirmation.cancel}
          onConfirm={confirmation.confirm}
        />}
        <MoreInfo summary="เกี่ยวกับการบันทึก">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</MoreInfo>
      </div>
    </Drawer>
  );
}
