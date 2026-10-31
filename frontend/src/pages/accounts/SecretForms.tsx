import { Facts } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { DialogFooter, DialogHeader } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import type { SecretFormProps } from './accountFormTypes';
import {
  accountRoleLabel,
  MAX_PASSWORD,
  PASSWORD_PROBLEM,
  PHONE_PROBLEM,
  placeText
} from './accountRules';
import { formClass } from './accountStyles';
import { usePasswordForm, usePhoneForm } from './useSecretForms';



/**
 * facility-0054 rule 4: the Admin types a Cleaner's or Supervisor's new phone. It is sent once, then cleared; every
 * device of the person must log in again. The old number cannot be shown, because it is stored as a hash, and it is
 * not needed. So the form says so and shows who is being edited (facility-0081).
 */
export function PhoneForm({ account, onDone, onCancel, embedded = false, disabled = false }: SecretFormProps & { embedded?: boolean; disabled?: boolean }) {
  const { names, phone, setPhone, problem, isLoading, titleId, submit } = usePhoneForm({ account, onDone, onCancel });

  return (
    <form className={embedded ? "flex flex-col gap-4 border-t border-border pt-5" : formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      {embedded ? <h3 id={titleId} className="font-semibold">{`แก้เบอร์โทร · ${account.displayName}`}</h3> : <DialogHeader id={titleId} title={`แก้เบอร์โทร · ${account.displayName}`} onClose={onCancel} closeDisabled={isLoading} />}
      <div className={embedded ? "flex flex-col gap-4" : "-mx-6 min-h-0 flex-1 overflow-y-auto px-6 pb-5 flex flex-col gap-5"}>
        {/* facility-0081: who the form is for; the owner found 19 px bold too large (2026-10-26), so the normal size */}
        {!embedded && <Facts
          items={[
            {
              label: 'ชื่อ',
              value: account.displayName,
            },
            {
              label: 'รหัสพนักงาน',
              value: account.loginName,
            },
            {
              label: 'บทบาท',
              value: accountRoleLabel[account.role],
            },
            {
              label: 'ประจำที่',
              value: placeText(account, names),
            },
          ]}
        />}
        <Field
          label="เบอร์โทรที่คนนี้ใช้ตอนนี้"
          error={problem === PHONE_PROBLEM ? problem : undefined}
          help="ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก · เบอร์ใช้แทนรหัสผ่าน ระบบจึงเก็บแบบที่เปิดดูไม่ได้ · บันทึกแล้วทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ · ถ้าใส่เบอร์เดิม ระบบไม่เปลี่ยนอะไรและเครื่องเดิมยังใช้งานได้"
        >
          <TextInput
            size="desk"
            type="tel"
            inputMode="numeric"
            autoComplete="off"
            maxLength={16}
            value={phone}
            disabled={isLoading || disabled}
            onChange={(event) => setPhone(event.target.value)}
          />
        </Field>
        {problem && <Note tone="error">{problem}</Note>}
      </div>
      {embedded ? <Button type="submit" size="desk" disabled={isLoading || disabled}>{isLoading ? 'กำลังบันทึกเบอร์...' : 'บันทึกเบอร์ใหม่'}</Button> : <FormButtons sending={isLoading} label="บันทึกเบอร์ใหม่" onCancel={onCancel} />}
    </form>
  );
}

/** facility-0020: a new temporary password for an Admin, told by voice or on paper; sent once, then cleared. */
export function PasswordForm({ account, isSelf, onDone, onCancel, embedded = false, disabled = false }: SecretFormProps & { isSelf: boolean; embedded?: boolean; disabled?: boolean }) {
  const { password, setPassword, problem, isLoading, titleId, submit } = usePasswordForm({ account, isSelf, onDone, onCancel });

  return (
    <form className={embedded ? "flex flex-col gap-4 border-t border-border pt-5" : formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      {embedded ? <h3 id={titleId} className="font-semibold">{`รีเซ็ตรหัสผ่าน · ${account.displayName}`}</h3> : <DialogHeader
        id={titleId}
        title={`รีเซ็ตรหัสผ่าน · ${account.displayName}`}
        onClose={onCancel}
        closeDisabled={isLoading}
      />}
      <div className={embedded ? "flex flex-col gap-4" : "-mx-6 min-h-0 flex-1 overflow-y-auto px-6 pb-5 flex flex-col gap-5"}>
        <Field
          label="รหัสผ่านชั่วคราวใหม่"
          error={problem === PASSWORD_PROBLEM ? problem : undefined}
          help="15–100 ตัวอักษร เป็นวลียาว ๆ ได้ บอกเองด้วยวาจาหรือกระดาษ · ถ้าใส่รหัสเดิม ระบบไม่เปลี่ยนอะไร"
        >
          <TextInput
            size="desk"
            type="password"
            autoComplete="new-password"
            maxLength={MAX_PASSWORD}
            value={password}
            disabled={isLoading || disabled}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>
        {problem && <Note tone="error">{problem}</Note>}
      </div>
      {embedded ? <Button type="submit" size="desk" disabled={isLoading || disabled}>{isLoading ? 'กำลังบันทึกรหัสผ่าน...' : 'บันทึกรหัสผ่านใหม่'}</Button> : <FormButtons sending={isLoading} label="บันทึกรหัสผ่านใหม่" onCancel={onCancel} />}
    </form>
  );
}

/** บันทึก… and ยกเลิก under the phone and password forms. */
function FormButtons({ sending, label, onCancel }: { sending: boolean; label: string; onCancel: () => void }) {
  return (
    <DialogFooter>
      <Button size="desk" disabled={sending} onClick={onCancel}>
        ยกเลิก
      </Button>
      <Button type="submit" variant="primary" size="desk" disabled={sending}>
        {sending ? 'กำลังบันทึก...' : label}
      </Button>
    </DialogFooter>
  );
}
