import { baseApi } from '../../shared/api/baseApi';

/** AuditCategory on the server: the "ประเภท" filter (facility-0057). */
export type AuditCategory = 'Attendance' | 'Cover' | 'AlertReview' | 'Setup' | 'QrToken' | 'Account' | 'Export';

/**
 * AuditLogEntryDto. before and after are the stored snapshots as JSON objects (PascalCase keys, null fields kept);
 * they never hold a phone number or password (facility-0057). category is missing for an action no category lists.
 */
export interface AuditEntry {
  id: number;
  occurredAt: string;
  /** Missing on a row the server wrote by itself; actorName is then ระบบ (facility-0092). */
  actorId?: number;
  actorName: string;
  action: string;
  category?: AuditCategory;
  entityType: string;
  entityId?: number;
  summary: string;
  before?: unknown;
  after?: unknown;
  reason?: string;
}

/** AuditLogPageDto: nextBeforeId is sent while a page came back full. */
export interface AuditPage {
  entries: AuditEntry[];
  nextBeforeId?: number;
}

/** AuditActorDto: everyone who appears in the log. */
export interface AuditActor {
  id: number;
  displayName: string;
}

/** Both dates or neither (then the server's last 7 dates); missing filters are left out. */
export interface HistoryArgs {
  from?: string;
  to?: string;
  actorId?: number;
  category?: AuditCategory;
}

/** The first page asks for the newest rows: 0 stands for "no beforeId" (ids start at 1). */
const NEWEST = 0;

const historyApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // plan 12 decision 7: older pages by beforeId, newest first
    getAuditLog: build.infiniteQuery<AuditPage, HistoryArgs, number>({
      infiniteQueryOptions: {
        initialPageParam: NEWEST,
        getNextPageParam: (lastPage) => lastPage.nextBeforeId,
      },
      query: ({ queryArg: { from, to, actorId, category }, pageParam }) => ({
        url: '/admin/audit-log',
        params: { from, to, actorId, category, beforeId: pageParam === NEWEST ? undefined : pageParam },
      }),
    }),
    getAuditActors: build.query<AuditActor[], void>({
      query: () => '/admin/audit-log/actors',
    }),
  }),
});

export const { useGetAuditLogInfiniteQuery, useGetAuditActorsQuery } = historyApi;
