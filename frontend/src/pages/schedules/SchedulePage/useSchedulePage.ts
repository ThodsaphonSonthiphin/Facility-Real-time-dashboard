import { skipToken } from '@reduxjs/toolkit/query/react';
import { useCallback, useState, useSyncExternalStore } from 'react';
import type { RootState } from '../../../store';
import { useAppDispatch, useAppSelector } from '../../../store/hooks';
import { selectHubStatus } from '../../../store/slices/hubSlice';
import { scheduleHubKey, schedulePoll, useGetManagedScheduleQuery, useGetMyScheduleQuery } from '../schedulesApi';
import {
  clockSlot,
  initialScheduleSelection,
  readMayBeStale,
  selectionAfterRead,
  type ScheduleRole,
  type ScheduleSelection,
} from '../scheduleRules';
import { selectionChosen } from '../scheduleViewSlice';
import type { OpenScheduleEditor, SchedulePageView } from './type';

/**
 * One schedule page's state, for the three roles that read a week (ADR facility-0180–0186, 0218; spec §4, §12).
 *
 * The week and filters come from `scheduleView`, which lives for this ongoing use only: first open is this week (or the
 * start week of an ongoing night shift), returning restores what was chosen, and a reload or a new login starts over
 * because nothing is written to storage. A restored filter is a selection, not an authority — the server answers with the
 * week it may show, and one it has nothing readable for is dropped here rather than kept as a blank page.
 *
 * The hook never decides authority and never widens a read: a Cleaner's read carries no filter at all, and a management
 * read sends only what the person chose.
 */
export function useSchedulePage(role: ScheduleRole): SchedulePageView {
  const dispatch = useAppDispatch();
  const [editor, setEditor] = useState<OpenScheduleEditor | null>(null);
  const phone = useSyncExternalStore(subscribeToPhoneWidth, isPhoneWidth);
  const restored = useAppSelector((state: RootState) => state.scheduleView.views[role]);
  const changedAt = useAppSelector((state: RootState) => state.scheduleView.changedAt);
  // A Supervisor's and an Admin's initial filters follow the shift on the clock; a Cleaner has none
  const requested = restored ?? initialScheduleSelection(role, clockSlot(new Date()));
  const fallback = withoutBuilding(requested);

  // The read the person asked for, and the unfiltered one it falls back to. Both hooks run and one is skipped: a hook
  // may not be called conditionally.
  const askedMine = useGetMyScheduleQuery(
    role === 'cleaner' ? { weekStart: requested.weekStart } : skipToken,
    schedulePoll,
  );
  const askedManaged = useGetManagedScheduleQuery(role === 'cleaner' ? skipToken : requested, schedulePoll);
  const fallbackManaged = useGetManagedScheduleQuery(
    role === 'cleaner' || requested.buildingId === undefined ? skipToken : fallback,
    schedulePoll,
  );

  const asked = role === 'cleaner' ? askedMine : askedManaged;
  // facility-0185: a restored filter the actor no longer holds answers with nothing readable, so it is dropped at read
  // time. Nothing is written to the slice: the person's own choice stays theirs, and the fallback read cannot drop
  // anything itself because it carries no building.
  const held = asked.currentData
    ? selectionAfterRead(requested, { availability: asked.currentData.availability, rowCount: asked.currentData.rows.length })
    : undefined;
  const filterDropped = held?.dropped === true;
  const read = filterDropped ? fallbackManaged : asked;
  const selection = filterDropped ? fallback : requested;

  const remember = useCallback(
    (next: ScheduleSelection) => dispatch(selectionChosen({ role, selection: next })),
    [dispatch, role],
  );

  const week = read.currentData;
  const latest = read.error ? undefined : week;
  const manage = role !== 'cleaner' && latest && !editor;
  const businessDate = clockSlot(new Date()).startDate;
  return {
    role,
    week,
    latest,
    phone,
    editor,
    openWeekly: () => {
      if (manage && latest.rows.some((row) => row.days.some((day) => day.edit?.canChange))) {
        setEditor({ kind: 'weekly', week: latest });
      }
    },
    openBaseline: (person) => {
      if (manage && person.cleanerKind === 'Regular' && person.days.some((day) => day.edit?.canChange)) {
        setEditor({ kind: 'baseline', week: latest, person });
      }
    },
    openDated: (person, day) => {
      if (manage && day.edit?.canChange && day.shift) setEditor({ kind: 'dated', person, day });
    },
    closeEditor: () => setEditor(null),
    dateAction: (day) => day.date < businessDate ? 'แก้ไขแผนย้อนหลัง' : 'แก้เฉพาะวันที่',
    selection,
    filterDropped,
    isLoading: read.isLoading,
    isFetching: read.isFetching,
    error: read.error,
    mayBeStale: readMayBeStale(changedAt, read),
    hubStatus: useAppSelector(selectHubStatus(scheduleHubKey(selection))),
    refetch: () => void read.refetch(),
    chooseWeek: (weekStart) => remember({ ...requested, weekStart }),
    chooseBuilding: (buildingId) => remember({ ...requested, buildingId }),
    chooseShift: (shift) => remember({ ...requested, shift }),
    goWeek: (weeks) => remember({ ...requested, weekStart: moveWeek(requested.weekStart, weeks) }),
  };
}

function withoutBuilding(selection: ScheduleSelection): ScheduleSelection {
  return selection.buildingId === undefined
    ? selection
    : { weekStart: selection.weekStart, shift: selection.shift };
}

/** A week moved by whole weeks. A week is a Monday, and 7 days always lands on the next Monday. */
function moveWeek(weekStart: string, weeks: number): string {
  const at = Date.parse(`${weekStart}T00:00:00Z`) + weeks * 7 * 86_400_000;
  return new Date(at).toISOString().slice(0, 10);
}

const phoneWidth = '(max-width: 639px)';
function isPhoneWidth(): boolean {
  return window.matchMedia?.(phoneWidth).matches ?? false;
}
function subscribeToPhoneWidth(listener: () => void) {
  const query = window.matchMedia?.(phoneWidth);
  query?.addEventListener('change', listener);
  return () => query?.removeEventListener('change', listener);
}
