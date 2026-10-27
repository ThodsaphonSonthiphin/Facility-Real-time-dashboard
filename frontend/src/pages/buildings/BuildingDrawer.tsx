import { useState, type FormEvent } from 'react';
import type { ManagedBuilding } from '../../shared/api/adminSetupApi';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { draftProblem, stillInside } from './buildingRules';
import { useCreateBuildingMutation, useRenameBuildingMutation, useSetBuildingActiveMutation } from './buildingsApi';

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
  const [code, setCode] = useState('');
  const [name, setName] = useState(building?.name ?? '');
  const [problem, setProblem] = useState<string | null>(null);
  const [switchProblem, setSwitchProblem] = useState<string | null>(null);
  const [createBuilding, created] = useCreateBuildingMutation();
  const [renameBuilding, renamed] = useRenameBuildingMutation();
  const [setBuildingActive, switching] = useSetBuildingActiveMutation();
  const sending = created.isLoading || renamed.isLoading || switching.isLoading;
  const left = building?.isActive ? stillInside(building) : null;
  const titleId = 'building-editor-title';

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const found = draftProblem(code, name, !building);
    setProblem(found);
    if (found) {
      return;
    }
    try {
      if (building) {
        const saved = await renameBuilding({ buildingId: building.id, name: name.trim() }).unwrap();
        onDone(`บันทึกตึก ${saved.code} ${saved.name} แล้ว`);
      } else {
        const saved = await createBuilding({ code: code.trim(), name: name.trim() }).unwrap();
        onDone(`เพิ่มตึก ${saved.code} ${saved.name} แล้ว`);
      }
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (current: ManagedBuilding) => {
    setSwitchProblem(null);
    try {
      const saved = await setBuildingActive({ buildingId: current.id, active: !current.isActive }).unwrap();
      onDone(`${saved.isActive ? 'เปิด' : 'ปิด'}ใช้งานตึก ${saved.code} แล้ว`);
    } catch (error) {
      setSwitchProblem(getErrorMessage(error));
    }
  };

  return (
    <Drawer labelledBy={titleId} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <h2 id={titleId} tabIndex={-1} className="text-lg font-semibold">
            {building ? `แก้ไขตึก · ${building.code} ${building.name}` : 'เพิ่มตึก'}
          </h2>
          <Button size="desk" onClick={onClose}>
            ปิด
          </Button>
        </div>
        <form className={`flex flex-col gap-3 ${boxClass}`} onSubmit={(event) => void submit(event)}>
          {building ? (
            <p className="text-muted">{`รหัสตึก ${building.code} (แก้ไม่ได้)`}</p>
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
          {problem && <Note tone="error">{problem}</Note>}
          <div className="flex flex-wrap gap-2">
            <Button type="submit" variant="primary" size="desk" disabled={sending}>
              บันทึก
            </Button>
            <Button size="desk" disabled={sending} onClick={onClose}>
              ยกเลิก
            </Button>
          </div>
        </form>
        {building && (
          <section className={`flex flex-col gap-2 ${boxClass}`} aria-labelledby="building-active-title">
            <h3 id="building-active-title" className="text-base font-semibold">
              {building.isActive ? 'ปิดใช้งานตึก' : 'เปิดใช้งานตึก'}
            </h3>
            {!building.isActive && <Note tone="warn">ตึกนี้ปิดใช้งานอยู่ เปิดพื้นที่หรือบัญชีหัวหน้าในตึกนี้ไม่ได้</Note>}
            {building.isActive && left && (
              <Note tone="warn">{`${left} ปิดพื้นที่ และย้ายหรือปิดบัญชีหัวหน้าก่อน จึงจะปิดตึกได้`}</Note>
            )}
            {building.isActive && !left && (
              <p>ตึกหายจากฟอร์มและหน้าข้อมูลสด ยังเลือกได้ในสรุปการตรวจและส่งออก Excel ประวัติยังอยู่ เปิดกลับได้ ไม่มีปุ่มลบ</p>
            )}
            {switchProblem && <Note tone="error">{switchProblem}</Note>}
            <div>
              {/* turning off is undone by the same button, so it is not `danger` (MASTER.md): the words carry it */}
              <Button size="desk" disabled={sending || left !== null} onClick={() => void toggle(building)}>
                {building.isActive ? 'ปิดใช้งานตึกนี้' : 'เปิดใช้งานตึกนี้'}
              </Button>
            </div>
          </section>
        )}
        <MoreInfo summary="เกี่ยวกับการบันทึก">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</MoreInfo>
      </div>
    </Drawer>
  );
}
