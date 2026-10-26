import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { boxClass, inlineLinkClass } from '../../shared/components/styles';
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
    // A phone page (facility-0087): body text-base, the one main action at the bottom in thumb reach. The bottom
    // padding grows with the phone's home-bar inset so the button never sits under it (pages/phone.md); no token
    // covers env(), hence the arbitrary value
    <main className="mx-auto flex min-h-screen max-w-phone flex-col gap-4 px-4 pt-6 pb-[max(1.5rem,env(safe-area-inset-bottom))] text-base text-text">
      <header className="flex flex-col gap-0.5">
        <span className="text-xs tracking-wide text-muted">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
        <h1 className="text-2xl font-bold">เข้าสู่ระบบ</h1>
      </header>
      {returnsToPage && <Note tone="info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</Note>}
      <form className="flex grow flex-col gap-4" onSubmit={onSubmit}>
        <div className={`flex flex-col gap-3 ${boxClass}`}>
          <Field label="รหัสพนักงาน">
            <TextInput
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
              type="tel"
              inputMode="tel"
              autoComplete="current-password"
              required
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
            />
          </Field>
        </div>
        {errorText && <Note tone="error">{errorText}</Note>}
        <Button type="submit" variant="primary" size="main" className="mt-auto" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </Button>
      </form>
      <footer className="flex flex-col gap-1 text-center text-xs text-muted">
        <span>เปลี่ยนเบอร์โทร: แจ้งแอดมินให้แก้ · ไม่มีการสมัครเอง</span>
        <span>
          แอดมินเข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·{' '}
          <Link className={inlineLinkClass} to="/login/admin" state={location.state}>
            หน้าแอดมิน
          </Link>
        </span>
        <span>เวอร์ชัน {buildCommit}</span>
      </footer>
    </main>
  );
}
