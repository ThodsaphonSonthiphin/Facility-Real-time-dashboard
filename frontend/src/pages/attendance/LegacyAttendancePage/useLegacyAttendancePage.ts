import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { useGetAttendanceBoardQuery, type BoardArgs, type BoardRow } from '../../../shared/api/attendanceBoardApi';
import { livePoll } from '../../../shared/api/baseApi';
import type { Shift } from '../../../shared/api/types';
import type { Notice } from '../../../shared/components/Toast';
import type { CoverSlot } from '../../../shared/cover/coverSlots';
import { attendanceTargets, readBoardArgs, readFill, searchRows, toBoardParams } from '../attendanceRules';
import { PAGE_SIZE, type Editing } from './type';

const formKey = (form: Editing) => `${form.areaId}:${form.event ?? ''}:${form.shiftDate}:${form.shift}:${form.userId}`;

export function useLegacyAttendancePage() {
  const [params, setParams] = useSearchParams();
  const args = readBoardArgs(params);
  const fill = readFill(params);
  const { data: board, error, isLoading, refetch } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Editing | null>(null);
  // The board of the shift on the clock (the same query as this page's when no other shift is asked): covers go from it
  const nowBoard = useGetAttendanceBoardQuery({}, livePoll);
  const current = nowBoard.data ? { shiftDate: nowBoard.data.shiftDate, shift: nowBoard.data.shift } : undefined;
  // The cover Drawer, with the shift it opens on and the Area of the row it was opened from; it picks its own shift
  const [cover, setCover] = useState<(CoverSlot & { areaId?: number }) | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const go = (next: BoardArgs) => setParams(toBoardParams(next));
  const shown = board ? searchRows(board.rows, search) : [];
  // Another search, shift or building starts again at the first page
  const pageKey = `${search}|${toBoardParams(args).toString()}`;
  const [paging, setPaging] = useState({ key: pageKey, page: 0 });
  const lastPage = Math.max(0, Math.ceil(shown.length / PAGE_SIZE) - 1);
  const page = paging.key === pageKey ? Math.min(paging.page, lastPage) : 0;
  const setPage = (next: number) => setPaging({ key: pageKey, page: next });

  // From an alert's "เติมเวลา…" link (plan 11 decision 9): the form opens on that Cleaner once the board is in
  const fillRow = fill && board ? board.rows.find((row) => attendanceTargets(row).some(({ cleaner }) => cleaner.id === fill.userId)) : undefined;

  // A form opened by hand belongs to the slot and Cleaner it was opened for: when the board under it changes, it closes
  const openRow = editing && board ? board.rows.find((row) => row.areaId === editing.areaId) : undefined;
  const canStillEdit = openRow && attendanceTargets(openRow).some(({ cleaner }) => cleaner.id === editing?.userId);
  if (
    editing &&
    board &&
    (board.shiftDate !== editing.shiftDate || board.shift !== editing.shift || !canStillEdit)
  ) {
    setEditing(null);
    setNotice({ kind: 'warn', text: 'กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน' });
  }
  const open: Editing | null =
    editing ??
    (fill && fillRow && board
      ? { areaId: fillRow.areaId, event: fill.event, shiftDate: board.shiftDate, shift: board.shift, userId: fill.userId }
      : null);
  const formRow = open && board ? board.rows.find((row) => row.areaId === open.areaId) : undefined;
  const formTarget = formRow && attendanceTargets(formRow).find(({ cleaner }) => cleaner.id === open?.userId);
  const openKey = open ? formKey(open) : null;
  const openKeyRef = useRef(openKey);
  useEffect(() => {
    openKeyRef.current = openKey;
  }, [openKey]);

  // Closes only the form the send belongs to: the Admin may have opened another since
  const closeForm = (key: string) => {
    setEditing((current) => (current && formKey(current) === key ? null : current));
    if (fill && openKeyRef.current === key) {
      go(args); // the link's fill and event go, so the form does not open again
    }
  };

  const openCover = (row?: BoardRow) => {
    setNotice(null);
    if (board) {
      setCover({ areaId: row?.areaId, shiftDate: board.shiftDate, shift: board.shift });
    }
  };

  const openCorrection = (row: BoardRow, userId: number) => {
    if (!board) return;
    setNotice(null);
    setEditing({ areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift, userId });
  };

  return {
    args, board, error, isLoading, current, search, setSearch, notice, shown, page, setPage, cover,
    fillTargetMissing: board && fill && !fillRow && !editing,
    open, openKey, formRow, formTarget, closeForm, openCover, openCorrection,
    onSlot: (shiftDate: string, shift: Shift) => go({ ...args, shiftDate, shift }),
    onBuilding: (buildingId?: number) => go({ ...args, buildingId }),
    retry: () => void refetch(),
    clearNotice: () => setNotice(null),
    closeCover: () => setCover(null),
    onCovered: (text: string) => {
      setNotice({ kind: 'ok', text });
      setCover(null);
    },
    onCorrected: (text: string, key: string) => {
      setNotice({ kind: 'ok', text });
      closeForm(key);
    },
  };
}
