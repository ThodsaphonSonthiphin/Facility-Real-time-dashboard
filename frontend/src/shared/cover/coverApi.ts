import { boardTag } from '../api/attendanceBoardApi';
import { baseApi } from '../api/baseApi';
import type { Shift } from '../api/types';

/** CoverCandidateDto (wireframe D3 "ให้ใครทำแทน"): their own Area, their Shift-In, and their latest scan at this Area's signs. */
export interface CoverCandidate {
  id: number;
  employeeId: string;
  displayName: string;
  areaCode: string;
  shiftInAt?: string;
  scannedThisAreaAt?: string;
}

/** CoverAssignmentDto: the Area fields for a Cleaner's cover, the building fields for a Supervisor's. */
export interface CoverAssignment {
  id: number;
  userId: number;
  personName: string;
  areaId?: number;
  areaCode?: string;
  buildingId?: number;
  buildingCode?: string;
  shiftDate: string;
  shift: Shift;
  assignedAt: string;
}

/** AssignCoverRequest: areaId for a Cleaner (facility-0041), buildingId for a Supervisor (facility-0048), never both. */
export interface AssignCoverRequest {
  userId: number;
  areaId?: number;
  buildingId?: number;
  shiftDate: string;
  shift: Shift;
  /** From a Blocked Scan on the list: the server also closes that alert (facility-0041 rule 4). */
  blockedScanId?: number;
}

/**
 * A cover changes the Attendance Board and may close an alert. Refetch after a refusal too, since another Admin
 * may have acted first.
 */
const coverApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /** Changes with every check-in and cover, so it follows the board's tag for its shift: the hub invalidates that one. */
    getCoverCandidates: build.query<CoverCandidate[], { areaId: number; shiftDate: string; shift: Shift }>({
      query: ({ areaId, shiftDate, shift }) => ({
        url: '/admin/cover-assignments/candidates',
        params: { areaId, shiftDate, shift },
      }),
      providesTags: (_result, _error, { shiftDate, shift }) => [boardTag(shiftDate, shift)],
    }),
    assignCover: build.mutation<CoverAssignment, AssignCoverRequest>({
      query: (body) => ({ url: '/admin/cover-assignments', method: 'POST', body }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
    cancelCover: build.mutation<void, number>({
      query: (id) => ({ url: `/admin/cover-assignments/${id}`, method: 'DELETE' }),
      invalidatesTags: ['AttendanceBoard', 'Alerts'],
    }),
  }),
});

export const { useGetCoverCandidatesQuery, useAssignCoverMutation, useCancelCoverMutation } = coverApi;
