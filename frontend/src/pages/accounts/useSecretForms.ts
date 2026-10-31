import { useState, type FormEvent } from 'react';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import {
  MAX_PASSWORD,
  MIN_PASSWORD,
  normalizePhone,
  PASSWORD_PROBLEM,
  PHONE_PROBLEM
} from './accountRules';
import { useChangePhoneMutation, useResetPasswordMutation } from './accountsApi';

import type { SecretFormProps } from './accountFormTypes';

export function usePhoneForm({ account, onDone }: SecretFormProps) {
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

  return { names, phone, setPhone, problem, isLoading, titleId, submit };
}

export function usePasswordForm({ account, isSelf, onDone }: SecretFormProps & { isSelf: boolean }) {
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

  return { password, setPassword, problem, isLoading, titleId, submit };
}
