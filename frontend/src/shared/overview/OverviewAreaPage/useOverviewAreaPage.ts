import { skipToken } from '@reduxjs/toolkit/query';
import { useSearchParams } from 'react-router';
import { freshOnOpen } from '../../api/baseApi';
import { getErrorMessage } from '../../utils/getErrorMessage';
import { shiftShortLabel } from '../../utils/labels';
import { formatDate, formatShiftDate, formatTime } from '../../utils/thaiTime';
import { overviewApi, overviewHubKey, useGetOverviewAreaQuery } from '../overviewApi';
import { overviewHref, readOverviewContext } from '../overviewContext';
import type { OverviewContext, OverviewView } from '../types';
import { useOverviewRecovering, useOverviewRefresh } from '../useOverviewRefresh';
import type { OverviewAreaPageModel } from './type';

function phaseWords({ phase, closeUtc }: OverviewContext): string | undefined {
  if (phase === 'receiving') {
    return closeUtc
      ? `กะสิ้นสุดแล้ว ยังรับข้อมูลถึง ${formatDate(closeUtc)} ${formatTime(closeUtc)}`
      : 'กะสิ้นสุดแล้ว ยังอยู่ในช่วงรับข้อมูล';
  }
  if (phase === 'closed') return 'ปิดรับข้อมูลของกะนี้แล้ว';
  if (phase === 'notStarted') return 'กะนี้ยังไม่เริ่ม';
}

export function useOverviewAreaPage(routeView: OverviewView): OverviewAreaPageModel {
  const [params] = useSearchParams();
  const parsed = readOverviewContext(params, true);
  const selection = parsed.ok ? parsed.context : undefined;
  const view = selection?.view ?? routeView;
  const wrongRoute = (view === 'attendance') !== (routeView === 'attendance');
  const args = selection?.areaId !== undefined && !wrongRoute
    ? {
        areaId: selection.areaId,
        shiftDate: selection.date,
        shift: selection.shift,
        buildingId: selection.building,
        group: selection.group,
        roundKey: selection.roundKey,
      }
    : skipToken;
  const { currentData: cached } = overviewApi.endpoints.getOverviewArea.useQueryState(args);
  const refresh = useOverviewRefresh(cached?.context);
  const recovering = useOverviewRecovering(args);
  const result = useGetOverviewAreaQuery(args, { ...freshOnOpen, ...refresh });
  // currentData never carries the previous Area or round under a newly navigated URL.
  const detail = result.currentData;
  const selectedOldRound = !!detail?.selectedRoundKey
    && detail.rounds.some((round) => round.roundKey === detail.selectedRoundKey);
  const readOnly = !detail?.context.isClockCurrent || detail.context.phase !== 'running' || selectedOldRound;
  const sections = detail?.availability;
  const visibleSections = sections && (view === 'attendance'
    ? [sections.entry]
    : [sections.work, sections.inspection, sections.issue, sections.history]);

  const backParams = new URLSearchParams(params);
  for (const key of ['source', 'areaId', 'view', 'roundKey']) backParams.delete(key);
  const back = readOverviewContext(backParams);
  const error = !parsed.ok ? parsed.message
    : wrongRoute ? 'มุมมองไม่ตรงกับหน้านี้'
    : result.error ? getErrorMessage(result.error, 'ไม่สามารถโหลดข้อมูลพื้นที่ที่เลือกได้')
    : undefined;

  return {
    detail,
    loading: args !== skipToken && result.isFetching && !detail,
    error,
    recovering,
    hubKey: args !== skipToken && detail && detail.context.phase !== 'closed' ? overviewHubKey(args) : undefined,
    readAtText: detail && formatTime(detail.context.readAtUtc),
    backHref: back.ok ? overviewHref(back.context) : '/admin/overview',
    readOnly,
    stateChanged: detail?.matchesGroup === false,
    partial: visibleSections?.some((section) => section.state === 'partial') ?? false,
    retry: () => { if (args !== skipToken) void result.refetch(); },
    canRetry: args !== skipToken,
    periodText: selection?.date && selection.shift
      ? `${formatShiftDate(selection.date)} · ${shiftShortLabel[selection.shift]}`
      : '',
    phaseText: detail && phaseWords(detail.context),
    view,
    selection,
  };
}
