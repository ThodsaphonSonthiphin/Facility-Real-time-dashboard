import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { useGetAttendanceBoardQuery } from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';
import { Ban, Clock, MapPin, TriangleAlert, type LucideIcon } from 'lucide-react';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { Segmented } from '../../shared/components/Segmented';
import { Tabs } from '../../shared/components/Tabs';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, formatWeekday, thaiToday } from '../../shared/utils/thaiTime';
import { AlertTable } from './AlertTable';
import {
  alertDay,
  alertTabHint,
  alertTabLabel,
  alertTabOf,
  alertTabs,
  coverSlots,
  filterAlerts,
  sameSlot,
  type AlertDay,
  type AlertTab,
  type CoverSlot,
  type ReviewTab,
} from './alertRules';
import { CoverForm } from '../../shared/components/CoverForm';
import { CoversThisShift } from './CoversThisShift';
import { EmptyAreas } from './EmptyAreas';
import { countClass, countTones, panelClass } from './alertStyles';

const tabIcon: Record<AlertTab, LucideIcon> = { blocked: Ban, far: MapPin, missing: Clock };

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
    picked && current && !sameSlot(picked, current) && slots.some((slot) => sameSlot(slot, picked))
      ? picked
      : undefined;
  const aheadBoard = useGetAttendanceBoardQuery(aheadSlot ?? skipToken, livePoll);
  const ahead = aheadSlot !== undefined;
  const board = ahead ? aheadBoard : nowBoard;
  // currentData: never the board of the shift picked before while the new one loads
  const boardNow = board.currentData;
  const [tab, setTab] = useState<AlertTab>('blocked');
  const [review, setReview] = useState<ReviewTab>('open');
  const [day, setDay] = useState<AlertDay>('today');
  const [notice, setNotice] = useState<Notice | null>(null);
  // The Area and the slot the cover form was opened for: a board of another shift closes it
  const [cover, setCover] = useState<{ areaId: number; shiftDate: string; shift: Shift } | null>(null);
  const today = thaiToday();
  const list = (alerts.data ?? []).filter((alert) => alertDay(alert, today) === day);
  const inTab = list.filter((alert) => alertTabOf(alert) === tab);
  const openInTab = inTab.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, { tab, review, day, today });
  // The form closes by itself once the Area has someone (a cover made here or elsewhere) or another shift is shown
  if (cover && boardNow && (boardNow.shiftDate !== cover.shiftDate || boardNow.shift !== cover.shift)) {
    setCover(null);
  }
  const coverRow = cover
    ? boardNow?.rows.find(
        (row) => row.areaId === cover.areaId && (ahead ? row.covers.length === 0 : row.state === 'Empty'),
      )
    : undefined;
  // Closes only the form the send belongs to: the Admin may have opened another since
  const closeCover = (areaId: number, shiftDate: string, shift: Shift) =>
    setCover((open) =>
      open && open.areaId === areaId && open.shiftDate === shiftDate && open.shift === shift ? null : open,
    );
  const yesterday = addDays(today, -1);

  return (
    <main className="flex flex-col gap-6 text-sm text-text">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">รายการที่ขึ้นเตือน</h1>
        {/* As tall as its words before the board comes, so nothing under it moves */}
        <span className="min-h-5 text-text-2">
          {`พื้นที่ที่ยังไม่มีคน คนทำแทน และการสแกนที่ต้องตาม${
            current
              ? ` · ${shiftLabel[current.shift]} ${formatWeekday(current.shiftDate)} ${formatShiftDate(current.shiftDate)}`
              : ''
          }`}
        </span>
      </header>
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
      <div className="flex flex-wrap items-stretch gap-6">
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
        {boardNow && <CoversThisShift board={boardNow} ahead={ahead} onNotice={setNotice} />}
      </div>
      <section className={panelClass} aria-labelledby="alerts-list">
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 pt-4">
          <h2 id="alerts-list" className="text-lg font-semibold">
            เตือนจากการสแกน
          </h2>
          <Segmented
            label="วัน"
            value={day}
            onChange={setDay}
            options={[
              { value: 'yesterday', label: `เมื่อวาน ${formatShiftDate(yesterday).slice(0, 5)}` },
              { value: 'today', label: `วันนี้ ${formatShiftDate(today).slice(0, 5)}` },
            ]}
          />
        </div>
        <div className="flex flex-wrap items-end justify-between gap-3 border-b border-border px-4">
          <Tabs
            label="ประเภทเตือน"
            idPrefix="alerts"
            ruled={false}
            value={tab}
            onChange={setTab}
            tabs={alertTabs.map((one) => {
              const Icon = tabIcon[one];
              const waiting = list.filter((alert) => alertTabOf(alert) === one && !alert.reviewed).length;
              return {
                id: one,
                label: (
                  <>
                    <Icon aria-hidden="true" className="size-4" />
                    {alertTabLabel[one]}
                    <span
                      title="ยังไม่ได้คุย"
                      className={`${countClass} min-h-5.5 min-w-5.5 px-1.5 ${waiting > 0 ? countTones.rework : countTones.idle}`}
                    >
                      {waiting}
                    </span>
                  </>
                ),
              };
            })}
          />
          <div className="mb-2">
            <Segmented
              label="สถานะการคุย"
              value={review}
              onChange={setReview}
              options={[
                { value: 'open', label: `ยังไม่ได้คุย (${openInTab})` },
                { value: 'reviewed', label: `คุยแล้ว (${inTab.length - openInTab})` },
                { value: 'all', label: 'ทั้งหมด' },
              ]}
            />
          </div>
        </div>
        <div className="flex gap-2 border-b border-border bg-surface-2 px-4 py-2.5 text-text-3">
          <TriangleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-text-2" />
          <span>{alertTabHint[tab]}</span>
        </div>
        <div role="tabpanel" id={`alerts-panel-${tab}`} aria-labelledby={`alerts-tab-${tab}`}>
          {alerts.isLoading && <PageLoading lines={3} />}
          {alerts.error && (
            <div className="p-4">
              <Note tone="error" onRetry={() => void alerts.refetch()}>
                {getErrorMessage(alerts.error)}
              </Note>
            </div>
          )}
          {alerts.data && <AlertTable alerts={shown} today={today} review={review} onNotice={setNotice} />}
        </div>
      </section>
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน</span>
        <span>รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า</span>
      </MoreInfo>
    </main>
  );
}
