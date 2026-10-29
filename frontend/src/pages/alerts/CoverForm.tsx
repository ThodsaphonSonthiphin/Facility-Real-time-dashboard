import { useState } from 'react';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { Field, Select } from '../../shared/components/Field';
import { Modal } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { useBuildingNames } from '../../shared/api/buildingNames';
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
      const cover = await assign({ userId, areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift }).unwrap();
      onDone(coverNotice(cover, names));
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <Modal labelledBy={headingId} onClose={() => !isLoading && onCancel()}>
      <div className="flex flex-col gap-2.5">
        <h2 id={headingId} tabIndex={-1} className="text-base font-bold">{`มอบหมายคนทำแทน · ${row.areaCode}`}</h2>
        <span>
          {row.cleaner
            ? `แม่บ้านประจำ: ${row.cleaner.displayName}${ahead ? '' : ' · ยังไม่ลงเวลา'}`
            : 'พื้นที่นี้ไม่มีแม่บ้านประจำกะนี้'}
        </span>
        {candidates.isLoading && <p className="text-text-2">กำลังโหลดรายชื่อ...</p>}
        {candidates.error && <Note tone="error">{getErrorMessage(candidates.error)}</Note>}
        {candidates.data && list.length === 0 && <Note tone="info">ไม่มีแม่บ้านกะนี้ที่ทำแทนได้</Note>}
        {list.length > 0 && (
          <Field label="ให้ใครทำแทน">
            <Select size="desk" value={userId} onChange={(event) => setChosen(Number(event.target.value))}>
              {list.map((candidate) => (
                <option key={candidate.id} value={candidate.id}>
                  {candidateText(candidate, ahead)}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <span className="text-text-2">
          {`มีผลเฉพาะ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)} · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก`}
        </span>
        <span className="text-text-2">
          แม่บ้านที่ทำแทนลงเวลาที่ป้ายของพื้นที่ตัวเองตามปกติ แล้วสแกนจุดของพื้นที่นี้ได้ · งานที่สแกนจะบันทึกว่า "ทำแทน"
        </span>
        {problem && <Note tone="error">{problem}</Note>}
        <div className="flex flex-wrap gap-2">
          <Button variant="primary" size="desk" disabled={userId == null || isLoading} onClick={() => void save()}>
            ยืนยันมอบหมาย
          </Button>
          <Button size="desk" disabled={isLoading} onClick={onCancel}>
            ยกเลิก
          </Button>
        </div>
      </div>
    </Modal>
  );
}
