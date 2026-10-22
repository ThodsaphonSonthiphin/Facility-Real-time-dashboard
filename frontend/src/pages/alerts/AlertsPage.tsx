import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { useGetAttendanceBoardQuery } from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AlertItem, type Notice } from './AlertItem';
import {
  alertGroupLabel,
  coverSlots,
  filterAlerts,
  sameSlot,
  type AlertGroup,
  type CoverSlot,
  type ReviewTab,
} from './alertRules';
import { CoverForm } from './CoverForm';
import { CoversThisShift } from './CoversThisShift';
import { EmptyAreas } from './EmptyAreas';
import './alerts.css';

const GROUPS: readonly AlertGroup[] = ['all', 'blocked', 'far', 'missing'];

/**
 * Wireframe D3: the Areas with nobody this shift and their covers (facility-0041, 0048), and the alerts of yesterday and
 * today for the Admin to talk through (facility-0035, 0037, 0051). A shift up to 7 days ahead can be picked, so covers
 * for the night and the weekend are given before the Admin leaves (facility-0075, 0084). The boards follow the hub like
 * the attendance page; no hub event carries alerts, so they are asked on every open, every 30 s and after each action.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const nowBoard = useGetAttendanceBoardQuery({}, livePoll);
  const [picked, setPicked] = useState<CoverSlot | null>(null);
  const current = nowBoard.data ? { shiftDate: nowBoard.data.shiftDate, shift: nowBoard.data.shift } : undefined;
  const slots = current ? coverSlots(current) : [];
  // A pick the clock has reached or passed shows the board of now again
  const aheadSlot =
    picked && current && !sameSlot(picked, current) && slots.some((slot) => sameSlot(slot, picked)) ? picked : undefined;
  const aheadBoard = useGetAttendanceBoardQuery(aheadSlot ?? skipToken, livePoll);
  const ahead = aheadSlot !== undefined;
  const board = ahead ? aheadBoard : nowBoard;
  // currentData: never the board of the shift picked before while the new one loads
  const boardNow = board.currentData;
  const [tab, setTab] = useState<ReviewTab>('open');
  const [group, setGroup] = useState<AlertGroup>('all');
  const [notice, setNotice] = useState<Notice | null>(null);
  // The Area and the slot the cover form was opened for: a board of another shift closes it
  const [cover, setCover] = useState<{ areaId: number; shiftDate: string; shift: Shift } | null>(null);
  const list = alerts.data ?? [];
  const openCount = list.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, tab, group);
  // The form closes by itself once the Area has someone (a cover made here or elsewhere) or another shift is shown
  if (cover && boardNow && (boardNow.shiftDate !== cover.shiftDate || boardNow.shift !== cover.shift)) {
    setCover(null);
  }
  const coverRow = cover
    ? boardNow?.rows.find((row) => row.areaId === cover.areaId && (ahead || row.state === 'Empty'))
    : undefined;
  // Closes only the form the send belongs to: the Admin may have opened another since
  const closeCover = (areaId: number, shiftDate: string, shift: Shift) =>
    setCover((open) => (open && open.areaId === areaId && open.shiftDate === shiftDate && open.shift === shift ? null : open));
  const tabs: { tab: ReviewTab; label: string }[] = [
    { tab: 'open', label: `ยังไม่ได้คุย (${openCount})` },
    { tab: 'reviewed', label: `คุยแล้ว (${list.length - openCount})` },
    { tab: 'all', label: 'ทั้งหมด' },
  ];

  return (
    <main className="alerts-page">
      <header>
        <span className="app-muted">เมื่อวานและวันนี้ · แอดมินเท่านั้น</span>
        <h1>รายการที่ขึ้นเตือน</h1>
      </header>
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      <EmptyAreas
        board={boardNow}
        error={board.error}
        slots={slots}
        slot={aheadSlot ?? current}
        ahead={ahead}
        onSlot={(slot) => {
          setNotice(null);
          setPicked(current && sameSlot(slot, current) ? null : slot);
        }}
        chosen={coverRow ? coverRow.areaId : null}
        onChoose={(areaId) => {
          setNotice(null);
          if (boardNow) {
            setCover({ areaId, shiftDate: boardNow.shiftDate, shift: boardNow.shift });
          }
        }}
      >
        {boardNow && coverRow && (
          <CoverForm
            key={`${boardNow.shiftDate}:${boardNow.shift}:${coverRow.areaId}`}
            board={boardNow}
            row={coverRow}
            ahead={ahead}
            onDone={(text) => {
              setNotice({ kind: 'ok', text });
              closeCover(coverRow.areaId, boardNow.shiftDate, boardNow.shift);
            }}
            onCancel={() => closeCover(coverRow.areaId, boardNow.shiftDate, boardNow.shift)}
          />
        )}
      </EmptyAreas>
      <section className="alerts-section" aria-labelledby="alerts-list">
        <h2 id="alerts-list">เตือนจากการสแกน</h2>
        <div className="alerts-filters">
          <div className="alerts-tabs" role="group" aria-label="การคุย">
            {tabs.map((item) => (
              <button
                key={item.tab}
                type="button"
                className="alerts-tab"
                aria-pressed={tab === item.tab}
                onClick={() => setTab(item.tab)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <label className="alerts-type">
            ประเภท
            <select className="app-input" value={group} onChange={(event) => setGroup(event.target.value as AlertGroup)}>
              {GROUPS.map((value) => (
                <option key={value} value={value}>
                  {alertGroupLabel[value]}
                </option>
              ))}
            </select>
          </label>
        </div>
        {alerts.isLoading && <p className="app-muted">กำลังโหลด...</p>}
        {alerts.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(alerts.error)}
          </p>
        )}
        {alerts.data && shown.length === 0 && <p className="app-note app-note--info">ไม่มีรายการ</p>}
        <ul className="alerts-list">
          {shown.map((alert) => (
            // The same record id can come with two kinds (a far Shift-In and its missing Shift-Out)
            <AlertItem key={`${alert.kind}-${alert.recordId}`} alert={alert} onNotice={setNotice} />
          ))}
        </ul>
        <p className="app-muted">
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </p>
      </section>
      {boardNow && <CoversThisShift board={boardNow} ahead={ahead} onNotice={setNotice} />}
    </main>
  );
}
