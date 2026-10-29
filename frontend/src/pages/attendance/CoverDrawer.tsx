import { skipToken } from '@reduxjs/toolkit/query/react';
import { ArrowRightLeft } from 'lucide-react';
import { useState } from 'react';
import { useGetAttendanceBoardQuery, type BoardRow } from '../../shared/api/attendanceBoardApi';
import { freshOnOpen, livePoll } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { CandidateChoices, SearchBox } from '../../shared/components/CandidateChoices';
import { candidateFacts, choiceCardClass } from '../../shared/utils/coverCandidates';
import { Select } from '../../shared/components/Field';
import { DialogFooter, DialogHeader, Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { Segmented } from '../../shared/components/Segmented';
import { useBuildingNames } from '../../shared/api/buildingNames';
import { useAssignCoverMutation, useCancelCoverMutation, useGetCoverCandidatesQuery } from '../../shared/api/alertsApi';
import { coverNotice } from '../../shared/utils/coverText';
import { coverSlots, sameSlot, shiftEndText, type CoverSlot } from '../../shared/utils/coverSlots';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate, formatTime, formatWeekday } from '../../shared/utils/thaiTime';
import { searchRows } from './attendanceRules';

const tagClass =
  'inline-flex min-h-6 shrink-0 items-center rounded-sm border px-2 text-xs font-semibold whitespace-nowrap';
const tags = {
  warn: `${tagClass} border-overdue-border bg-surface text-overdue`,
  cover: `${tagClass} border-break-border bg-surface text-break`,
  plain: `${tagClass} border-idle-border bg-idle-bg text-idle`,
};

const shiftOptions: { value: Shift; label: string }[] = [
  { value: 'Day', label: 'เช้า 07:00–19:00' },
  { value: 'Night', label: 'ดึก 19:00–07:00' },
];

interface CoverDrawerProps {
  /** The shift on the clock: covers go from it to the Night shift 7 days on (facility-0075). */
  current: CoverSlot;
  /** The shift the drawer opens on, and the Area when it was opened from a row. */
  start: CoverSlot & { areaId?: number };
  onDone: (text: string) => void;
  onCancel: () => void;
}

/**
 * The design file's D2 "มอบหมายทำแทน" (ฟอร์มมอบหมายทำแทน), a 520 px Drawer: 1. the day and shift, 2. the Area, the ones
 * with nobody first, 3. the Cleaner, from the server's candidates; then a summary and the covers already given for
 * that shift. Same API and rules as D3 (facility-0041, 0075, 0084). An Area that already has a cover gets a second
 * one, never a replacement (owner, 2026-10-29): to change who, cancel the old one in the list first. The choices live
 * here only (facility-0024); one send at a time.
 */
