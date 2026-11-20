import { Ban, Check, Clock, MapPin, TriangleAlert, type LucideIcon } from 'lucide-react';
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { livePoll } from '../../shared/api/baseApi';
import { AdminPageHeader } from '../../shared/components/AdminPageHeader/AdminPageHeader';
import { KpiCard } from '../../shared/components/KpiCard/KpiCard';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { Segmented } from '../../shared/components/Segmented';
import { Tabs } from '../../shared/components/Tabs';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { addDays } from '../../shared/utils/dateRange';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { AlertTable } from './AlertTable';
import {
  alertDay,
  alertTabHint,
  alertTabLabel,
  alertTabOf,
  alertTabs,
  filterAlerts,
  type AlertDay,
  type AlertTab,
  type ReviewTab,
} from './alertRules';
import { countClass, countTones, panelClass } from './alertStyles';

const tabIcon: Record<AlertTab, LucideIcon> = { blocked: Ban, far: MapPin, missing: Clock };

/**
 * Wireframe D3 (facility-0225): exception review queue of yesterday and today for the Admin to talk through
 * (facility-0035, 0037, 0051). Emergency cover assignment lives under /admin/schedules.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const [tab, setTab] = useState<AlertTab>('blocked');
  const [review, setReview] = useState<ReviewTab>('open');
  const [day, setDay] = useState<AlertDay>('today');
  const [notice, setNotice] = useState<Notice | null>(null);
  const today = thaiToday();
  const list = (alerts.data ?? []).filter((alert) => alertDay(alert, today) === day);
  const inTab = list.filter((alert) => alertTabOf(alert) === tab);
  const openInTab = inTab.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, { tab, review, day, today });
  const yesterday = addDays(today, -1);
  const openAlertsCount = alerts.data ? list.filter((alert) => !alert.reviewed).length : undefined;
  const reviewedAlertsCount = alerts.data ? list.filter((alert) => alert.reviewed).length : undefined;

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        title="รายการที่ขึ้นเตือน"
        subtitle="รายการสแกนที่ต้องติดตาม (สแกนผิดพื้นที่ ไกลป้าย และลืมลงเวลาเลิกงาน)"
      />
      <section aria-label="สรุปสถานะและเตือน" className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <KpiCard
          label={`เตือนที่ยังไม่ได้คุย (${day === 'today' ? 'วันนี้' : 'เมื่อวาน'})`}
          value={openAlertsCount ?? '–'}
          unit="รายการ"
          icon={TriangleAlert}
          tone="red"
        />
        <KpiCard
          label={`คุยแล้ว (${day === 'today' ? 'วันนี้' : 'เมื่อวาน'})`}
          value={reviewedAlertsCount ?? '–'}
          unit="รายการ"
          icon={Check}
          tone="green"
        />
      </section>
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
      <section className={panelClass} aria-labelledby="alerts-list">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3.5">
          <h2 id="alerts-list" className="text-base font-bold">
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
      </MoreInfo>
    </main>
  );
}
