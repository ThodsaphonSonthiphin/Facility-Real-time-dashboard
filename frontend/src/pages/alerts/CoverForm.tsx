import { useState } from 'react';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { candidateText, coverNotice } from './alertRules';
import { useAssignCoverMutation, useGetCoverCandidatesQuery } from './alertsApi';

interface CoverFormProps {
  board: AttendanceBoard;
  row: BoardRow;
  /** A shift not started: nobody has checked in yet (facility-0084). */
  ahead: boolean;
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * Wireframe D3 "มอบหมายคนทำแทน" (facility-0041): the server's candidates, best first, for the shift on screen, which
 * may be up to 7 days ahead (facility-0075). The choice lives here only (facility-0024); one send at a time.
 */
export function CoverForm({ board, row, ahead, onDone, onCancel }: CoverFormProps) {
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
      const cover = await assign({ userId, areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift }).unwrap();
      onDone(coverNotice(cover));
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <section className="app-panel alerts-cover" aria-labelledby={headingId}>
      <h2 id={headingId}>{`มอบหมายคนทำแทน · ${row.areaCode}`}</h2>
      <span>
        {row.cleaner
          ? `แม่บ้านประจำ: ${row.cleaner.displayName}${ahead ? '' : ' · ยังไม่ลงเวลา'}`
          : 'Area นี้ไม่มีแม่บ้านประจำกะนี้'}
      </span>
      {candidates.isLoading && <p className="app-muted">กำลังโหลดรายชื่อ...</p>}
      {candidates.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(candidates.error)}
        </p>
      )}
      {candidates.data && list.length === 0 && <p className="app-note app-note--info">ไม่มีแม่บ้านกะนี้ที่ทำแทนได้</p>}
      {list.length > 0 && (
        <label className="app-field">
          ให้ใครทำแทน
          <select className="app-input" value={userId} onChange={(event) => setChosen(Number(event.target.value))}>
            {list.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidateText(candidate, ahead)}
              </option>
            ))}
          </select>
        </label>
      )}
      <span className="app-field__help">
        {`มีผลเฉพาะ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)} · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก`}
      </span>
      <span className="app-field__help">
        แม่บ้านที่ทำแทนลงเวลาที่ป้าย Area ของตัวเองตามปกติ แล้วสแกนจุดของ Area นี้ได้ · งานที่สแกนจะบันทึกว่า "ทำแทน"
      </span>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      <div className="app-confirm__buttons">
        <button
          type="button"
          className="app-button app-button--primary"
          disabled={userId == null || isLoading}
          onClick={() => void save()}
        >
          ยืนยันมอบหมาย
        </button>
        <button type="button" className="app-button" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </button>
      </div>
    </section>
  );
}
