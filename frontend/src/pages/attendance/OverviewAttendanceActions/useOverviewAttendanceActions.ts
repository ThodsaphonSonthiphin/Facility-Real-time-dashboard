import { useLayoutEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { attendanceBoardApi, useGetAttendanceBoardQuery, type AttendanceBoard } from '../../../shared/api/attendanceBoardApi';
import { livePoll } from '../../../shared/api/baseApi';
import type { Notice } from '../../../shared/components/Toast';
import { readOverviewContext } from '../../../shared/overview/overviewContext';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { useAppDispatch } from '../../../store/hooks';
import type { AttendanceActionKind, OpenAttendanceAction, OverviewAttendanceActionsProps } from './type';

function rowFor(board: AttendanceBoard | undefined, scope: OverviewAttendanceActionsProps) {
  if (!board || board.shiftEnded || board.shift !== scope.shift || board.shiftDate !== scope.shiftDate) return;
  return board.rows.find((row) => row.areaId === scope.areaId);
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
  const { currentData: board, error, isFetching, refetch } = useGetAttendanceBoardQuery({}, livePoll);
  const row = rowFor(board, scope);
  const [open, setOpen] = useState<OpenAttendanceAction | null>(null);
  const [opening, setOpening] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const action = open && row && row.cleaner?.id === open.userId ? open : null;
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

  const openAction = async (kind: AttendanceActionKind) => {
    if (!row || opening || action) return;
    const userId = row.cleaner?.id;
    setOpening(true);
    setNotice(null);
    try {
      const fresh = await freshRow();
      if (!mounted.current) return;
      if (!fresh || fresh.cleaner?.id !== userId) {
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

  const canSubmit = async (current: OpenAttendanceAction) => {
    if (activeAction.current !== current) return false;
    const latest = await freshRow();
    if (activeAction.current !== current) return false;
    if (latest && latest.cleaner?.id === current.userId) return true;
    close(current);
    warnChanged();
    return false;
  };

  return {
    board, row, action, notice, opening, isFetching,
    error: error ? getErrorMessage(error) : undefined,
    retry: () => void refetch(),
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