export function CoverDrawer({ current, start, onDone, onCancel }: CoverDrawerProps) {
  const names = useBuildingNames();
  const slots = coverSlots(current);
  const dates = [...new Set(slots.map((slot) => slot.shiftDate))];
  const [slot, setSlot] = useState<CoverSlot>(() =>
    slots.some((one) => sameSlot(one, start)) ? { shiftDate: start.shiftDate, shift: start.shift } : current,
  );
  const [areaId, setAreaId] = useState<number | null>(start.areaId ?? null);
  const [chosen, setChosen] = useState<number | null>(null);
  const [areaSearch, setAreaSearch] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [assign, { isLoading }] = useAssignCoverMutation();
  const [cancelCover, cancelling] = useCancelCoverMutation();
  const ahead = !sameSlot(slot, current);
  const board = useGetAttendanceBoardQuery(slot, livePoll);
  const rows = board.currentData?.rows ?? [];
  const area = areaId == null ? undefined : rows.find((row) => row.areaId === areaId);
  const candidates = useGetCoverCandidatesQuery(area ? { areaId: area.areaId, ...slot } : skipToken, freshOnOpen);
  const list = candidates.currentData ?? [];
  const userId = chosen ?? list[0]?.id; // the server lists the best first
  const person = list.find((candidate) => candidate.id === userId);
  const covers = rows.flatMap((row) => row.covers.map((cover) => ({ row, cover })));
  const slotWords = `${shiftShortLabel[slot.shift]} ${formatWeekday(slot.shiftDate)} ${formatShiftDate(slot.shiftDate)}`;
  const headingId = 'attendance-cover';

  const pickSlot = (next: CoverSlot) => {
    // A Day shift already over on today's date is not offered: the date keeps its Night shift
    setSlot(slots.some((one) => sameSlot(one, next)) ? next : { ...next, shift: 'Night' });
    setAreaId(null);
    setChosen(null);
    setProblem(null);
  };

  const save = async () => {
    if (!area || userId == null) {
      return;
    }
    setProblem(null);
    try {
      const made = await assign({ userId, areaId: area.areaId, shiftDate: slot.shiftDate, shift: slot.shift }).unwrap();
      onDone(coverNotice(made, names));
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const cancelOne = async (id: number) => {
    setProblem(null);
    try {
      await cancelCover(id).unwrap();
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  return (
    <Drawer labelledBy={headingId} width="lg" onClose={() => !isLoading && onCancel()}>
      <DialogHeader
        id={headingId}
        title="มอบหมายทำแทน"
        detail="ให้แม่บ้านดูแลพื้นที่อื่นเพิ่ม เฉพาะกะที่เลือก"
        closeDisabled={isLoading}
        onClose={onCancel}
      />
      <div className="flex flex-col gap-6">
        <section className="flex flex-col gap-3" aria-labelledby="cover-step-1">
          <h3 id="cover-step-1" className="text-base font-bold">
            1. วันและกะ
          </h3>
          <div className="flex flex-wrap items-start gap-4">
            <label className="flex flex-col gap-1">
              <span className="font-semibold">วันที่</span>
              <Select
                size="desk"
                value={slot.shiftDate}
                disabled={isLoading}
                onChange={(event) => pickSlot({ shiftDate: event.target.value, shift: slot.shift })}
              >
                {dates.map((date) => (
                  <option key={date} value={date}>
                    {`${formatWeekday(date)} ${formatShiftDate(date)}${date === current.shiftDate ? ' (วันนี้)' : ''}`}
                  </option>
                ))}
              </Select>
              <span className="text-xs text-text-2">{`ล่วงหน้าได้ถึง ${formatShiftDate(dates[dates.length - 1])}`}</span>
            </label>
            <div className="flex flex-col gap-1">
              <span className="font-semibold">กะ</span>
              <Segmented
                label="กะ"
                value={slot.shift}
                disabled={isLoading}
                onChange={(shift) => pickSlot({ shiftDate: slot.shiftDate, shift })}
                options={shiftOptions}
              />
            </div>
          </div>
        </section>
        <section className="flex flex-col gap-2" aria-labelledby="cover-step-2">
          <h3 id="cover-step-2" className="text-base font-bold">
            2. พื้นที่ที่ต้องมีคนทำแทน
          </h3>
          <SearchBox label="ค้นหาพื้นที่หรือแม่บ้านประจำ" value={areaSearch} onChange={setAreaSearch} />
          {board.isFetching && !board.currentData && <PageLoading>กำลังโหลดพื้นที่...</PageLoading>}
          {board.error && <Note tone="error">{getErrorMessage(board.error)}</Note>}
          <div
            role="radiogroup"
            aria-labelledby="cover-step-2"
            className="flex max-h-72 flex-col gap-1.5 overflow-y-auto rounded-md border border-border p-1"
          >
            {searchRows(rows, areaSearch).map((row) => (
              <label key={row.areaId} className={`${choiceCardClass} flex-row! items-center! justify-between gap-2`}>
                <input
                  type="radio"
                  className="sr-only"
                  name="cover-area"
                  checked={areaId === row.areaId}
                  disabled={isLoading}
                  onChange={() => {
                    setAreaId(row.areaId);
                    setChosen(null);
                    setProblem(null);
                  }}
                />
                <span className="flex min-w-0 flex-col">
                  <span>
                    <strong>{row.areaCode}</strong> {row.areaName}
                  </span>
                  <span className="text-xs text-text-2">{areaLine(row)}</span>
                </span>
                <AreaTag row={row} ahead={ahead} />
              </label>
            ))}
            {board.currentData && searchRows(rows, areaSearch).length === 0 && (
              <span className="px-2 py-1.5 text-text-2">ไม่พบพื้นที่ที่ค้นหา</span>
            )}
          </div>
          <span className="text-xs text-text-2">
            {ahead ? 'กะนี้ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้' : 'พื้นที่ที่ยังไม่มีคนเข้างานขึ้นก่อน'}
          </span>
        </section>
        <section className="flex flex-col gap-2" aria-labelledby="cover-step-3">
          <h3 id="cover-step-3" className="text-base font-bold">
            3. แม่บ้านที่จะทำแทน
          </h3>
          {!area && <span className="text-text-2">เลือกพื้นที่ก่อน แล้วรายชื่อแม่บ้านที่ทำแทนได้จะขึ้นที่นี่</span>}
          {area && candidates.isFetching && !candidates.currentData && <PageLoading>กำลังโหลดรายชื่อ...</PageLoading>}
          {area && candidates.error && <Note tone="error">{getErrorMessage(candidates.error)}</Note>}
          {area && candidates.currentData && list.length === 0 && <Note tone="info">ไม่มีแม่บ้านกะนี้ที่ทำแทนได้</Note>}
          {area && list.length > 0 && (
            <CandidateChoices
              label="แม่บ้านที่จะทำแทน"
              candidates={list}
              value={userId}
              onChange={setChosen}
              disabled={isLoading}
              describe={(candidate) => {
                const facts = candidateFacts(candidate, rows, area, ahead);
                return {
                  own: facts.ownText,
                  tag:
                    facts.covering > 0 ? (
                      <span className={tags.cover}>{`ทำแทนอยู่ ${facts.covering} พื้นที่`}</span>
                    ) : facts.why.startsWith('ตึกเดียวกัน') ? (
                      <span className={tags.plain}>ตึกเดียวกัน</span>
                    ) : undefined,
                };
              }}
            />
          )}
          <span className="text-xs text-text-2">
            {ahead
              ? 'แสดงแม่บ้านกะนี้ที่ทำแทนได้ · กะยังไม่เริ่ม จึงเรียงตามรหัสพนักงาน'
              : 'แสดงแม่บ้านกะนี้ที่ทำแทนได้ · คนที่สแกนป้ายพื้นที่นี้แล้วและเข้างานก่อนขึ้นก่อน'}
          </span>
        </section>
        {area && area.covers.length > 0 && (
          <Note tone="warn" title={`${area.areaCode} มีคนทำแทนอยู่แล้ว`}>
            {`${area.covers.map((cover) => `${cover.displayName} ได้รับมอบหมายตั้งแต่ ${formatTime(cover.assignedAt)}`).join(' · ')} · ถ้ามอบหมายนี้ พื้นที่นี้จะมีคนทำแทน ${area.covers.length + 1} คน ถ้าจะเปลี่ยนคน ให้ยกเลิกของเดิมในรายการด้านล่างก่อน`}
          </Note>
        )}
        {area && person && (
          <div className="flex flex-col gap-2 rounded-md border border-pending-border bg-accent-subtle px-4 py-3">
            <strong className="flex items-center gap-1.5">
              <ArrowRightLeft aria-hidden="true" className="size-4 text-break" />
              สรุป
            </strong>
            <Summary
              name={person.displayName}
              ownCode={person.areaCode}
              rows={rows}
              userId={person.id}
              area={area}
              slotWords={slotWords}
            />
            <span className="text-xs text-text-3">{`สิทธิ์ทำแทนหมดเองเมื่อจบกะ ${shiftEndText[slot.shift]}`}</span>
          </div>
        )}
        {problem && <Note tone="error">{problem}</Note>}
        <section className="flex flex-col gap-2 border-t border-border pt-4" aria-labelledby="cover-given">
          <h3 id="cover-given" className="text-sm font-bold">
            {`ทำแทนที่มอบหมายไว้แล้ว${ahead ? slotWords : 'กะนี้'} · ${covers.length}`}
          </h3>
          {covers.length === 0 ? (
            <span className="text-text-2">ยังไม่มี</span>
          ) : (
            <ul className="m-0 flex list-none flex-col rounded-md border border-border p-0">
              {covers.map(({ row, cover }) => (
                <li
                  key={cover.id}
                  className="flex items-center justify-between gap-2 border-b border-border px-3 py-2 last:border-b-0"
                >
                  <span>
                    {`${row.areaCode} ${row.areaName} ← `}
                    <strong>{cover.displayName}</strong>
                    {` · ${shiftShortLabel[slot.shift]}`}
                  </span>
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode}`}
                    disabled={isLoading || cancelling.isLoading}
                    onClick={() => void cancelOne(cover.id)}
                  >
                    ยกเลิก
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
      <DialogFooter>
        <Button size="desk" disabled={isLoading} onClick={onCancel}>
          ยกเลิก
        </Button>
        <Button
          variant="primary"
          size="desk"
          disabled={!area || userId == null || isLoading}
          onClick={() => void save()}
        >
          มอบหมาย
        </Button>
      </DialogFooter>
    </Drawer>
  );
}

function areaLine(row: BoardRow): string {
  return [
    `ตึก ${row.buildingCode}`,
    row.cleaner ? `แม่บ้านประจำ ${row.cleaner.displayName}` : 'ไม่มีแม่บ้านประจำ',
  ].join(' · ');
}

/** Ahead nobody has checked in, so "ยังไม่มีคน" would say nothing: only a cover or a missing regular Cleaner is tagged. */
function AreaTag({ row, ahead }: { row: BoardRow; ahead: boolean }) {
  if (row.covers.length > 0) {
    return <span className={tags.cover}>มีคนทำแทนแล้ว</span>;
  }
  if (ahead) {
    return row.cleaner ? null : <span className={tags.warn}>ไม่มีแม่บ้านประจำ</span>;
  }
  return row.state === 'Empty' ? (
    <span className={tags.warn}>ยังไม่มีคน</span>
  ) : (
    <span className={tags.plain}>มีคนแล้ว</span>
  );
}

/** Who will look after what in that shift: their own Area, the covers they already have, and this one. */
function Summary({
  name,
  ownCode,
  rows,
  userId,
  area,
  slotWords,
}: {
  name: string;
  ownCode: string;
  rows: readonly BoardRow[];
  userId: number;
  area: BoardRow;
  slotWords: string;
}) {
  const own = rows.find((row) => row.areaCode === ownCode);
  const others = rows.filter(
    (row) => row.areaId !== area.areaId && row.covers.some((cover) => cover.userId === userId),
  );
  const lines = [
    `${own ? `${own.areaCode} ${own.areaName} · ตึก ${own.buildingCode}` : ownCode} (ของตัวเอง)`,
    ...others.map((row) => `${row.areaCode} ${row.areaName} · ตึก ${row.buildingCode} (ทำแทน)`),
    `${area.areaCode} ${area.areaName} · ตึก ${area.buildingCode} (ทำแทน)`,
  ];
  return (
    <>
      <span>
        <strong>{name}</strong>
        {` จะดูแล ${lines.length} พื้นที่ใน${slotWords}`}
      </span>
      <ul className="m-0 pl-5">
        {lines.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>
    </>
  );
}
