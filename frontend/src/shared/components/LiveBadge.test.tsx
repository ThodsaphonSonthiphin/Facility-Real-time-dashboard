import { act, render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { hubStatusChanged } from '../../store/slices/hubSlice';
import { LiveBadge } from './LiveBadge';

describe('LiveBadge (facility-0064 item 1)', () => {
  it('shows nothing while the first connection is made or once it is live, and the orange badge while it is down', () => {
    const store = makeStore();
    render(
      <Provider store={store}>
        <LiveBadge hubKey="test" />
      </Provider>,
    );

    expect(screen.getByRole('status')).toBeEmptyDOMElement(); // no status yet
    act(() => store.dispatch(hubStatusChanged({ key: 'test', status: 'connecting' })));
    expect(screen.getByRole('status')).toBeEmptyDOMElement(); // owner, 2026-10-28: it flashed on every open
    act(() => store.dispatch(hubStatusChanged({ key: 'test', status: 'reconnecting' })));
    expect(screen.getByRole('status')).toHaveTextContent('กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)');
    act(() => store.dispatch(hubStatusChanged({ key: 'test', status: 'live' })));
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });
});
