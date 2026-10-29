import { LogIn } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { Spinner } from '../../shared/components/PageState';
import { inlineLinkClass } from '../../shared/components/styles';
import { useLogin } from './useLogin';

/** Wireframe M0: Cleaners and Supervisors log in with employee ID + phone on their phone (facility-0054). */
export function LoginPage() {
  const [employeeId, setEmployeeId] = useState('');
  const [phone, setPhone] = useState('');
  const { submit, isLoading, errorText, returnsToPage, redirectTo } = useLogin('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง');
  const location = useLocation();

  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    void submit({ employeeId: employeeId.trim(), phone });
  };

  return (
    // A phone page (facility-0087): body text-base, the one main action at the bottom in thumb reach, in the white bar
    // (the design file's Login). The top padding clears the notch; no token covers env(), hence the arbitrary value
    <div className="mx-auto flex min-h-svh max-w-mobile flex-col text-base text-text">
      <main className="flex flex-1 flex-col gap-6 px-4 pt-[max(48px,env(safe-area-inset-top))] pb-4">
        <header className="flex flex-col gap-1">
          <span className="text-sm font-semibold text-text-2">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
          <h1 className="text-2xl font-bold">เข้าสู่ระบบ</h1>
          <span className="text-text-2">ใช้รหัสพนักงานและเบอร์โทรของคุณ</span>
        </header>
        {returnsToPage && <Note tone="info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</Note>}
        {errorText && <Note tone="error">{errorText}</Note>}
        <form id="login-form" className="flex flex-col gap-5" onSubmit={onSubmit}>
          <Field label="รหัสพนักงาน" help="เลข 7 หลัก">
            <TextInput
              size="code"
              type="text"
              inputMode="numeric"
              autoComplete="username"
              required
              value={employeeId}
              onChange={(event) => setEmployeeId(event.target.value)}
            />
          </Field>
          <Field label="เบอร์โทรศัพท์" help="เบอร์ที่แจ้งแอดมินไว้ตอนสร้างบัญชี">
            <TextInput
              size="code"
              type="tel"
              inputMode="tel"
              autoComplete="current-password"
              required
              aria-invalid={errorText ? true : undefined}
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
            />
          </Field>
        </form>
        <footer className="flex flex-col gap-1 text-sm text-text-2">
          <span>เปลี่ยนเบอร์โทร: แจ้งแอดมินให้แก้ · ไม่มีการสมัครเอง</span>
          <span>
            แอดมินเข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·{' '}
            <Link className={inlineLinkClass} to="/login/admin" state={location.state}>
              หน้าแอดมิน
            </Link>
          </span>
          <span className="text-xs">เวอร์ชัน {buildCommit}</span>
        </footer>
      </main>
      <div className="sticky bottom-0 border-t border-border bg-surface px-4 pt-3 pb-[max(12px,env(safe-area-inset-bottom))]">
        <Button type="submit" form="login-form" variant="primary" size="main" disabled={isLoading}>
          {isLoading ? <Spinner /> : <LogIn aria-hidden="true" className="size-6" />}
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </Button>
      </div>
    </div>
  );
}
