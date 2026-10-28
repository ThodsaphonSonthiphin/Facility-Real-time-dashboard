import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { selectHubStatus } from '../../store/slices/hubSlice';
import { followScanHub } from './scanHub';

// The SignalR file does not arrive: a new build replaced it, or the phone's link dropped while it was on its way
vi.mock('@microsoft/signalr', () => {
  throw new Error('Failed to fetch dynamically imported module');
});

describe('followScanHub when the SignalR file cannot be fetched', () => {
  it('shows the page as not live (it keeps polling), then clears its status when the entry goes', async () => {
    const store = makeStore();
    let remove: () => void = () => {};
    const cacheEntryRemoved = new Promise<void>((resolve) => {
      remove = resolve;
    });
    const done = followScanHub({
      key: 'test',
      cacheDataLoaded: Promise.resolve(),
      cacheEntryRemoved,
      dispatch: store.dispatch,
      accessToken: async () => 'token',
      onMissed: () => {},
    });

    await vi.waitFor(() => expect(selectHubStatus('test')(store.getState())).toBe('reconnecting'));
    remove();
    await done;

    expect(store.getState().hub.connections).toEqual({});
  });
});
