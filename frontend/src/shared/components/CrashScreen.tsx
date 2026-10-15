import { useEffect } from 'react';
import { useRouteError } from 'react-router';
import './shared.css';

/**
 * facility-0068 item 2: the root route's errorElement, instead of a white page. Item 4: the error goes to the
 * browser console only; nothing leaves the phone.
 */
export function CrashScreen() {
  const error = useRouteError();

  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="app-status">
      <h1 className="app-status__title">เกิดข้อผิดพลาด</h1>
      <p className="app-status__detail">หน้านี้แสดงผลไม่ได้ กดโหลดใหม่เพื่อลองอีกครั้ง</p>
      <button type="button" className="app-button app-button--primary" onClick={() => window.location.reload()}>
        โหลดใหม่
      </button>
    </main>
  );
}
