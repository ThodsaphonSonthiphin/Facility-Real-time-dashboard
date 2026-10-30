import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { addDays, checkRange, lastSevenDays, rangeTexts } from '../../shared/utils/dateRange';
import { thaiToday } from '../../shared/utils/thaiTime';
import { useGetAuditActorsQuery, useGetAuditLogInfiniteQuery, type HistoryArgs } from './historyApi';
import { HISTORY_MAX_DAYS, readHistoryArgs, toHistoryParams } from './historyRules';

/** URL filters own the query; a visible index selects one genuine cached cursor page. */
export function useHistoryPage() {
  const [params, setParams] = useSearchParams();
  const args = readHistoryArgs(params);
  const identity = toHistoryParams(args).toString();
  const log = useGetAuditLogInfiniteQuery(args, freshOnOpen);
  const actors = useGetAuditActorsQuery(undefined, freshOnOpen);
  const today = thaiToday();
  const asked = args.from && args.to ? { from: args.from, to: args.to } : undefined;
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(today)));
  const [problem, setProblem] = useState<string | null>(null);
  const [search, setSearch] = useState(args.q ?? '');
  const [custom, setCustom] = useState(false);
  const rangeKey = asked ? `${asked.from}_${asked.to}` : '';
  const [seenRange, setSeenRange] = useState(rangeKey);
  if (seenRange !== rangeKey) {
    setSeenRange(rangeKey);
    setTexts(rangeTexts(asked ?? lastSevenDays(today)));
    setProblem(null);
    setCustom(false);
  }
  const [seenSearch, setSeenSearch] = useState(args.q);
  if (seenSearch !== args.q) {
    setSeenSearch(args.q);
    setSearch(args.q ?? '');
  }
  const [paging, setPaging] = useState({ identity, index: 0 });
  if (paging.identity !== identity) setPaging({ identity, index: 0 });
  const pages = log.currentData?.pages ?? [];
  // A full final page can advertise a cursor whose response is empty. Keep the last real page visible.
  const lastRealPage = pages.reduce((last, page, index) => page.entries.length ? index : last, 0);
  const index = paging.identity === identity ? Math.min(paging.index, lastRealPage) : 0;
  const entries = pages[index]?.entries ?? [];
  const beforeCount = pages.slice(0, index).reduce((sum, page) => sum + page.entries.length, 0);
  const hasNext = Boolean(pages[index + 1]?.entries.length) || (index === pages.length - 1 && Boolean(log.hasNextPage));
  const go = (next: HistoryArgs) => setParams(toHistoryParams(next));
  const show = () => {
    const range = checkRange(texts, HISTORY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    const next = toHistoryParams({ ...args, ...range, q: search });
    if (next.toString() === params.toString()) {
      void log.refetch();
      return;
    }
    setParams(next);
  };
  const nextPage = async () => {
    if (log.isFetching || !hasNext) return;
    const nextIndex = index + 1;
    if (pages[nextIndex]?.entries.length) {
      setPaging({ identity, index: nextIndex });
      return;
    }
    try {
      const answer = await log.fetchNextPage().unwrap();
      if (answer.pages[nextIndex]?.entries.length) {
        // A late response to an old filter must never move the new filter's page.
        setPaging((current) => current.identity === identity ? { identity, index: nextIndex } : current);
      }
    } catch {
      // RTK exposes the error; retain this page so the same Next button can retry.
    }
  };
  const applied = asked ?? lastSevenDays(today);
  const preset = custom ? 'custom' : applied.to === today && applied.from === addDays(today, -6) ? 'week'
    : applied.to === today && applied.from === addDays(today, -29) ? '30' : 'custom';
  const selectPreset = (value: string) => {
    if (value === 'custom') {
      setCustom(true);
      return;
    }
    setCustom(false);
    setProblem(null);
    go({ ...args, from: value === '30' ? addDays(today, -29) : undefined, to: value === '30' ? today : undefined });
  };
  return {
    args, log, actors, texts, setTexts, problem, search, setSearch, show, go, preset, selectPreset,
    entries, hasNext, nextPage, hasPrevious: index > 0,
    previousPage: () => setPaging({ identity, index: Math.max(0, index - 1) }),
    rangeLabel: entries.length ? `แสดง ${beforeCount + 1}–${beforeCount + entries.length} รายการ` : 'แสดง 0 รายการ',
  };
}
