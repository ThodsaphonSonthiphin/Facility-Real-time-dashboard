import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * facility-0064 item 1: whether a scan hub connection is live is not server data, so it lives in a slice. Each page's
 * connection (one per cache entry, #26) has its own key, so two followers at once never overwrite each other's status
 * (plan 11 decision 4). connecting: not live yet; live: pushes arrive; reconnecting: the connection dropped and is being
 * tried again.
 */
export type HubStatus = 'connecting' | 'live' | 'reconnecting';

export interface HubState {
  connections: Record<string, HubStatus>;
}

const initialState: HubState = { connections: {} };

const hubSlice = createSlice({
  name: 'hub',
  initialState,
  reducers: {
    hubStatusChanged(state, action: PayloadAction<{ key: string; status: HubStatus }>) {
      state.connections[action.payload.key] = action.payload.status;
    },
    /** The cache entry went and its connection stopped. */
    hubFollowEnded(state, action: PayloadAction<string>) {
      delete state.connections[action.payload];
    },
  },
});

export const { hubStatusChanged, hubFollowEnded } = hubSlice.actions;
export const hubReducer = hubSlice.reducer;

/** A connection that has no status yet, or has ended, counts as connecting. */
export function selectHubStatus(key: string) {
  return (state: { hub: HubState }): HubStatus => state.hub.connections[key] ?? 'connecting';
}
