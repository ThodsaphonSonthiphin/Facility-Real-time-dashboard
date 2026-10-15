import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import type { CleaningStatus, Placement, PointCard, PointStatus } from '../../shared/api/types';
import type { GpsFix } from '../../shared/geo/readLocation';

/** CreateScanRecordRequest. "notes" keeps the phase-1 field name. Who scanned comes from the token (facility-0017). */
export interface SubmitScanRequest extends GpsFix {
  qrToken: string;
  status: CleaningStatus;
  issueTags?: string[];
  notes?: string;
}

/** ScanRecordCreatedResponse (201). withinRadius false is a Geofence Flag; null means the sign has no coordinates yet. */
export interface ScanRecordCreated {
  scanRecordId: number;
  servicePointId: number;
  placement: Placement;
  lateMinutes: number | null;
  newPointStatus: PointStatus;
  distanceM: number | null;
  withinRadius: boolean | null;
  submittedAt: string;
}

const scanApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // facility-0059: the token comes only from the URL the sign's QR opened
    getPointByToken: build.query<PointCard, string>({
      query: (token) => `/service-points/by-token/${encodeURIComponent(token)}`,
    }),
    submitScan: build.mutation<ScanRecordCreated, SubmitScanRequest>({
      query: (body) => ({ url: '/scan-records', method: 'POST', body, timeout: SEND_TIMEOUT_MS }),
    }),
  }),
});

export const { useGetPointByTokenQuery, useSubmitScanMutation } = scanApi;
