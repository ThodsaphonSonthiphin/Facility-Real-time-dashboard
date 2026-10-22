import { useEffect } from 'react';
import { useRouteError } from 'react-router';
import { Button } from './Button';
import { statusScreenClass } from './styles';

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
    <main className={statusScreenClass.main}>
      <h1 className={statusScreenClass.title}>เกิดข้อผิดพลาด</h1>
      <p className="text-muted">หน้านี้แสดงผลไม่ได้ กดโหลดใหม่เพื่อลองอีกครั้ง</p>
      <Button variant="primary" size="main" onClick={() => window.location.reload()}>
        โหลดใหม่
      </Button>
    </main>
  );
}
