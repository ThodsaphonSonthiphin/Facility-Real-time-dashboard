import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * facility-0064 item 1: whether the scan hub is connected is not server data, so it lives in a slice. connecting: not
 * live yet; live: pushes arrive; reconnecting: the connection dropped and is being tried again. Plan 11's Dashboard
 * badge reads this one too.
 */
export type HubStatus = 'connecting' | 'live' | 'reconnecting';

export interface HubState {
  status: HubStatus;
}

const initialState: HubState = { status: 'connecting' };

const hubSlice = createSlice({
  name: 'hub',
  initialState,
  reducers: {
    hubStatusChanged(state, action: PayloadAction<HubStatus>) {
      state.status = action.payload;
    },
  },
});

export const { hubStatusChanged } = hubSlice.actions;
export const hubReducer = hubSlice.reducer;
