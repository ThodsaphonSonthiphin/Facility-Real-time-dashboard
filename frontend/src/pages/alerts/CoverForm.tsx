import { useState } from 'react';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { Button } from '../../shared/components/Button';
import { DialogFooter, DialogHeader, Modal } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { CandidateChoices } from '../../shared/cover/CandidateChoices';
import { useAssignCoverMutation, useGetCoverCandidatesQuery } from '../../shared/cover/coverApi';
import { candidateFacts } from '../../shared/cover/coverCandidates';
import { coverNotice } from '../../shared/cover/coverText';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate, formatWeekday } from '../../shared/utils/thaiTime';

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
        detail={`${shiftShortLabel[board.shift]} ${formatWeekday(board.shiftDate)} ${formatShiftDate(board.shiftDate)} · ตึก ${row.buildingCode}`}
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
              : 'คนที่สแกนป้ายพื้นที่นี้ล่าสุดในกะที่เลือกจะอยู่บนสุด คนที่ยังไม่ได้สแกนจะเรียงตามเวลาเข้างาน'}
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
