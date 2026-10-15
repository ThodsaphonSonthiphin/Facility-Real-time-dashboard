import { describe, expect, it } from 'vitest';
import { getErrorMessage } from './getErrorMessage';

describe('getErrorMessage', () => {
  it("shows the server's own Thai message as it is", () => {
    expect(getErrorMessage({ status: 409, data: { message: 'สแกนใหม่อีกครั้ง' } })).toBe('สแกนใหม่อีกครั้ง');
  });

  it('no network', () => {
    expect(getErrorMessage({ status: 'FETCH_ERROR', error: 'TypeError: Failed to fetch' })).toBe(
      'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่',
    );
  });

  it.each([
    [401, 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่'],
    [403, 'บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้'],
    [429, 'ลองหลายครั้งเกินไป รอสักครู่แล้วลองใหม่'],
    [500, 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง'],
    [503, 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง'],
  ])('status %i without a message', (status, text) => {
    expect(getErrorMessage({ status, data: null })).toBe(text);
  });

  it('a blank server message falls back to the status wording', () => {
    expect(getErrorMessage({ status: 403, data: { message: '  ' } })).toBe('บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้');
  });

  it("anything else gets the page's fallback, or the general one", () => {
    expect(getErrorMessage({ status: 400, data: null }, 'บันทึกไม่สำเร็จ')).toBe('บันทึกไม่สำเร็จ');
    expect(getErrorMessage(new Error('x'))).toBe('ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง');
    expect(getErrorMessage(undefined)).toBe('ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง');
  });
});
