import { baseApi } from './baseApi';
import type { AttendanceEvent, BlockedScan, Shift } from './types';

/**
 * The alert list (wireframe D3, facility-0035, 0037, 0041, 0051), read by the alert page and the Admin menu's count
 * (plan 11 decision 12). Fields the server sends as null arrive missing, so they are optional here.
 */

/** AlertKind on the server. */
export type AlertKind = 'BlockedScan' | 'AttendanceGps' | 'SubmissionGps' | 'MissingAttendance' | 'InspectionGps';

/** AlertPersonDto. employeeId is the login name. */
export interface AlertPerson {
  id: number;
  employeeId: string;
  displayName: string;
  role: 'Cleaner' | 'Supervisor' | 'Admin';
  areaCode?: string;
}

/** CoverSuggestionDto: what to send to assign the cover, while one can still be given. areaId for a Cleaner, buildingId for a Supervisor. */
export interface CoverSuggestion {
  userId: number;
  areaId?: number;
  buildingId?: number;
  shiftDate: string;
  shift: Shift;
}

/**
 * AlertDto. recordId is the row behind it; the same id may come with two kinds, so an alert is known by kind and id.
 * at is when it happened; for MissingAttendance it is the shift's end. areaCode is where it happened.
 */
export interface Alert {
  kind: AlertKind;
  recordId: number;
  at: string;
  person: AlertPerson;
  areaCode: string;
  areaName: string;
  signCode?: string;
  pointName?: string;
  distanceM?: number;
  radiusM?: number;
  accuracyM?: number;
  missingEvents: AttendanceEvent[];
  reviewed: boolean;
  reviewedAt?: string;
  cover?: CoverSuggestion;
  blockReason?: BlockedScan['reason'];
}

const alertListApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Yesterday and today, newest first (AlertQuery). */
    getAlerts: build.query<Alert[], void>({
      query: () => '/admin/alerts',
      providesTags: ['Alerts'],
    }),
  }),
});

export const { useGetAlertsQuery } = alertListApi;

/** The alerts no Admin has closed yet: the menu's count, wireframe D1 "รายการที่ขึ้นเตือน (4)". */
export function openAlertCount(alerts: readonly Alert[] | undefined): number {
  return alerts ? alerts.filter((alert) => !alert.reviewed).length : 0;
}
