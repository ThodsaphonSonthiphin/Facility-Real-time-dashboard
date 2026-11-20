import { loginAsAdmin, loginAsEmployee } from '../support/login.ts';
import { openSchedule, type ScheduleWeek } from '../support/scheduling.ts';
import { schedulingFixture } from '../support/seed.ts';
import { expect, test } from '../support/test.ts';

test.skip(process.env.E2E_DATASET !== 'large', 'Requires the run-owned scheduling roster');

test('UX-01–03 focus, menu stacking and roster separators hold at mobile and desktop widths', async ({ page }, info) => {
  const fixture = schedulingFixture();
  await loginAsAdmin(page);
  await openSchedule(page, '/admin/schedules', fixture.planningWeek);

  const table = page.getByRole('table', { name: 'ตารางงานรายสัปดาห์' });
  await expect(table).toBeVisible();
  const rowCount = await table.locator('tbody tr').count();
  expect(rowCount).toBeGreaterThan(1);
  const separatedRows = await table.locator('tbody').evaluate((body) =>
    Array.from((body as HTMLTableSectionElement).rows).slice(1).map((row) => Array.from(row.cells).every((cell) => {
      const style = getComputedStyle(cell);
      return style.borderTopStyle === 'solid' && Number.parseFloat(style.borderTopWidth) >= 1;
    })),
  );
  expect(separatedRows.length).toBeGreaterThan(0);
  expect(separatedRows.every(Boolean)).toBe(true);

  for (const width of [320, 375]) {
    await page.setViewportSize({ width, height: 844 });
    const trigger = page.getByRole('button', { name: 'เมนูแอดมิน' });
    await trigger.click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    const drawer = page.getByRole('dialog', { name: 'เมนูแอดมิน' });
    await expect(drawer).toBeVisible();
    await drawer.evaluate((el) => Promise.all(el.getAnimations().map((animation) => animation.finished)));
    const historyLink = page.getByRole('navigation', { name: 'เมนูแอดมิน' })
      .getByRole('link', { name: 'ประวัติการแก้ไข', exact: true });
    const hitTest = await historyLink.evaluate((link) => {
      const table = document.querySelector('table[aria-label="ตารางงานรายสัปดาห์"]');
      const section = table?.closest('section');
      const header = table?.querySelector('thead');
      if (!table || !section || !header) return { overlapsTable: false, receivesPointer: false, panelIsAboveTable: false };
      const linkBounds = link.getBoundingClientRect();
      const headerBounds = header.getBoundingClientRect();
      const y = linkBounds.top + linkBounds.height / 2;
      // Exercise the real z-index collision without making this fixture's long editor toolbar a prerequisite.
      // A positioned ancestor with auto z-index preserves the table's sticky-cell stacking against the menu panel.
      section.style.position = 'relative';
      section.style.top = `${y - (headerBounds.top + headerBounds.height / 2)}px`;
      const bounds = link.getBoundingClientRect();
      const x = bounds.left + bounds.width / 2;
      const tableBounds = table.getBoundingClientRect();
      const hit = document.elementFromPoint(x, y);
      const stack = document.elementsFromPoint(x, y);
      const panelIndex = stack.findIndex((element) => link.contains(element));
      const tableIndex = stack.findIndex((element) => table.contains(element));
      return {
        overlapsTable: x >= tableBounds.left && x <= tableBounds.right && y >= tableBounds.top && y <= tableBounds.bottom,
        receivesPointer: hit === link || link.contains(hit),
        // A top-layer modal <dialog> renders background elements inert (tableIndex === -1) or strictly behind the drawer.
        panelIsAboveTable: panelIndex >= 0 && (tableIndex === -1 || panelIndex < tableIndex),
      };
    });
    expect(hitTest.overlapsTable).toBe(true);
    expect(hitTest.receivesPointer).toBe(true);
    expect(hitTest.panelIsAboveTable).toBe(true);
    await info.attach(`admin-menu-${width}px`, { body: await page.screenshot(), contentType: 'image/png' });
    await historyLink.click();
    await expect(page).toHaveURL(/\/admin\/history$/);
    await expect.poll(() => page.evaluate(() => (document.activeElement as HTMLElement | null)?.id)).toBe('main-content');
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);

    await page.goto('/admin/schedules');
    await openSchedule(page, '/admin/schedules', fixture.planningWeek);
    const scroller = table.locator('xpath=..');
    await expect(scroller).toHaveAttribute('role', 'region');
    await scroller.focus();

    let reachedFinalDate = false;
    for (let index = 0; index < 300; index += 1) {
      await page.keyboard.press('Tab');
      await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
      const focusedCell = await page.evaluate(() => {
        const active = document.activeElement;
        const cell = active?.closest('td');
        const region = active?.closest('[role="region"]');
        if (!region) return null;
        if (!cell) return { dateColumn: null, unobscured: true };
        const stickyName = region.querySelector('tbody th[scope="row"]');
        if (!stickyName) return { dateColumn: cell.cellIndex, unobscured: false };
        const activeBounds = active!.getBoundingClientRect();
        const regionBounds = region.getBoundingClientRect();
        const visibleLeft = regionBounds.left + stickyName.getBoundingClientRect().width;
        return {
          dateColumn: cell.cellIndex,
          unobscured: activeBounds.left >= visibleLeft - 1 && activeBounds.right <= regionBounds.right + 1,
        };
      });
      if (!focusedCell) break;
      if (focusedCell.dateColumn !== null) {
        expect(focusedCell.unobscured, `Focused date column ${focusedCell.dateColumn} at ${width}px`).toBe(true);
      }
      if (focusedCell.dateColumn === 7) {
        reachedFinalDate = true;
        break;
      }
    }
    expect(reachedFinalDate, `Tab reaches the final date at ${width}px`).toBe(true);

    await page.keyboard.press('Shift+Tab');
    await page.evaluate(() => new Promise<void>((resolve) => requestAnimationFrame(() => resolve())));
    const previousCell = await page.evaluate(() => {
      const active = document.activeElement;
      const cell = active?.closest('td');
      const region = active?.closest('[role="region"]');
      if (!region) return { inRegion: false, dateColumn: null, unobscured: true };
      if (!cell) return { inRegion: true, dateColumn: null, unobscured: true };
      const stickyName = region.querySelector('tbody th[scope="row"]');
      if (!stickyName) return { inRegion: true, dateColumn: cell.cellIndex, unobscured: false };
      const activeBounds = active!.getBoundingClientRect();
      const regionBounds = region.getBoundingClientRect();
      const visibleLeft = regionBounds.left + stickyName.getBoundingClientRect().width;
      return {
        inRegion: true,
        dateColumn: cell.cellIndex,
        unobscured: activeBounds.left >= visibleLeft - 1 && activeBounds.right <= regionBounds.right + 1,
      };
    });
    expect(previousCell.inRegion).toBe(true);
    if (previousCell.dateColumn !== null) expect(previousCell.unobscured).toBe(true);
    await page.keyboard.press('Tab');
    const returnedCell = await page.evaluate(() => (document.activeElement?.closest('td') as HTMLTableCellElement | null)?.cellIndex);
    expect(returnedCell).toBe(7);
    await info.attach(`roster-${width}px`, { body: await page.screenshot(), contentType: 'image/png' });
  }

  for (const width of [1024, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await expect(table).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
    const scroller = table.locator('xpath=..');
    const fits = await scroller.evaluate((element) => element.scrollWidth <= element.clientWidth + 1);
    expect(await scroller.getAttribute('tabindex')).toBe(fits ? null : '0');
    await info.attach(`roster-${width}px`, { body: await page.screenshot(), contentType: 'image/png' });
  }
});

