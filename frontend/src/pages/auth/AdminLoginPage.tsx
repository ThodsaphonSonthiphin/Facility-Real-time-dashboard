import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { inlineLinkClass } from '../../shared/components/styles';
import { useLogin } from './useLogin';

/** Wireframe D0: the Admin logs in with username + password (facility-0054). */
export function AdminLoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const { submit, isLoading, errorText, returnsToPage, redirectTo } = useLogin('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  const location = useLocation();

  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    void submit({ username: username.trim(), password });
  };

  return (
    // An Admin page (facility-0087): body text-sm, 36 px controls, one card in the middle of the screen
    <main className="flex min-h-screen items-center justify-center p-6 text-sm text-text">
      <form
        className="flex w-full max-w-100 flex-col gap-3 rounded border border-border bg-surface p-7"
        onSubmit={onSubmit}
      >
        <header className="flex flex-col gap-0.5">
          <span className="text-xs tracking-wide text-muted">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
          <h1 className="text-xl font-bold">แอดมินเข้าสู่ระบบ</h1>
        </header>
        {returnsToPage && <Note tone="info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</Note>}
        <Field label="ชื่อผู้ใช้">
          <TextInput
            size="desk"
            type="text"
            autoComplete="username"
            required
            value={username}
            onChange={(event) => setUsername(event.target.value)}
          />
        </Field>
        <Field label="รหัสผ่าน">
          <TextInput
            size="desk"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </Field>
        {errorText && <Note tone="error">{errorText}</Note>}
        <Button type="submit" variant="primary" size="desk" className="mt-1 w-full" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </Button>
        <footer className="flex flex-col gap-1 text-center text-xs text-muted">
          <span>
            แม่บ้านและหัวหน้าเข้าด้วยรหัสพนักงาน + เบอร์โทรบนมือถือ ·{' '}
            <Link className={inlineLinkClass} to="/login" state={location.state}>
              หน้าเข้าสู่ระบบของพนักงาน
            </Link>
          </span>
          <span>ลืมรหัสผ่าน: ให้แอดมินอีกคนรีเซ็ต</span>
          <span>เวอร์ชัน {buildCommit}</span>
        </footer>
      </form>
    </main>
  );
}
