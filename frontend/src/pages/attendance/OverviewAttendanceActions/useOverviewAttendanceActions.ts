import { useEffect, useEffectEvent, useLayoutEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { attendanceBoardApi, useGetAttendanceBoardQuery, type AttendanceBoard, type BoardRow } from '../../../shared/api/attendanceBoardApi';
import type { AttendanceEvent } from '../../../shared/api/types';
import { livePoll } from '../../../shared/api/baseApi';
import type { Notice } from '../../../shared/components/Toast';
import { readOverviewContext } from '../../../shared/overview/overviewContext';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { useAppDispatch } from '../../../store/hooks';
import { attendanceTargets } from '../attendanceRules';
import type { AttendanceActionKind, OpenAttendanceAction, OverviewAttendanceActionsProps } from './type';

function rowFor(board: AttendanceBoard | undefined, scope: OverviewAttendanceActionsProps) {
  if (!board || board.shiftEnded || board.shift !== scope.shift || board.shiftDate !== scope.shiftDate) return;
  return board.rows.find((row) => row.areaId === scope.areaId);
}

function actionStillMatches(row: BoardRow, action: Pick<OpenAttendanceAction, 'kind' | 'userId'>) {
  return action.kind === 'cover'
    ? row.cleaner?.id === action.userId
    : attendanceTargets(row).some(({ cleaner }) => cleaner.id === action.userId);
}

export function useScopedAttendanceActionArgs(): OverviewAttendanceActionsProps | undefined {
  const [params] = useSearchParams();
  const parsed = readOverviewContext(params, true);
  if (!parsed.ok) return;
  const { areaId, date, shift, view } = parsed.context;
  if (areaId !== undefined && date && shift && view === 'attendance') return { areaId, shiftDate: date, shift };
}

export function useOverviewAttendanceActions(scope: OverviewAttendanceActionsProps) {
  const dispatch = useAppDispatch();
  const { currentData: board, error, isFetching, isSuccess, requestId, refetch } =
    useGetAttendanceBoardQuery({}, { ...livePoll, refetchOnMountOrArgChange: false });
  const [initialRequestId] = useState(requestId);
  const [verified, setVerified] = useState(false);
  // Ignore the request already present at mount. A later success can come from opening, retry, polling or focus.
  if (!verified && isSuccess && !isFetching && requestId && requestId !== initialRequestId) {
    setVerified(true);
  }
  const row = verified ? rowFor(board, scope) : undefined;
  const targets = row ? attendanceTargets(row) : [];
  const [open, setOpen] = useState<OpenAttendanceAction | null>(null);
  const [opening, setOpening] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const action = open && row && actionStillMatches(row, open) ? open : null;
  const target = action?.kind === 'correction' ? targets.find(({ cleaner }) => cleaner.id === action.userId) : undefined;
  const unavailableEvents = new Set(target?.events
    .filter((event) => event.areaId !== scope.areaId).map((event) => event.eventType));
  const nextAction = useRef(0);
  const mounted = useRef(false);
  const activeAction = useRef<OpenAttendanceAction | null>(null);

  useLayoutEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);
  useLayoutEffect(() => {
    activeAction.current = action;
    return () => { activeAction.current = null; };
  }, [action]);

  const warnChanged = () => setNotice({
    kind: 'warn',
    text: 'พื้นที่ กะ หรือแม่บ้านเปลี่ยนแล้ว กรุณาตรวจข้อมูลล่าสุดและเปิดฟอร์มใหม่',
  });

  if (open && !action) {
    setOpen(null);
    warnChanged();
  }

  const freshRow = async () => {
    // RTK Query otherwise joins a pending poll, whose snapshot may precede the start of this action.
    const pending = dispatch(attendanceBoardApi.util.getRunningQueryThunk('getAttendanceBoard', {}));
    if (pending) await pending;
    if (!mounted.current) return;
    return rowFor(await refetch().unwrap(), scope);
  };

  const readOnMount = useEffectEvent(freshRow);
  useEffect(() => {
    // The query result supplies the error and verification state, including refreshes other than this read.
    void readOnMount().catch(() => {});
  }, []);

  const openAction = async (kind: AttendanceActionKind, cleanerId?: number) => {
    if (!row || opening || action) return;
    const userId = kind === 'cover' ? row.cleaner?.id : cleanerId;
    setOpening(true);
    setNotice(null);
    try {
      const fresh = await freshRow();
      if (!mounted.current) return;
      if (!fresh || !actionStillMatches(fresh, { kind, userId })) {
        warnChanged();
        return;
      }
      setOpen({ kind, id: ++nextAction.current, userId });
    } catch (error) {
      if (mounted.current) setNotice({ kind: 'warn', text: getErrorMessage(error) });
    } finally {
      if (mounted.current) setOpening(false);
    }
  };

  const close = (current: OpenAttendanceAction) => {
    setOpen((previous) => previous === current ? null : previous);
  };

  const canSubmit = async (current: OpenAttendanceAction, event?: AttendanceEvent) => {
    if (activeAction.current !== current) return false;
    const latest = await freshRow();
    if (activeAction.current !== current) return false;
    if (latest && actionStillMatches(latest, current)) {
      if (current.kind === 'cover') return true;
      const selected = attendanceTargets(latest).find(({ cleaner }) => cleaner.id === current.userId);
      if (event && selected
        && !selected.events.some((fact) => fact.eventType === event && fact.areaId !== scope.areaId)) return true;
    }
    close(current);
    warnChanged();
    return false;
  };

  return {
    board, row, targets, target, unavailableEvents, action, notice, opening, isFetching, verified,
    error: error ? getErrorMessage(error) : undefined,
    retry: () => void freshRow().catch(() => {}),
    openAction,
    canSubmit,
    close,
    done: (current: OpenAttendanceAction, text: string) => {
      if (activeAction.current !== current) return;
      setNotice({ kind: 'ok', text });
      close(current);
    },
    clearNotice: () => setNotice(null),
  };
}
