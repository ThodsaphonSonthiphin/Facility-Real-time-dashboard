import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { useLogin } from './useLogin';
import './auth.css';

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
    <main className="auth-page">
      <header className="auth-heading">
        <span className="auth-kicker">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
        <h1>เข้าสู่ระบบ</h1>
      </header>
      {returnsToPage && (
        <p className="auth-note auth-note--info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</p>
      )}
      <form className="auth-form" onSubmit={onSubmit}>
        <div className="auth-fields">
          <label htmlFor="login-employee-id">รหัสพนักงาน</label>
          <input
            id="login-employee-id"
            type="text"
            inputMode="numeric"
            autoComplete="username"
            required
            value={employeeId}
            onChange={(event) => setEmployeeId(event.target.value)}
          />
          <label htmlFor="login-phone">เบอร์โทรศัพท์</label>
          <input
            id="login-phone"
            type="tel"
            inputMode="tel"
            autoComplete="current-password"
            required
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
          />
          <span className="auth-hint">เบอร์ที่แจ้ง Admin ไว้ตอนสร้างบัญชี</span>
        </div>
        {errorText && (
          <p className="auth-note auth-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="auth-submit" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </button>
      </form>
      <footer className="auth-footer">
        <span>เปลี่ยนเบอร์โทร: แจ้ง Admin ให้แก้ · ไม่มีการสมัครเอง</span>
        <span>
          Admin เข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·{' '}
          <Link to="/login/admin" state={location.state}>
            หน้า Admin
          </Link>
        </span>
        <span className="auth-build">เวอร์ชัน {buildCommit}</span>
      </footer>
    </main>
  );
}
