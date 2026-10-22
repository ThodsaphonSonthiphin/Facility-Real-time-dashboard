import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { useLogin } from './useLogin';
import './auth.css';

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
    <main className="auth-page auth-page--desk">
      <form className="auth-form auth-card" onSubmit={onSubmit}>
        <header className="auth-heading">
          <span className="auth-kicker">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
          <h1>แอดมินเข้าสู่ระบบ</h1>
        </header>
        {returnsToPage && (
          <p className="auth-note auth-note--info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</p>
        )}
        <label htmlFor="admin-username">ชื่อผู้ใช้</label>
        <input
          id="admin-username"
          type="text"
          autoComplete="username"
          required
          value={username}
          onChange={(event) => setUsername(event.target.value)}
        />
        <label htmlFor="admin-password">รหัสผ่าน</label>
        <input
          id="admin-password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
        {errorText && (
          <p className="auth-note auth-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="auth-submit" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </button>
        <footer className="auth-footer">
          <span>
            แม่บ้านและหัวหน้าเข้าด้วยรหัสพนักงาน + เบอร์โทรบนมือถือ ·{' '}
            <Link to="/login" state={location.state}>
              หน้าเข้าสู่ระบบของพนักงาน
            </Link>
          </span>
          <span>ลืมรหัสผ่าน: ให้แอดมินอีกคนรีเซ็ต</span>
          <span className="auth-build">เวอร์ชัน {buildCommit}</span>
        </footer>
      </form>
    </main>
  );
}
