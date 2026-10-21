import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { checkRange, lastSevenDays, rangeTexts } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { thaiToday } from '../../shared/utils/thaiTime';
import { HistoryControls } from './HistoryControls';
import { useGetAuditActorsQuery, useGetAuditLogInfiniteQuery, type HistoryArgs } from './historyApi';
import { HISTORY_MAX_DAYS, readHistoryArgs, searchEntries, toHistoryParams } from './historyRules';
import { HistoryTable } from './HistoryTable';
import './history.css';

/**
 * facility-0057 rule 2, wireframe D9: every Admin change, newest first, read-only; opening it is not logged. The
 * range, actor and type are in the address; with no range the server's last 7 dates apply. Older pages come on
 * request (plan 12 decision 7).
 */
export function HistoryPage() {
  const [params, setParams] = useSearchParams();
  const args = readHistoryArgs(params);
  const log = useGetAuditLogInfiniteQuery(args, freshOnOpen);
  const { data: actors } = useGetAuditActorsQuery(undefined, freshOnOpen);
  const asked = args.from && args.to ? { from: args.from, to: args.to } : undefined;
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(thaiToday())));
  const [problem, setProblem] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  // the fields follow the address when it changes under the open page (menu link, Back)
  const rangeKey = asked ? `${asked.from}_${asked.to}` : '';
  const [seenRange, setSeenRange] = useState(rangeKey);
  if (seenRange !== rangeKey) {
    setSeenRange(rangeKey);
    setTexts(rangeTexts(asked ?? lastSevenDays(thaiToday())));
    setProblem(null);
  }
  const entries = log.currentData?.pages.flatMap((page) => page.entries) ?? [];
  const shown = searchEntries(entries, search);
  const go = (next: HistoryArgs) => setParams(toHistoryParams(next));

  const show = () => {
    const range = checkRange(texts, HISTORY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    go({ ...args, ...range });
  };

  return (
    <main className="history-page">
      <header>
        <span className="app-muted">Admin เท่านั้น · อ่านอย่างเดียว</span>
        <h1>ประวัติการเปลี่ยนแปลงของ Admin</h1>
      </header>
      <HistoryControls
        args={args}
        actors={actors}
        texts={texts}
        onTexts={setTexts}
        onShow={show}
        onArgs={go}
        search={search}
        onSearch={setSearch}
      />
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
      {log.isFetching && !log.currentData && <p className="app-muted">กำลังโหลด...</p>}
      {log.error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(log.error)}
        </p>
      )}
      {log.currentData && (
        <>
          <HistoryTable
            entries={shown}
            emptyText={entries.length > 0 ? 'ไม่พบรายการที่ตรงกับที่ค้นหา' : 'ไม่มีประวัติในช่วงนี้'}
          />
          <div className="history-more">
            <span className="app-muted">
              {shown.length === entries.length
                ? `แสดง ${entries.length} รายการ`
                : `แสดง ${shown.length} จาก ${entries.length} รายการที่โหลดแล้ว`}
            </span>
            {log.hasNextPage && (
              <button
                type="button"
                className="app-button"
                disabled={log.isFetchingNextPage}
                onClick={() => void log.fetchNextPage()}
              >
                {log.isFetchingNextPage ? 'กำลังโหลด...' : 'โหลดรายการเก่ากว่านี้'}
              </button>
            )}
          </div>
        </>
      )}
      <p className="history-footnote">
        ไม่มีใครแก้หรือลบรายการในหน้านี้ได้ · เก็บนานเท่ากับข้อมูลที่ถูกแก้ · เบอร์โทรไม่แสดงทั้งก่อนและหลัง · การเปิดดูหน้านี้ไม่ถูกบันทึก
      </p>
    </main>
  );
}
