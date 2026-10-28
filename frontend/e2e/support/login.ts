import { expect, type Page } from '@playwright/test';
import { admin, type Employee } from './seed.ts';

/** The phone login (wireframe M0). Does not check where it lands: a test that expects a refusal uses it too. */
export async function loginAsEmployee(page: Page, who: Pick<Employee, 'employeeId' | 'phone'>): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('รหัสพนักงาน').fill(who.employeeId);
  await page.getByLabel('เบอร์โทรศัพท์').fill(who.phone);
  await page.getByRole('button', { name: 'เข้าสู่ระบบ' }).click();
}

/** The Admin login; an Admin's home is D1 (facility-0058). */
export async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto('/login/admin');
  await page.getByLabel('ชื่อผู้ใช้').fill(admin.username);
  await page.getByLabel('รหัสผ่าน').fill(admin.password);
  await page.getByRole('button', { name: 'เข้าสู่ระบบ' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}
