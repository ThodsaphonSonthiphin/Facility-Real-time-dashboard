import { ServicePointStatus, UserSession, CreateScanRequest, CreateScanResponse } from '../types';

const BASE_URL = '/api';

export async function loginApi(username: string, password: string): Promise<UserSession> {
  const res = await fetch(`${BASE_URL}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password })
  });

  if (!res.ok) {
    throw new Error('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  }

  return res.json();
}

export async function getServicePointsApi(): Promise<ServicePointStatus[]> {
  const res = await fetch(`${BASE_URL}/service-points`);
  if (!res.ok) {
    throw new Error('ไม่สามารถโหลดข้อมูลจุดบริการได้');
  }
  return res.json();
}

export async function getServicePointByTokenApi(token: string): Promise<ServicePointStatus> {
  const res = await fetch(`${BASE_URL}/service-points/by-token/${encodeURIComponent(token)}`);
  if (!res.ok) {
    throw new Error('ไม่พบข้อมูลจุดบริการสำหรับ QR Token นี้');
  }
  return res.json();
}

export async function createScanRecordApi(req: CreateScanRequest): Promise<CreateScanResponse> {
  const res = await fetch(`${BASE_URL}/scan-records`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.message || 'เกิดข้อผิดพลาดในการบันทึกผลการสแกน');
  }

  return res.json();
}
