import { buildingOptionText } from '../../shared/api/buildingNames';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { DialogFooter, DialogHeader } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import type { CreateAccountFormProps } from './accountFormTypes';
import {
  accountRoleLabel,
  areaOptionLabel,
} from './accountRules';
import { formClass } from './accountStyles';
import { ShiftChoice } from './ShiftChoice';
import { useCreateAccountForm } from './useCreateAccountForm';



/**
 * Wireframe D6's "สร้างบัญชี" (facility-0020, 0054, 0056): the fields of the chosen role only. A phone or password lives
 * in this form's state until it is sent, then it is cleared.
 */
export function CreateAccountForm({ accounts, areas, buildings, onCancel }: CreateAccountFormProps) {
  const { names, draft, chooseRole, set, problem, notice, isLoading, activeAreas, blockOf, chooseArea, chooseBuilding, submit } = useCreateAccountForm({ accounts, areas, buildings, onCancel });

  return (
    <form className={formClass} aria-labelledby="accounts-create-title" onSubmit={(event) => void submit(event)}>
      <DialogHeader id="accounts-create-title" title="สร้างบัญชี" onClose={onCancel} closeDisabled={isLoading} detail="สร้างทีละคน" />
      <div className="-mx-6 min-h-0 flex-1 overflow-y-auto px-6 pb-5 flex flex-col gap-5">
        <fieldset disabled={isLoading} className="m-0 min-w-0 border-0 p-0">
          <legend className="mb-2 font-semibold">บทบาท</legend>
          <div className="grid grid-cols-3 gap-2">
            {(['Cleaner', 'Supervisor', 'Admin'] as const).map((role) => (
              <label key={role} className="flex min-w-0 cursor-pointer flex-col gap-1 rounded-md m-px border border-border-control p-2.5 has-checked:m-0 has-checked:border-2 has-checked:border-accent has-checked:bg-accent-subtle has-focus-visible:outline-3 has-focus-visible:outline-offset-2 has-focus-visible:outline-accent has-disabled:opacity-50">
                <input
                  type="radio"
                  aria-label={accountRoleLabel[role]}
                  className="sr-only"
                  name="accounts-create-role"
                  checked={draft.role === role}
                  onChange={() => chooseRole(role)}
                />
                <span className="font-semibold ">{accountRoleLabel[role]}</span>
                <span className="text-xs text-text-2">{role === 'Cleaner' ? 'ผูกพื้นที่ + กะ' : role === 'Supervisor' ? 'ผูกตึก + กะ' : 'ดูแลทั้งระบบ'}</span>
              </label>
            ))}
          </div>
        </fieldset>
        <Field label="ชื่อที่แสดง">
          <TextInput
            size="desk"
            maxLength={150}
            value={draft.displayName}
            disabled={isLoading}
            onChange={(event) => set({ displayName: event.target.value })}
          />
        </Field>
        {draft.role === 'Admin' ? (
          <>
            <Field label="ชื่อผู้ใช้">
              <TextInput
                size="desk"
                autoComplete="off"
                maxLength={100}
                value={draft.username}
                disabled={isLoading}
                onChange={(event) => set({ username: event.target.value })}
              />
            </Field>
            <Field label="รหัสผ่านชั่วคราว">
              <TextInput
                size="desk"
                type="password"
                autoComplete="new-password"
                maxLength={100}
                value={draft.password}
                disabled={isLoading}
                onChange={(event) => set({ password: event.target.value })}
              />
            </Field>
            <p className="text-text-2">แอดมิน: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว 15–100 ตัวอักษร เป็นวลียาว ๆ ได้ (บอกเองด้วยวาจาหรือกระดาษ)</p>
          </>
        ) : (
          <>
            {/* 420px leaves room for two readable fields inside the 520px reference Drawer. */}
            <div className="grid grid-cols-1 gap-4 min-[420px]:grid-cols-2">
              <Field label="รหัสพนักงาน" help="ห้ามซ้ำ แก้ทีหลังไม่ได้">
                <TextInput
                  size="desk"
                  autoComplete="off"
                  maxLength={20}
                  value={draft.employeeId}
                  disabled={isLoading}
                  onChange={(event) => set({ employeeId: event.target.value })}
                />
              </Field>
              <Field
                label="เบอร์โทรศัพท์"
                help="ใช้แทนรหัสผ่าน · แก้เบอร์ทีหลังได้"
              >
                <TextInput
                  size="desk"
                  type="tel"
                  inputMode="numeric"
                  autoComplete="off"
                  maxLength={16}
                  value={draft.phone}
                  disabled={isLoading}
                  onChange={(event) => set({ phone: event.target.value })}
                />
              </Field>
            </div>
            <fieldset className="min-w-0 rounded-lg border border-border p-4">
              <legend className="px-1.5 font-semibold">{draft.role === 'Cleaner' ? 'ผูกกับพื้นที่' : 'ผูกกับตึกและกะ'}</legend>
              {/* Reference assignment is 1fr + 160px above 420px; no container token fits. */}
              <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-[minmax(0,1fr)_10rem]">
                {draft.role === 'Cleaner' ? (
                  <Field label="พื้นที่ประจำ">
                    <Select
                      size="desk"
                      value={draft.areaId}
                      disabled={isLoading}
                      onChange={(event) => chooseArea(event.target.value)}
                    >
                      <option value="">— ยังไม่กำหนดพื้นที่ —</option>
                      {activeAreas.map((option) => (
                        <option key={option.id} value={String(option.id)}>
                          {areaOptionLabel(option, undefined, names)}
                        </option>
                      ))}
                    </Select>
                  </Field>
                ) : (
                  <Field label="ตึกที่ประจำ">
                    <Select
                      size="desk"
                      value={draft.buildingId}
                      disabled={isLoading}
                      onChange={(event) => chooseBuilding(event.target.value)}
                    >
                      <option value="">เลือกตึก</option>
                      {buildings.map((building) => (
                        <option key={building.id} value={String(building.id)}>
                          {buildingOptionText(building)}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
                <ShiftChoice
                  name="accounts-create-shift"
                  value={draft.shift}
                  blockOf={blockOf}
                  disabled={isLoading}
                  onChange={(shift) => set({ shift })}
                />
              </div>
              <p className="mt-3 text-xs text-text-2">
                {draft.role === 'Cleaner'
                  ? 'พื้นที่ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · พื้นที่ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนดพื้นที่ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'
                  : 'หัวหน้าประจำตึกและกะ ตึกละ 1 คนต่อกะ · ตรวจได้เฉพาะในกะตัวเอง · ไม่ต้องลงเวลา'}
              </p>
            </fieldset>
            <p className="rounded-md bg-bg p-3 text-text-3">เข้าระบบด้วยรหัสพนักงาน + เบอร์โทร ไม่ต้องตั้งรหัสผ่าน</p>
          </>
        )}
        {draft.role !== 'Admin' && <details className="text-xs text-text-2"><summary className="min-h-9 cursor-pointer py-2 font-semibold focus-visible:outline-2 focus-visible:outline-accent">เกี่ยวกับเบอร์โทร</summary><p>ใช้แทนรหัสผ่าน ระบบเก็บเบอร์แบบที่เปิดดูไม่ได้ ใช้ตรวจตอน login อย่างเดียว · เปลี่ยนเบอร์ทีหลังได้ด้วยปุ่ม "แก้เบอร์โทร"</p></details>}
        {problem && <Note tone="error">{problem}</Note>}
        {notice && (
          <Note tone="info" status>
            {notice}
          </Note>
        )}
      </div>
      <DialogFooter>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
        <Button type="submit" variant="primary" size="desk" disabled={isLoading}>
          {isLoading ? 'กำลังสร้าง...' : 'สร้างบัญชี'}
        </Button>
      </DialogFooter>
    </form>
  );
}
