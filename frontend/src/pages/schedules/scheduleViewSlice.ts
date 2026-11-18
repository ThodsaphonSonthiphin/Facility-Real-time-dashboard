import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { sessionExpired, signedOut } from '../../store/slices/authSlice';
import type { ScheduleRole, ScheduleSelection } from './scheduleRules';

/**
 * ADR facility-0218: the week and filters a person just looked at are remembered for the same ongoing use and no longer.
 * This is memory only — nothing is written to localStorage, sessionStorage, a cookie or the URL — so a reload, a new tab
 * and a new login all start from the initial defaults (0180, 0184, 0186). A session that ends forgets them the same way
 * the Dashboard's filters do (dashboardSlice).
 *
 * `changedAt` is the "the server says the plan moved" mark: a `ScheduleChanged` push sets it, and the page keeps its stale
 * notice until a read that STARTED after the mark SUCCEEDS. A request starting, a reconnect or a failed read is never
 * evidence that what is on screen is current (spec §12).
 */
export interface ScheduleViewState {
  /** One selection per role: an account has exactly one role, so this is one person's ongoing use. */
  views: Partial<Record<ScheduleRole, ScheduleSelection>>;
  /** Milliseconds since the epoch of the last pushed change, or null when nothing has been pushed this session. */
  changedAt: number | null;
}

const initialState: ScheduleViewState = { views: {}, changedAt: null };

const scheduleViewSlice = createSlice({
  name: 'scheduleView',
  initialState,
  reducers: {
    /** The person chose a week or a filter: it is what returning to the page restores (0181, 0185). */
    selectionChosen(state, action: PayloadAction<{ role: ScheduleRole; selection: ScheduleSelection }>) {
      state.views[action.payload.role] = action.payload.selection;
    },
    /** A `ScheduleChanged` push: what is on screen may no longer be current until a read after this succeeds. */
    scheduleChanged(state, action: PayloadAction<{ at: number }>) {
      state.changedAt = action.payload.at;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(sessionExpired, () => initialState).addCase(signedOut, () => initialState);
  },
});

export const { selectionChosen, scheduleChanged } = scheduleViewSlice.actions;
export const scheduleViewReducer = scheduleViewSlice.reducer;
