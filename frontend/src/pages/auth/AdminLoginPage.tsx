import { Eye, EyeOff, LoaderCircle } from 'lucide-react';
import { Link, Navigate } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { Button } from '../../shared/components/Button';
import { Field, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { inlineLinkClass } from '../../shared/components/styles';
import { useAdminLoginPage } from './useAdminLoginPage';

/** D0: Enterprise Light layout, with the existing authentication and navigation contract. */
export function AdminLoginPage() {
  const page = useAdminLoginPage();

  if (page.redirectTo) {
    return <Navigate to={page.redirectTo} replace />;
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-bg px-4 py-12 text-sm text-text">
      <div className="flex w-full max-w-100 flex-col gap-6">
        <header className="flex flex-col gap-1">
          <span className="font-semibold text-text-2">ระบบตรวจงานแม่บ้าน</span>
          <h1 className="text-2xl font-bold">แอดมินเข้าสู่ระบบ</h1>
        </header>
        <form
          className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-6"
          onSubmit={page.onSubmit}
          aria-busy={page.isLoading}
        >
          {page.returnsToPage && <Note tone="info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</Note>}
          {page.errorText && <div id={page.errorId}><Note tone="error">{page.errorText}</Note></div>}
          <Field label="ชื่อผู้ใช้">
            <TextInput
              size="desk"
              className="h-10"
              type="text"
              autoComplete="username"
              required
              disabled={page.isLoading}
              value={page.username}
              onChange={(event) => page.setUsername(event.target.value)}
            />
          </Field>
          <div className="flex flex-col gap-1">
            <label htmlFor={page.passwordId} className="font-semibold">รหัสผ่าน</label>
            <div className="flex gap-2">
              <TextInput
                id={page.passwordId}
                size="desk"
                className="h-10 min-w-0 flex-1"
                type={page.passwordVisible ? 'text' : 'password'}
                autoComplete="current-password"
                required
                disabled={page.isLoading}
                aria-invalid={page.credentialInvalid || undefined}
                aria-describedby={page.credentialInvalid ? page.errorId : undefined}
                value={page.password}
                onChange={(event) => page.setPassword(event.target.value)}
              />
              <Button size="desk" className="h-10 shrink-0" onClick={page.togglePassword} disabled={page.isLoading}>
                {page.passwordVisible
                  ? <EyeOff aria-hidden="true" className="size-4" />
                  : <Eye aria-hidden="true" className="size-4" />}
                {page.passwordVisible ? 'ซ่อน' : 'แสดง'}
              </Button>
            </div>
          </div>
          <Button type="submit" variant="primary" size="desk" className="h-10 w-full" disabled={page.isLoading}>
            {page.isLoading && <LoaderCircle aria-hidden="true" className="size-4 motion-safe:animate-spin" />}
            {page.isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
          </Button>
        </form>
        <footer className="flex flex-col gap-2 text-center text-xs text-text-2">
          <span>ลืมรหัสผ่าน ให้แอดมินคนอื่นรีเซ็ตให้ที่หน้า บัญชีผู้ใช้</span>
          <Link className={inlineLinkClass} to="/login" state={page.linkState}>
            หน้าเข้าสู่ระบบของพนักงาน
          </Link>
          <span>เวอร์ชัน {buildCommit}</span>
        </footer>
      </div>
    </main>
  );
}
