import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import type { CleaningStatus, Placement, PointCard, PointStatus } from '../../shared/api/types';
import { readLocation, type GpsFix } from '../../shared/geo/readLocation';

/** InspectionSubmissionDto: the submission the Supervisor judges (M6 "ส่งงานล่าสุด"). note is left out when empty. */
export interface InspectionSubmission {
  id: number;
  submittedAt: string;
  cleanerName: string;
  placement: Placement;
  status: CleaningStatus;
  issueTags: string[];
  note?: string;
}

/**
 * InspectionScanDto. Without a submission, refusal says why there is nothing to inspect (facility-0047 rule 7).
 * isCover: allowed here only by a Cover Assignment. distanceM and withinRadius are left out when the sign has no
 * coordinates.
 */
export interface InspectionScan {
  point: PointCard;
  submission?: InspectionSubmission;
  refusal?: string;
  isCover: boolean;
  distanceM?: number;
  withinRadius?: boolean;
}

/** What the page opened with: the scan's answer and the accuracy of the fix it sent (shown as ±). */
export interface InspectionVisit extends InspectionScan {
  accuracyM: number;
}

/** facility-0029: Passed = สะอาด, Rework = ไม่สะอาด. */
export type InspectionResult = 'Passed' | 'Rework';

/** CreateInspectionRequest. scanRecordId is the submission the scan showed; defect goes only with Rework. */
export interface RecordInspectionRequest extends GpsFix {
  qrToken: string;
  scanRecordId: number;
  result: InspectionResult;
  defect?: string;
}

/** InspectionCreatedResponse (201). withinRadius false is a Geofence Flag (the INSPECTION_GPS alert). */
export interface InspectionRecorded {
  inspectionId: number;
  servicePointId: number;
  scanRecordId: number;
  result: InspectionResult;
  newPointStatus: PointStatus;
  distanceM?: number;
  withinRadius?: boolean;
  inspectedAt: string;
}

const inspectApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * Plan 9 decision 3: the scan runs when the page opens, with a fresh fix (facility-0037). It is a query, so StrictMode
     * and a quick remount share one request. It records nothing unless the scan is blocked (facility-0048). A location
     * problem is a CUSTOM_ERROR whose `error` is the LocationProblem.
     */
    openInspection: build.query<InspectionVisit, string>({
      async queryFn(qrToken, _api, _extraOptions, baseQuery) {
        const reading = await readLocation();
        if (reading.kind !== 'fix') {
          return { error: { status: 'CUSTOM_ERROR' as const, error: reading.kind } };
        }
        const answer = await baseQuery({
          url: '/inspections/scan',
          method: 'POST',
          body: { qrToken, ...reading.fix },
          timeout: SEND_TIMEOUT_MS,
        });
        if (answer.error) {
          return { error: answer.error };
        }
        return { data: { ...(answer.data as InspectionScan), accuracyM: reading.fix.accuracyM } };
      },
    }),
    recordInspection: build.mutation<InspectionRecorded, RecordInspectionRequest>({
      query: (body) => ({ url: '/inspections', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
    }),
  }),
});

export const { useOpenInspectionQuery, useRecordInspectionMutation } = inspectApi;
