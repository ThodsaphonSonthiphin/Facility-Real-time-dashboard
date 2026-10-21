import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { PointStatus } from '../../shared/api/types';
import { sessionExpired, signedOut } from './authSlice';

/**
 * facility-0024 item 2: the Dashboard's filters live in a slice, so they are still set when the Admin comes back from
 * another page. buildingCode '' is every building; status 'all' is every status; issueOnly keeps the cards with an issue
 * the Cleaner reported (facility-0046). A session that ends forgets them.
 */
export interface DashboardState {
  buildingCode: string;
  status: PointStatus | 'all';
  issueOnly: boolean;
}

const initialState: DashboardState = { buildingCode: '', status: 'all', issueOnly: false };

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
  },
  extraReducers: (builder) => {
    builder.addCase(sessionExpired, () => initialState).addCase(signedOut, () => initialState);
  },
});

export const { buildingChosen, statusToggled, issueOnlyToggled } = dashboardSlice.actions;
export const dashboardReducer = dashboardSlice.reducer;
