/**
 * facility-0022 item 4: one wording for the faults every page shares. The server's own Thai message
 * ({ "message": ... }, ApiResults.Message) is shown as it is. A page passes its own text only where the context differs.
 */
export function getErrorMessage(error: unknown, fallback = 'ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง'): string {
  if (typeof error !== 'object' || error === null || !('status' in error)) {
    return fallback;
  }
  const { status, data } = error as { status: unknown; data?: unknown };
  if (status === 'FETCH_ERROR' || status === 'TIMEOUT_ERROR') {
    return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่';
  }
  const message = typeof data === 'object' && data !== null ? (data as { message?: unknown }).message : undefined;
  if (typeof message === 'string' && message.trim() !== '') {
    return message;
  }
  if (status === 401) {
    return 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่';
  }
  if (status === 403) {
    return 'บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้';
  }
  if (status === 429) {
    return 'ลองหลายครั้งเกินไป รอสักครู่แล้วลองใหม่';
  }
  if (typeof status === 'number' && status >= 500) {
    return 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง';
  }
  return fallback;
}
