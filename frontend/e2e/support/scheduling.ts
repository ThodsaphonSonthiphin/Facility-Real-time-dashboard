import { expect, type APIRequestContext, type Locator, type Page, type Response, type TestInfo } from '@playwright/test';
import { sidewaysOverflow } from './pageWatch.ts';
import { formatShiftDate, formatWeekday } from '../../src/shared/utils/thaiTime.ts';

const scheduleLinks = {
  '/my-schedule': 'ตารางของฉัน',
  '/schedule-management': 'จัดตารางงาน',
  '/admin/schedules': 'ตารางงาน',
} as const;
type ScheduleRoute = keyof typeof scheduleLinks;

export interface ScheduleWeek {
  weekStart: string;
  rows: { userId: number; weeklyRevision: string; days: { date: string; isDayOff: boolean; area?: { value: number | null }; edit?: { weeklyOff: boolean } }[] }[];
  relief: { userId: number }[];
}

export interface CommandResult {
  outcome: string;
  problems: { code: string }[];
}

export const scheduleCommandPath = '/api/schedules/commands';

export function apiAnswer(response: Response, path: string, method = 'GET'): boolean {
  return new URL(response.url()).pathname === path && response.request().method() === method;
}

/** Select with the actual week controls, and read the HTTP answer that supplied the page. */
export async function openSchedule(page: Page, route: ScheduleRoute, weekStart: string): Promise<ScheduleWeek> {
  const api = route === '/my-schedule' ? '/api/schedules/me' : '/api/schedules';
  const initial = page.waitForResponse((response) => apiAnswer(response, api));
  if (new URL(page.url()).pathname === route) {
    await page.getByRole('button', { name: 'อัปเดต', exact: true }).click();
  } else {
    const link = route === '/my-schedule'
      ? page.getByRole('navigation', { name: 'เมนูหลักแม่บ้าน' }).getByRole('link', { name: scheduleLinks[route], exact: true })
      : page.getByRole('link', { name: scheduleLinks[route], exact: true });
    await link.click();
    await expect(page).toHaveURL((url) => url.pathname === route);
  }
  let response = await initial;
  expect(response.ok(), `Opening ${route} returned HTTP ${response.status()}`).toBe(true);
  let week = await response.json() as ScheduleWeek;
  while (week.weekStart !== weekStart) {
    const backward = weekStart < week.weekStart;
    const next = new Date(Date.parse(week.weekStart) + (backward ? -7 : 7) * 86_400_000).toISOString().slice(0, 10);
    const read = page.waitForResponse((reply) =>
      apiAnswer(reply, api) && new URL(reply.url()).searchParams.get('weekStart') === next);
    await page.getByRole('button', { name: backward ? 'สัปดาห์ก่อนหน้า' : 'สัปดาห์ถัดไป' }).click();
    response = await read;
    expect(response.ok()).toBe(true);
    week = await response.json() as ScheduleWeek;
    expect(week.weekStart).toBe(next);
  }
  return week;
}

export async function saveReviewedSchedule(page: Page): Promise<CommandResult> {
  const saved = page.waitForResponse((response) => apiAnswer(response, scheduleCommandPath, 'POST'));
  await page.getByRole('button', { name: 'บันทึก', exact: true }).click();
  const answer = await saved;
  expect(answer.ok(), `Scheduling command returned HTTP ${answer.status()}`).toBe(true);
  const result = await answer.json() as CommandResult;
  expect(result.outcome).toBe('committed');
  await expect(page.getByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  return result;
}

export function weekDayLabel(date: string): string {
  return `${formatWeekday(date)} ${formatShiftDate(date).slice(0, 5)}`;
}

export async function captureSchedule(page: Page, info: TestInfo, name: string, state?: Locator): Promise<void> {
  if (state) {
    // A sticky action bar can cover a status that still counts as intersecting the viewport.
    await state.evaluate((element) => element.scrollIntoView({ block: 'center' }));
    await expect(state).toBeInViewport({ ratio: 1 });
    expect(await state.evaluate((element) => {
      const { x, y, width, height } = element.getBoundingClientRect();
      return element.contains(document.elementFromPoint(x + width / 2, y + height / 2));
    })).toBe(true);
  }
  expect(await sidewaysOverflow(page)).toBe(0);
  const width = page.viewportSize()!.width;
  const screenshot = await page.screenshot({ path: info.outputPath(`${name}-${width}.png`), animations: 'disabled' });
  await info.attach(`${name}-${width}`, { body: screenshot, contentType: 'image/png' });
}

export async function authenticatedRead<T>(request: APIRequestContext, path: string, accessToken: string): Promise<T> {
  const response = await request.get(path, { headers: { Authorization: `Bearer ${accessToken}` } });
  expect(response.ok(), `Independent read of ${path.split('?')[0]} returned HTTP ${response.status()}`).toBe(true);
  return response.json() as Promise<T>;
}

export async function observeLogin(page: Page, login: () => Promise<void>): Promise<string> {
  const received = page.waitForResponse((response) => apiAnswer(response, '/api/auth/login', 'POST'));
  await login();
  const response = await received;
  expect(response.ok()).toBe(true);
  const { accessToken } = await response.json() as { accessToken: string };
  return accessToken;
}
