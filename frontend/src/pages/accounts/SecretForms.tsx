import { useState, type FormEvent } from 'react';
import { Facts } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { DialogHeader } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useBuildingNames } from '../../shared/api/buildingNames';
import {
  accountRoleLabel,
  MAX_PASSWORD,
  MIN_PASSWORD,
  normalizePhone,
  PASSWORD_PROBLEM,
  PHONE_PROBLEM,
  placeText,
} from './accountRules';
import { formClass } from './accountStyles';
import { useChangePhoneMutation, useResetPasswordMutation, type Account } from './accountsApi';

interface SecretFormProps {
  account: Account;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * facility-0054 rule 4: the Admin types a Cleaner's or Supervisor's new phone. It is sent once, then cleared; every
 * device of the person must log in again. The old number cannot be shown, because it is stored as a hash, and it is
 * not needed. So the form says so and shows who is being edited (facility-0081).
 */
export function PhoneForm({ account, onDone, onCancel }: SecretFormProps) {
  const names = useBuildingNames();
  const [phone, setPhone] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [changePhone, { isLoading }] = useChangePhoneMutation();
  const titleId = `accounts-phone-${account.id}`;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const normalized = normalizePhone(phone);
    if (!normalized) {
      setProblem(PHONE_PROBLEM);
      return;
    }
    setProblem(null);
    try {
      await changePhone({ id: account.id, phone: normalized }).unwrap();
      setPhone('');
      onDone(`เปลี่ยนเบอร์โทรของ ${account.displayName} แล้ว ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className={formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <DialogHeader id={titleId} title={`แก้เบอร์โทร · ${account.displayName}`} onClose={onCancel} closeDisabled={isLoading} />
      {/* facility-0081: who the form is for; the owner found 19 px bold too large (2026-10-26), so the normal size */}
      <Facts
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
      />
      <Field
        label="เบอร์โทรที่คนนี้ใช้ตอนนี้"
        help="ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก · เบอร์ใช้แทนรหัสผ่าน ระบบจึงเก็บแบบที่เปิดดูไม่ได้ · บันทึกแล้วทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ · ถ้าใส่เบอร์เดิม ระบบไม่เปลี่ยนอะไรและเครื่องเดิมยังใช้งานได้"
      >
        <TextInput
          size="desk"
          type="tel"
          inputMode="numeric"
          autoComplete="off"
          maxLength={16}
          value={phone}
          disabled={isLoading}
          onChange={(event) => setPhone(event.target.value)}
        />
      </Field>
      {problem && <Note tone="error">{problem}</Note>}
      <FormButtons sending={isLoading} label="บันทึกเบอร์ใหม่" onCancel={onCancel} />
    </form>
  );
}

/** facility-0020: a new temporary password for an Admin, told by voice or on paper; sent once, then cleared. */
export function PasswordForm({ account, isSelf, onDone, onCancel }: SecretFormProps & { isSelf: boolean }) {
  const [password, setPassword] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [resetPassword, { isLoading }] = useResetPasswordMutation();
  const titleId = `accounts-password-${account.id}`;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (password.length < MIN_PASSWORD || password.length > MAX_PASSWORD) {
      setProblem(PASSWORD_PROBLEM);
      return;
    }
    setProblem(null);
    try {
      await resetPassword({ id: account.id, password }).unwrap();
      setPassword('');
      // Resetting your own password revokes this browser's session too (facility-0014)
      onDone(
        `รีเซ็ตรหัสผ่านของ ${account.displayName} แล้ว บอกรหัสใหม่ด้วยวาจาหรือกระดาษ ทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่${isSelf ? ' รวมเครื่องนี้ด้วย' : ''}`,
      );
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <form className={formClass} aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <DialogHeader
        id={titleId}
        title={`รีเซ็ตรหัสผ่าน · ${account.displayName}`}
        onClose={onCancel}
        closeDisabled={isLoading}
      />
      <Field
        label="รหัสผ่านชั่วคราวใหม่"
        help="15–100 ตัวอักษร เป็นวลียาว ๆ ได้ บอกเองด้วยวาจาหรือกระดาษ · ถ้าใส่รหัสเดิม ระบบไม่เปลี่ยนอะไร"
      >
        <TextInput
          size="desk"
          type="password"
          autoComplete="new-password"
          maxLength={MAX_PASSWORD}
          value={password}
          disabled={isLoading}
          onChange={(event) => setPassword(event.target.value)}
        />
      </Field>
      {problem && <Note tone="error">{problem}</Note>}
      <FormButtons sending={isLoading} label="บันทึกรหัสผ่านใหม่" onCancel={onCancel} />
    </form>
  );
}

/** บันทึก… and ยกเลิก under the phone and password forms. */
function FormButtons({ sending, label, onCancel }: { sending: boolean; label: string; onCancel: () => void }) {
  return (
    <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
      <Button size="desk" disabled={sending} onClick={onCancel}>
        ยกเลิก
      </Button>
      <Button type="submit" variant="primary" size="desk" disabled={sending}>
        {sending ? 'กำลังบันทึก...' : label}
      </Button>
    </div>
  );
}
