import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { PointStatus } from '../../shared/api/types';
import { sessionExpired, signedOut } from './authSlice';

/**
 * facility-0024 item 2: the Dashboard's filters live in a slice, so they are still set when the Admin comes back from
 * another page. buildingCode '' is every building; status 'all' is every status; issueOnly keeps the cards with an issue
 * the Cleaner reported (facility-0046). search narrows the Areas, points and Cleaners by their words (the design file's
 * Dashboard); problemFirst puts the Areas that need someone first and folds the others into one line each. A session
 * that ends forgets them.
 */
export interface DashboardState {
  buildingCode: string;
  status: PointStatus | 'all';
  issueOnly: boolean;
  search: string;
  problemFirst: boolean;
}

const initialState: DashboardState = {
  buildingCode: '',
  status: 'all',
  issueOnly: false,
  search: '',
  problemFirst: true,
};

const dashboardSlice = createSlice({
  name: 'dashboard',
  initialState,
  reducers: {
    buildingChosen(state, action: PayloadAction<string>) {
      state.buildingCode = action.payload;
    },
    /** Pressing the chosen status again shows every status. */
    statusToggled(state, action: PayloadAction<PointStatus>) {
      state.status = state.status === action.payload ? 'all' : action.payload;
    },
    issueOnlyToggled(state) {
      state.issueOnly = !state.issueOnly;
    },
    searchChanged(state, action: PayloadAction<string>) {
      state.search = action.payload;
    },
    problemFirstToggled(state) {
      state.problemFirst = !state.problemFirst;
    },
    /** "ล้างตัวกรอง": every card again; the sort choice is not a filter and stays. */
    filtersCleared(state) {
      state.buildingCode = '';
      state.status = 'all';
      state.issueOnly = false;
      state.search = '';
    },
  },
  extraReducers: (builder) => {
    builder.addCase(sessionExpired, () => initialState).addCase(signedOut, () => initialState);
  },
});

export const { buildingChosen, statusToggled, issueOnlyToggled, searchChanged, problemFirstToggled, filtersCleared } =
  dashboardSlice.actions;
export const dashboardReducer = dashboardSlice.reducer;
