import { buildingOptionText } from '../../shared/api/buildingNames';
import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { DialogFooter, DialogHeader } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import type { EditAccountFormProps } from './accountFormTypes';
import {
  accountRoleLabel,
  areaOptionLabel
} from './accountRules';
import { formClass } from './accountStyles';
import { ShiftChoice } from './ShiftChoice';
import { useEditAccountForm } from './useEditAccountForm';



/** D6's "แก้ไข": name and place; the role and the login name are fixed (plan 5 decision 2). Keyed by account id. */
export function EditAccountForm({ account, accounts, areas, buildings, onDone, onCancel }: EditAccountFormProps) {
  const { names, draft, set, problem, isLoading, areaChoices, blockOf, titleId, chooseArea, chooseBuilding, submit } = useEditAccountForm({ account, accounts, areas, buildings, onDone, onCancel });

  return (
    <form className={formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <DialogHeader id={titleId} title={`แก้ไขบัญชี · ${account.displayName}`} onClose={onCancel} closeDisabled={isLoading} />
      <div className="min-h-0 flex-1 overflow-y-auto pb-5 flex flex-col gap-5">
        <p className="text-text-2">{`${accountRoleLabel[account.role]} · ${account.loginName} (บทบาทและรหัสแก้ไม่ได้)`}</p>
        <Field label="ชื่อที่แสดง">
          <TextInput
            size="desk"
            maxLength={150}
            value={draft.displayName}
            disabled={isLoading}
            onChange={(event) => set({ displayName: event.target.value })}
          />
        </Field>
        {account.role !== 'Admin' && <p className="rounded-md bg-bg p-3 text-text-3">เบอร์โทรเปิดดูไม่ได้ ใช้ปุ่มแก้เบอร์โทรเพื่อเปลี่ยนเบอร์</p>}
        {/* Reference assignment is 1fr + 160px at 420px and above; no container token fits these widths. */}
        <fieldset hidden={account.role === 'Admin'} className="min-w-0 rounded-lg border border-border p-4">
          <legend className="px-1.5 font-semibold">{account.role === 'Cleaner' ? 'ผูกกับพื้นที่' : 'ผูกกับตึกและกะ'}</legend>
          <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-[minmax(0,1fr)_10rem]">
            {account.role === 'Cleaner' && (
              <Field label="พื้นที่ประจำ" help={account.isActive ? undefined : 'เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่'}>
                <Select
                  size="desk"
                  value={draft.areaId}
                  disabled={isLoading || !account.isActive}
                  onChange={(event) => chooseArea(event.target.value)}
                >
                  <option value="">— ยังไม่กำหนดพื้นที่ —</option>
                  {areaChoices.map((option) => (
                    <option key={option.id} value={String(option.id)}>
                      {areaOptionLabel(option, account.id, names)}
                    </option>
                  ))}
                </Select>
              </Field>
            )}
            {account.role === 'Supervisor' && (
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
            {account.role !== 'Admin' && (
              <ShiftChoice
                name={`${titleId}-shift`}
                value={draft.shift}
                blockOf={blockOf}
                disabled={isLoading}
                onChange={(shift) => set({ shift })}
              />
            )}
          </div>
        </fieldset>
        {problem && <Note tone="error">{problem}</Note>}
      </div>
      <DialogFooter>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
        <Button type="submit" variant="primary" size="desk" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึก'}
        </Button>
      </DialogFooter>
    </form>
  );
}
