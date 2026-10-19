import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { livePoll } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AlertItem, type Notice } from './AlertItem';
import { alertGroupLabel, filterAlerts, type AlertGroup, type ReviewTab } from './alertRules';
import './alerts.css';

const GROUPS: readonly AlertGroup[] = ['all', 'blocked', 'far', 'missing'];

/**
 * Wireframe D3: the alerts of yesterday and today for the Admin to talk through (facility-0035, 0037, 0041, 0051).
 * No hub event carries alerts, so the list is asked on every open, every 30 s and after each action.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const [tab, setTab] = useState<ReviewTab>('open');
  const [group, setGroup] = useState<AlertGroup>('all');
  const [notice, setNotice] = useState<Notice | null>(null);
  const list = alerts.data ?? [];
  const openCount = list.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, tab, group);
  const tabs: { tab: ReviewTab; label: string }[] = [
    { tab: 'open', label: `ยังไม่ได้คุย (${openCount})` },
    { tab: 'reviewed', label: `คุยแล้ว (${list.length - openCount})` },
    { tab: 'all', label: 'ทั้งหมด' },
  ];

  return (
    <main className="alerts-page">
      <header>
        <span className="app-muted">เมื่อวานและวันนี้ · Admin เท่านั้น</span>
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
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติของ Admin
        </p>
      </section>
    </main>
  );
}
