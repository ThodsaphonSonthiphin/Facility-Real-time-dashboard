import { test, expect, observeAdditionalContext } from '../support/test.ts';
import { loginAsEmployee } from '../support/login.ts';
import { phone } from '../support/devices.ts';
import { cleanerOnShift, thaiHour } from '../support/seed.ts';

test('Cleaner submission → Supervisor rework → Cleaner resubmission → passed inspection @dataset', async ({ page, browser }, testInfo) => {
  test.skip(process.env.E2E_DATASET === 'baseline', 'Requires generated full-shift point and assigned accounts');
  test.setTimeout(180_000);
  test.skip([6, 18].includes(thaiHour()) && new Date().getUTCMinutes() >= 58,
    'Near the shift boundary: the valid fixture round closes at06:59/18:59 during this multi-role workflow');
  const night = thaiHour() < 7 || thaiHour() >= 19;
  // All factory accounts use the original Day Cleaner's synthetic phone hash.
  const factoryPhone = cleanerOnShift(new Date('2026-10-31T05:00:00Z')).phone;
  const employeeId = night ? '7000001' : '7000000';
  const supervisorId = night ? '8000001' : '8000000';
  const sign = '/scan/e2e-point-1-1';
  await loginAsEmployee(page, { employeeId, phone: factoryPhone });
  await expect(page).toHaveURL(/\/my-work$/);
  await page.goto('/check-in/e2e-checkin-1');
  const attendance = page.waitForResponse((r) => r.url().endsWith('/api/attendance') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'เข้างาน', exact: true }).click();
  expect((await attendance).ok()).toBe(true);
  await page.goto(sign);
  await page.getByText('พบปัญหา', { exact: true }).click();
  await page.getByRole('button', { name: 'ส่งงาน', exact: true }).click();
  await expect(page.getByText('เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ')).toBeVisible();
  await page.getByLabel('หมายเหตุ').fill('ข้อมูลทดสอบ UI: พื้นเปียก');
  const submission = page.waitForResponse((r) => r.url().endsWith('/api/scan-records') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'ส่งงาน', exact: true }).click();
  expect((await submission).status()).toBe(201);
  await expect(page.getByRole('heading', { name: /^ส่งงานแล้ว/ })).toBeVisible();

  const supervisorContext = await browser.newContext({ ...phone, baseURL: testInfo.project.use.baseURL });
  await observeAdditionalContext(supervisorContext);
  try {
    const supervisorPage = await supervisorContext.newPage();
    await loginAsEmployee(supervisorPage, { employeeId: supervisorId, phone: factoryPhone });
    await expect(supervisorPage).toHaveURL(/\/my-building$/);
    await supervisorPage.goto(sign);
    await supervisorPage.getByText('ไม่สะอาด', { exact: true }).click();
    await supervisorPage.getByRole('button', { name: 'บันทึกผลตรวจ', exact: true }).click();
    await expect(supervisorPage.getByRole('alert')).toBeVisible();
    await supervisorPage.getByLabel('สิ่งที่ต้องแก้').fill('ข้อมูลทดสอบ UI: เช็ดพื้นให้แห้ง');
    await supervisorPage.getByRole('button', { name: 'บันทึกผลตรวจ', exact: true }).click();
    await expect(supervisorPage.getByRole('heading', { name: /^บันทึกผลตรวจแล้ว/ })).toBeVisible();
    await page.goto(sign);
    await expect(page.getByText('“ข้อมูลทดสอบ UI: เช็ดพื้นให้แห้ง”', { exact: true })).toBeVisible();
    await page.getByText('เรียบร้อย', { exact: true }).click();
    await page.getByRole('button', { name: 'ส่งงาน', exact: true }).click();
    await expect(page.getByRole('heading', { name: /^ส่งงานแก้แล้ว/ })).toBeVisible();
    await supervisorPage.goto(sign);
    await supervisorPage.getByText('สะอาด', { exact: true }).click();
    const inspected = supervisorPage.waitForResponse((r) => r.url().endsWith('/api/inspections') && r.request().method() === 'POST');
    await supervisorPage.getByRole('button', { name: 'บันทึกผลตรวจ', exact: true }).click();
    const result = await (await inspected).json() as { result: string; newPointStatus: string };
    expect(result.result).toBe('Passed');
    await expect(supervisorPage.getByText('ผล: สะอาด', { exact: true })).toBeVisible();
    await testInfo.attach('Supervisor-result', { body: await supervisorPage.screenshot(), contentType: 'image/png' });
    await page.goto('/my-work');
    await expect(page.getByText('ข้อมูลทดสอบ UI: เช็ดพื้นให้แห้ง')).toHaveCount(0);
  } finally {
    await supervisorContext.close();
  }
});
