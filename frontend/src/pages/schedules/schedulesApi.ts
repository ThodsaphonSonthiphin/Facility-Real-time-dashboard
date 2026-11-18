import type { RootState } from '../../store';
import { getValidAccessToken } from '../../shared/auth/session';
import { baseApi, SEND_TIMEOUT_MS } from '../../shared/api/baseApi';
import { ADMIN_HUB_SERVER_TIMEOUT_MS, followScanHub } from '../../shared/api/scanHub';
import type {
  CommandResult,
  ReliefCandidate,
  ConfirmTransferRequest,
  ScheduleCommandEnvelope,
  ScheduleCommandToken,
  ScheduleWeek,
  TransferResult,
} from '../../shared/api/scheduleTypes';
import type { Shift } from '../../shared/api/types';
import { scheduleChanged } from './scheduleViewSlice';

/**
 * The scheduling reads and writes of spec §11, as RTK Query endpoints (facility-0022): a page never calls fetch itself,
 * and one cache serves every screen that shows the same week.
 *
 * Every week is asked with its Monday (`weekStart`), because the server binds it as a `DateOnly` and answers a week, not a
 * date range. A management read adds the building and shift filters; a Supervisor sends neither and the server decides
 * their own dates and buildings from the dated periods, so a query cannot widen a read (spec §3).
 *
 * Revisions are decimal strings throughout, exactly as they arrive: the write contract takes back what the read gave, and
 * a number would lose the format the server compares.
 */
export interface WeekArgs {
  /** The Monday of the week, "yyyy-MM-dd". */
  weekStart: string;
}

/** A management read's filters: missing building means every building the actor may read, missing shift their own. */
export interface ManagedWeekArgs extends WeekArgs {
  buildingId?: number;
  shift?: Shift;
}

/**
 * One scoped week's cache tag (plan 11 decision 5's pattern): the week plus the filters that were asked, so a push can
 * invalidate a week without asking for a different building's roster again. `id` is explicit, so an unfiltered week and a
 * filtered one are different entries of the same tag type, never one cache key.
 */
export function scheduleTag(args: WeekArgs & { buildingId?: number; shift?: Shift }) {
  return { type: 'Schedules' as const, id: `${args.weekStart}:${args.buildingId ?? 'all'}:${args.shift ?? 'own'}` };
}

/** One hub connection per read on screen (plan 11 decision 4); the page's stale notice reads the same slice. */
export function scheduleHubKey(args: WeekArgs & { buildingId?: number; shift?: Shift }): string {
  return `schedule:${args.weekStart}:${args.buildingId ?? 'all'}:${args.shift ?? 'own'}`;
}

export type ReliefCandidateArgs = {
  sourceBuildingId: number;
  destinationAreaId: number;
  date: string;
  shift: Shift;
} & ({ employeeId: string; ownUserId?: never } | { ownUserId: number; employeeId?: never });

const schedulesApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    getReliefCandidate: build.query<ReliefCandidate, ReliefCandidateArgs>({
      query: (params) => ({ url: '/schedules/relief-candidates', params, cache: 'no-store' }),
      keepUnusedDataFor: 0,
      providesTags: ['Schedules'],
    }),

    /**
     * GET /api/schedules/me (spec §3): the caller's own authorised week, the Cleaner's only read. It has no filter to
     * widen, so the server answers exactly one row — a Cleaner's — or the actor's own scope.
     */
    getMySchedule: build.query<ScheduleWeek, WeekArgs>({
      query: ({ weekStart }) => ({ url: '/schedules/me', params: { weekStart } }),
      providesTags: (_week, _error, args) => [scheduleTag(args)],
      async onCacheEntryAdded(args, { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }) {
        await followScanHub({
          key: scheduleHubKey(args),
          serverTimeoutMs: ADMIN_HUB_SERVER_TIMEOUT_MS,
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          // The push names only the command, so the page notes that what it shows may be old and asks again. The notice
          // stays until a read that STARTED after the push succeeds (spec §12); a request in flight is not evidence.
          onSchedule: () => {
            dispatch(scheduleChanged({ at: Date.now() }));
            dispatch(baseApi.util.invalidateTags(['Schedules']));
          },
          onMissed: () => dispatch(baseApi.util.invalidateTags(['Schedules'])),
        });
      },
    }),

    /**
     * GET /api/schedules (spec §3, §11): the scoped management week. A Supervisor's read stays inside the dates and
     * buildings they hold today; an Admin's spans every building and shift. A forged filter is answered by the store's own
     * scope, never by this call, so nothing here is an authority check.
     */
    getManagedSchedule: build.query<ScheduleWeek, ManagedWeekArgs>({
      query: ({ weekStart, buildingId, shift }) => ({
        url: '/schedules',
        params: { weekStart, buildingId, shift },
      }),
      providesTags: (_week, _error, args) => [scheduleTag(args)],
      async onCacheEntryAdded(args, { cacheDataLoaded, cacheEntryRemoved, dispatch, getState }) {
        await followScanHub({
          key: scheduleHubKey(args),
          serverTimeoutMs: ADMIN_HUB_SERVER_TIMEOUT_MS,
          cacheDataLoaded,
          cacheEntryRemoved,
          dispatch,
          accessToken: () => getValidAccessToken(() => getState() as RootState, dispatch),
          onSchedule: () => {
            dispatch(scheduleChanged({ at: Date.now() }));
            dispatch(baseApi.util.invalidateTags(['Schedules']));
          },
          onMissed: () => dispatch(baseApi.util.invalidateTags(['Schedules'])),
        });
      },
    }),

    /**
     * POST /api/schedules/command-tokens (spec §11): the short-lived identity a mutation is submitted under. The client
     * never chooses it — the server binds it to the authenticated actor and the issuance time — so an identity is minted
     * once per proposed command and reused for a retry, never replaced to get past an expiry (A19).
     */
    issueScheduleCommandToken: build.mutation<ScheduleCommandToken, void>({
      query: () => ({ url: '/schedules/command-tokens', method: 'POST', timeout: SEND_TIMEOUT_MS }),
    }),

    /**
     * POST /api/schedules/commands (spec §11, A18): submit one batch. Only a `committed` result means the facts changed.
     * The refusals arrive as HTTP errors too (409 rejected/payload-mismatch, 410 expired, 403 not authorised, 400
     * otherwise) and carry the same body, so a caller reads it from the answer or from the error's `data`.
     *
     * A committed command invalidates every schedule read: the push says which command ran, not which week, and every
     * recipient of it holds a right to refetch its own read (spec §12).
     */
    executeScheduleCommand: build.mutation<CommandResult, ScheduleCommandEnvelope>({
      query: (envelope) => ({ url: '/schedules/commands', method: 'POST', body: envelope, timeout: SEND_TIMEOUT_MS }),
      invalidatesTags: (result) => result?.outcome === 'committed' ? ['Schedules'] : [],
    }),

    /**
     * GET /api/schedules/commands/{id} (spec §11, A19): the retained result of a write whose answer was lost. It answers
     * only for the actor and the scope that made the command, so a lookup can never enumerate another person's commands.
     */
    getScheduleCommandResult: build.query<CommandResult, string>({
      query: (commandId) => ({ url: `/schedules/commands/${commandId}`, timeout: SEND_TIMEOUT_MS, cache: 'no-store' }),
      keepUnusedDataFor: 0,
      async onQueryStarted(_commandId, { dispatch, queryFulfilled }) {
        const answer = await queryFulfilled.catch(() => undefined);
        if (answer?.data.outcome === 'committed') dispatch(baseApi.util.invalidateTags(['Schedules']));
      },
    }),

    /**
     * POST /api/area-transfers/{id}/confirm (spec §9, ADR 0204): the transferred person's arrival at the destination
     * Area's sign. It is an actual fact rather than a schedule command, so it does not mint a token; it sends the revision
     * the page read plus the scanned sign and the phone's location, and the server arbitrates racing confirmations.
     */
    confirmAreaTransfer: build.mutation<TransferResult, { id: number; request: ConfirmTransferRequest }>({
      query: ({ id, request }) => ({ url: `/area-transfers/${id}/confirm`, method: 'POST', body: request }),
      invalidatesTags: ['Schedules', 'MyWork', 'AttendanceBoard'],
    }),
  }),
});

export const {
  useGetMyScheduleQuery,
  useGetReliefCandidateQuery,
  useGetManagedScheduleQuery,
  useIssueScheduleCommandTokenMutation,
  useExecuteScheduleCommandMutation,
  useGetScheduleCommandResultQuery,
  useLazyGetScheduleCommandResultQuery,
  useConfirmAreaTransferMutation,
} = schedulesApi;

/** Hook options for a scheduling read: ask on every open, and every 30 s while the page is visible (spec §12). */
export const schedulePoll = {
  refetchOnMountOrArgChange: true,
  pollingInterval: 30_000,
  refetchOnFocus: true,
  skipPollingIfUnfocused: true,
} as const;
