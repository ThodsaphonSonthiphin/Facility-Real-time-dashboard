import { act, render, screen, waitFor, within } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { fakeServer, type FakeResponse } from '../../test/fakeServer';
import { attendanceGpsAlert } from '../../test/liveFixtures';
import { AdminNav } from './AdminNav';

describe('AdminNav alert count', () => {
  it.each([0, 9, 99, 999, 1000, 10000])('keeps the exact accessible count for %i open alerts', async (count) => {
    let respond!: (answer: FakeResponse) => void;
    fakeServer({ 'GET /api/admin/alerts': () => new Promise((resolve) => { respond = resolve; }) });
    render(<Provider store={makeStore()}><MemoryRouter><AdminNav /></MemoryRouter></Provider>);
    const link = screen.getByRole('link', { name: 'รายการที่ขึ้นเตือน' });
    expect(within(link).queryByTitle(String(count))).not.toBeInTheDocument();
    // The request remains pending until the test releases it, so the unknown state is exercised too.
    await screen.findByRole('navigation', { name: 'เมนูแอดมิน' });
    // RTK Query starts the request asynchronously.
    await waitFor(() => expect(respond).toBeDefined());
    await act(async () => {
      respond({ status: 200, body: Array.from({ length: count }, (_, i) => ({ ...attendanceGpsAlert, recordId: i + 1 })) });
    });
    if (count === 0) {
      await waitFor(() => expect(screen.queryByTitle('0')).not.toBeInTheDocument());
      expect(link).toHaveAccessibleName('รายการที่ขึ้นเตือน');
    } else {
      await screen.findByRole('link', { name: `รายการที่ขึ้นเตือน (${count})` });
      expect(within(link).getByTitle(String(count))).toHaveTextContent(count > 999 ? '999+' : String(count));
    }
  });
});
