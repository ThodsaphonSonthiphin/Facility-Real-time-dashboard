import { expect, test } from '../support/test.ts';
import { loginAsEmployee } from '../support/login.ts';
import { cleanerOnShift, supervisor } from '../support/seed.ts';

test('a Cleaner lands on My Work', async ({ page }) => {
  await loginAsEmployee(page, cleanerOnShift());
  await expect(page).toHaveURL(/\/my-work$/);
  await expect(page.getByRole('heading', { name: 'งานของฉัน' })).toBeVisible();
});

test('a Supervisor lands on My Building', async ({ page }) => {
  await loginAsEmployee(page, supervisor);
  await expect(page).toHaveURL(/\/my-building$/);
});

test('a wrong phone number is refused', async ({ page }) => {
  await loginAsEmployee(page, { employeeId: cleanerOnShift().employeeId, phone: '0899999999' });
  await expect(page.getByText('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

// facility-0104: 5 failures lock that login name from this place; the next try is refused before the phone is checked.
// 6999999 belongs to no account (the seed uses 1/2, the factory 7/8/9), so the lock lands on nobody real.
test('repeated wrong logins lock that employee ID only', async ({ page }) => {
  const guess = { employeeId: '6999999', phone: '0899999999' };
  const answers: number[] = [];
  for (let attempt = 0; attempt < 6; attempt++) {
    const answer = page.waitForResponse((response) => response.url().endsWith('/api/auth/login'));
    await loginAsEmployee(page, guess);
    answers.push((await answer).status());
  }
  expect(answers).toEqual([401, 401, 401, 401, 401, 429]);
  await expect(page.getByText('กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);

  // The same place may still log in under another name
  await loginAsEmployee(page, supervisor);
  await expect(page).toHaveURL(/\/my-building$/);
});