test('UX-03 keeps focused Supervisor roster dates visible in a partial week', async ({ page }, info) => {
  const fixture = schedulingFixture();
  await page.setViewportSize({ width: 320, height: 844 });
  await loginAsEmployee(page, fixture.supervisorAB);
  await expect(page).toHaveURL(/\/my-building$/);

  // A dated Supervisor assignment can expose only part of a week. Keep the live fixture's rows and roles.
  await page.route((url) => url.pathname === '/api/schedules', async (route) => {
    const response = await route.fetch();
    const week = await response.json() as ScheduleWeek;
    const dates = new Set(week.rows.flatMap((row) => row.days.map((day) => day.date)).sort());
    const visibleDates = [...dates].slice(0, 2);
    const rows = week.rows.map((row) => ({
      ...row,
      days: row.days.filter((day) => visibleDates.includes(day.date)),
    }));
    await route.fulfill({ response, json: { ...week, rows } });
  });
  await openSchedule(page, '/schedule-management', fixture.planningWeek);

  const table = page.getByRole('table', { name: 'ตารางงานรายสัปดาห์' });
  await expect(table.getByRole('columnheader')).toHaveCount(3);
  const scroller = page.getByRole('region', { name: 'ตารางงานรายสัปดาห์' });
  const name = table.getByRole('rowheader').first();
  const dates = table.getByRole('row').nth(1).getByRole('button', { name: /^เลือกช่อง / });
  await expect(dates).toHaveCount(2);
  await scroller.focus();

  for (const date of await dates.all()) {
    await page.keyboard.press('Tab');
    await expect(date).toBeFocused();
    await expect.poll(async () => {
      const dateBounds = (await date.boundingBox())!;
      const nameBounds = (await name.boundingBox())!;
      const scrollBounds = (await scroller.boundingBox())!;
      return dateBounds.x >= nameBounds.x + nameBounds.width - 1
        && dateBounds.x + dateBounds.width <= scrollBounds.x + scrollBounds.width + 1;
    }, { message: 'The focused date fits beside the sticky name' }).toBe(true);
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(320);
  await info.attach('supervisor-partial-week-320px', { body: await page.screenshot(), contentType: 'image/png' });
});
