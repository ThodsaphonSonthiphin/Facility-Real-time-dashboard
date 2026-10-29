import { useState } from 'react';
import type { AttendanceBoard, BoardRow } from '../api/attendanceBoardApi';
import { freshOnOpen } from '../api/baseApi';
import { Button } from './Button';
import { CandidateChoices } from './CandidateChoices';
import { candidateFacts } from '../utils/coverCandidates';
import { DialogFooter, DialogHeader, Modal } from './Modal';
import { Note } from './Note';
import { PageLoading } from './PageState';
import { getErrorMessage } from '../utils/getErrorMessage';
import { shiftShortLabel } from '../utils/labels';
import { formatShiftDate, formatWeekday } from '../utils/thaiTime';
import { useBuildingNames } from '../api/buildingNames';
import { coverNotice } from '../utils/coverText';
import { useAssignCoverMutation, useGetCoverCandidatesQuery } from '../api/alertsApi';

interface CoverFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  /** A shift not started: nobody has checked in yet (facility-0084). */
  ahead: boolean;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * Wireframe D3 "มอบหมายคนทำแทน" (facility-0041), the design file's 480 px box: the server's candidates as cards, best
 * first, for the shift on screen, which may be up to 7 days ahead (facility-0075). The choice lives here only
 * (facility-0024); one send at a time.
 */
export function CoverForm({ board, row, ahead, onDone, onCancel }: CoverFormProps) {
  const names = useBuildingNames();
  const candidates = useGetCoverCandidatesQuery(
    { areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift },
    freshOnOpen,
  );
  const [chosen, setChosen] = useState<number | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [assign, { isLoading }] = useAssignCoverMutation();
  const list = candidates.data ?? [];
  const userId = chosen ?? list[0]?.id; // the server lists the best first
  const headingId = `alerts-cover-${row.areaId}`;

  const save = async () => {
    if (userId == null) {
      return;
    }
    setProblem(null);
    try {
      const cover = await assign({
        userId,
        areaId: row.areaId,
        shiftDate: board.shiftDate,
        shift: board.shift,
      }).unwrap();
      onDone(coverNotice(cover, names));
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <Modal labelledBy={headingId} onClose={() => !isLoading && onCancel()}>
      <DialogHeader
        id={headingId}
        title={`มอบหมายคนทำแทน ${row.areaCode} ${row.areaName}`}
        detail={`${shiftShortLabel[board.shift]} ${formatWeekday(board.shiftDate)} ${formatShiftDate(board.shiftDate)} · ตึก ${row.buildingCode} · หมดสิทธิ์เมื่อจบกะ`}
        closeDisabled={isLoading}
        onClose={onCancel}
      />
      <div className="flex flex-col gap-3">
        {candidates.isLoading && <PageLoading>กำลังโหลดรายชื่อ...</PageLoading>}
        {candidates.error && <Note tone="error">{getErrorMessage(candidates.error)}</Note>}
        {candidates.data && list.length === 0 && <Note tone="info">ไม่มีแม่บ้านกะนี้ที่ทำแทนได้</Note>}
        {list.length > 0 && (
          <CandidateChoices
            label="แม่บ้านที่ทำแทน"
            candidates={list}
            value={userId}
            onChange={setChosen}
            disabled={isLoading}
            describe={(candidate) => {
              const facts = candidateFacts(candidate, board.rows, row, ahead);
              return { own: facts.ownText, why: facts.why };
            }}
          />
        )}
        {list.length > 0 && (
          <span className="text-xs text-text-2">
            {ahead
              ? 'กะนี้ยังไม่เริ่ม ยังไม่มีใครลงเวลา จึงเรียงตามรหัสพนักงาน'
              : 'เรียงจาก: สแกนป้ายพื้นที่นี้แล้ว → ลงเวลาเข้างานก่อน'}
          </span>
        )}
        {problem && <Note tone="error">{problem}</Note>}
      </div>
      <DialogFooter>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
        <Button variant="primary" size="desk" disabled={userId == null || isLoading} onClick={() => void save()}>
          ยืนยันมอบหมาย
        </Button>
      </DialogFooter>
    </Modal>
  );
}
