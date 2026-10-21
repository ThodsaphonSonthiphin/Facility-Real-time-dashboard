import { baseApi } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';

/**
 * InspectionSummaryRowDto (facility-0048 rule 5, plan 6 decision 15). notInspected counts finished rounds left
 * ไม่ได้ตรวจ in closed shifts; it is not submitted minus submittedInspected (plan 6 decision 17).
 */
export interface SummaryRow {
  supervisorId: number;
  employeeId: string;
  displayName: string;
  buildingCode: string;
  shift: Shift;
  pointsInspected: number;
  pointsTotal: number;
  submitted: number;
  submittedInspected: number;
  notInspected: number;
  inspections: number;
  reworks: number;
  gpsFlagged: number;
}

/** InspectionSummaryDto: from and to are the Thai shift dates the server counted, both included. */
export interface InspectionSummary {
  from: string;
  to: string;
  rows: SummaryRow[];
}

/** Both dates or neither (then the server's last 7 dates); no buildingId is every building. */
export interface SummaryArgs {
  from?: string;
  to?: string;
  buildingId?: number;
}

const summaryApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getInspectionSummary: build.query<InspectionSummary, SummaryArgs>({
      query: ({ from, to, buildingId }) => ({ url: '/admin/inspection-summary', params: { from, to, buildingId } }),
    }),
  }),
});

export const { useGetInspectionSummaryQuery } = summaryApi;
