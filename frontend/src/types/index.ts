export type PointStatus = 'Normal' | 'Overdue' | 'Issue' | 'OffHours';
export type ScanStatus = 'Normal' | 'Issue';

export interface ServicePointStatus {
  id: number;
  name: string;
  location: string;
  cleaningIntervalMinutes: number;
  qrToken: string;
  currentStatus: PointStatus;
  lastScannedAt: string | null;
  lastCleanerName: string | null;
  lastScanStatus: ScanStatus | null;
  lastIssueTags: string[] | null;
  lastNotes: string | null;
  minutesSinceLastScan: number;
}

export interface UserSession {
  id: number;
  username: string;
  fullName: string;
  role: string;
  token: string;
}

export interface CreateScanRequest {
  qrToken: string;
  userId: number;
  status: ScanStatus;
  issueTags?: string[];
  notes?: string;
}

export interface CreateScanResponse {
  scanRecordId: number;
  servicePointId: number;
  newPointStatus: PointStatus;
  scannedAt: string;
}

/** The account in a login or refresh response (ADR facility-0011). */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: string;
}

/** Body of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie. */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}
