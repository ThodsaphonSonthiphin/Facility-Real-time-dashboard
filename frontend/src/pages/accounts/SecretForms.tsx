import { useState, type FormEvent } from 'react';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  accountRoleLabel,
  MAX_PASSWORD,
  MIN_PASSWORD,
  normalizePhone,
  PASSWORD_PROBLEM,
  PHONE_PROBLEM,
  placeText,
} from './accountRules';
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
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`แก้เบอร์โทร · ${account.displayName}`}</h2>
      <dl className="app-facts accounts-who">
        <dt>ชื่อ</dt>
        <dd>{account.displayName}</dd>
        <dt>รหัสพนักงาน</dt>
        <dd>{account.loginName}</dd>
        <dt>บทบาท</dt>
        <dd>{accountRoleLabel[account.role]}</dd>
        <dt>ประจำที่</dt>
        <dd>{placeText(account)}</dd>
      </dl>
      <div className="app-field">
        <label htmlFor={`${titleId}-input`}>เบอร์โทรที่คนนี้ใช้ตอนนี้</label>
        <input
          id={`${titleId}-input`}
          type="tel"
          inputMode="numeric"
          className="app-input"
          autoComplete="off"
          maxLength={16}
          value={phone}
          disabled={isLoading}
          onChange={(event) => setPhone(event.target.value)}
        />
        <span className="app-field__help">
          ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก · เบอร์ใช้แทนรหัสผ่าน ระบบจึงเก็บแบบที่เปิดดูไม่ได้ ·
          บันทึกแล้วทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ · ถ้าใส่เบอร์เดิม ระบบไม่เปลี่ยนอะไรและเครื่องเดิมยังใช้งานได้
        </span>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึกเบอร์ใหม่'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
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
    <form className="accounts-form app-panel" aria-labelledby={titleId} onSubmit={(event) => void submit(event)}>
      <h2 id={titleId}>{`รีเซ็ตรหัสผ่าน · ${account.displayName}`}</h2>
      <div className="app-field">
        <label htmlFor={`${titleId}-input`}>รหัสผ่านชั่วคราวใหม่</label>
        <input
          id={`${titleId}-input`}
          type="password"
          className="app-input"
          autoComplete="new-password"
          maxLength={MAX_PASSWORD}
          value={password}
          disabled={isLoading}
          onChange={(event) => setPassword(event.target.value)}
        />
        <span className="app-field__help">8–100 ตัวอักษร บอกเองด้วยวาจาหรือกระดาษ · ถ้าใส่รหัสเดิม ระบบไม่เปลี่ยนอะไร</span>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="accounts-buttons">
        <button type="submit" className="app-button app-button--primary" disabled={isLoading}>
          {isLoading ? 'กำลังบันทึก...' : 'บันทึกรหัสผ่านใหม่'}
        </button>
        <button type="button" className="app-button" onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </form>
  );
}
