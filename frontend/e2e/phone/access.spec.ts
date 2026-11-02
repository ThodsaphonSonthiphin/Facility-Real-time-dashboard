import { test, expect } from '../support/test.ts';
import { loginAsEmployee } from '../support/login.ts';
import { cleanerOnShift, supervisor, checkInToken } from '../support/seed.ts';

test('unauthenticated Admin route asks for Admin login @smoke', async ({ page }) => {
  await page.goto('/admin/accounts');
  await expect(page).toHaveURL(/\/login\/admin$/);
  await expect(page.getByRole('button', { name: 'เข้าสู่ระบบ' })).toBeVisible();
});

test('Cleaner is denied Admin UI and Admin API', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);
  await page.goto('/admin/accounts');
  await expect(page.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeVisible();
  // Access token is intentionally only in memory. Capture a real authenticated fetch in the browser,
  // then replay its header to the Admin API to verify server authorization as well as the route guard.
  const me = page.waitForRequest((r) => r.url().endsWith('/api/my-work') && Boolean(r.headers().authorization));
  await page.getByRole('link', { name: 'กลับหน้าของฉัน' }).click();
  const request = await me;
  const response = await page.request.get('/api/admin/accounts', { headers: { authorization: request.headers().authorization } });
  expect(response.status()).toBe(403);
});

test('Supervisor cannot use Cleaner check-in or Admin pages', async ({ page }) => {
  await loginAsEmployee(page, supervisor);
  await expect(page).toHaveURL(/\/my-building$/);
  await page.goto(`/check-in/${checkInToken}`);
  await expect(page.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeVisible();
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeVisible();
});

test('invalid QR is refused with a readable error', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);
  await page.goto('/scan/e2e-nonexistent-sign');
  await expect(page.getByRole('heading', { name: /ป้าย|ไม่พบ/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'ส่งงาน' })).toHaveCount(0);
});

// facility-0106: the app page arrives with its browser security headers; the fixture fails any case the CSP blocks
test('the app is served with its security headers', async ({ page }) => {
  const response = await page.goto('/login');
  const headers = response!.headers();
  expect(headers['content-security-policy']).toContain("default-src 'self'");
  expect(headers['x-frame-options']).toBe('DENY');
  expect(headers['x-content-type-options']).toBe('nosniff');
  expect(headers['referrer-policy']).toBe('no-referrer');
  expect(headers['server']).toBeUndefined();
  await expect(page.getByRole('button', { name: 'เข้าสู่ระบบ' })).toBeVisible();
});
