import { test, expect } from '../support/test.ts';
import { loginAsAdmin } from '../support/login.ts';
import { areaCode } from '../support/seed.ts';

declare global {
  interface Window {
    e2ePrints: number;
  }
}

interface AreaDetailAnswer {
  checkInSign: { code: string; qrToken: string };
  points: { isActive: boolean; sign: { code: string; qrToken: string } }[];
}

const mmToPx = 96 / 25.4;

// D5, facility-0021 item 6: what is printed is what the Area holds; each QR carries its own sign's address
test('printed signs match the Area, their QR codes read back their address and they fit an A4 sheet', async ({ page }, testInfo) => {
  // The system print dialog would stop a headed run; count the call instead
  await page.addInitScript(() => {
    window.e2ePrints = 0;
    window.print = () => {
      window.e2ePrints++;
    };
  });
  await loginAsAdmin(page);
  await page.getByRole('navigation', { name: 'เมนูแอดมิน' }).getByRole('link', { name: 'พิมพ์ป้าย', exact: true }).click();
  const picker = page.getByRole('combobox', { name: 'พื้นที่', exact: true });
  await expect(picker.locator('option', { hasText: `${areaCode} ` })).toHaveCount(1);
  const value = await picker.evaluate((select: HTMLSelectElement, code) =>
    [...select.options].find((option) => option.text.startsWith(`${code} `))?.value, areaCode);
  const detail = page.waitForResponse((response) => response.url().endsWith(`/api/admin/areas/${value}`));
  await picker.selectOption(value!);
  const area = (await (await detail).json()) as AreaDetailAnswer;

  const origin = new URL(page.url()).origin;
  const expected = [
    { code: area.checkInSign.code, url: `${origin}/check-in/${area.checkInSign.qrToken}` },
    ...area.points.filter((point) => point.isActive).map((point) => ({ code: point.sign.code, url: `${origin}/scan/${point.sign.qrToken}` })),
  ];
  await expect(page.getByRole('article')).toHaveCount(expected.length);
  for (const sign of expected) {
    await expect(page.getByRole('article', { name: `ป้าย ${sign.code}` })).toContainText(sign.url);
  }

  // Read every QR back from its drawing, as a phone camera would (no extra library: the browser's own detector)
  const decoded = await page.evaluate(async () => {
    const Detector = (window as unknown as { BarcodeDetector?: new (options: object) => { detect: (source: CanvasImageSource) => Promise<{ rawValue: string }[]> } }).BarcodeDetector;
    if (!Detector) return null;
    const detector = new Detector({ formats: ['qr_code'] });
    const found: Record<string, string> = {};
    for (const svg of document.querySelectorAll<SVGSVGElement>('article svg[role="img"]')) {
      // The colours come from classes, which do not travel with a copy: write them on it
      const copy = svg.cloneNode(true) as SVGSVGElement;
      copy.querySelector('rect')!.setAttribute('fill', getComputedStyle(svg.querySelector('rect')!).fill);
      copy.querySelector('path')!.setAttribute('fill', getComputedStyle(svg.querySelector('path')!).fill);
      copy.setAttribute('width', '400');
      copy.setAttribute('height', '400');
      const image = new Image();
      image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(new XMLSerializer().serializeToString(copy))}`;
      await image.decode();
      const canvas = document.createElement('canvas');
      canvas.width = canvas.height = 480; // a white margin around the code, as on paper
      const drawing = canvas.getContext('2d')!;
      drawing.fillStyle = '#fff';
      drawing.fillRect(0, 0, 480, 480);
      drawing.drawImage(image, 40, 40, 400, 400);
      found[svg.getAttribute('aria-label')!] = (await detector.detect(canvas))[0]?.rawValue ?? '';
    }
    return found;
  });
  if (decoded === null) {
    testInfo.annotations.push({ type: 'not checked', description: 'QR contents: this engine has no BarcodeDetector' });
  } else {
    expect(decoded).toEqual(Object.fromEntries(expected.map((sign) => [`QR ${sign.code}`, sign.url])));
  }

  // As printed: only the sheets, each A4, six small signs a sheet, nothing cut off inside a sign
  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('navigation', { name: 'เมนูแอดมิน' })).toBeHidden();
  await expect(page.getByRole('heading', { name: 'พิมพ์ป้าย', level: 1 })).toBeHidden();
  const sheets = page.getByRole('region', { name: /^แผ่นที่ \d+$/ });
  await expect(sheets).toHaveCount(Math.ceil(expected.length / 6));
  for (const sheet of await sheets.all()) {
    const box = (await sheet.boundingBox())!;
    expect(Math.abs(box.width - 210 * mmToPx)).toBeLessThan(2);
    expect(Math.abs(box.height - 297 * mmToPx)).toBeLessThan(2);
  }
  const cutOff = await page.evaluate(() =>
    [...document.querySelectorAll('article')]
      .filter((sign) => sign.scrollHeight > sign.clientHeight + 1 || sign.scrollWidth > sign.clientWidth + 1)
      .map((sign) => sign.getAttribute('aria-label')));
  expect(cutOff).toEqual([]);
  await testInfo.attach('as-printed', { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });

  await page.emulateMedia({ media: 'screen' });
  await page.getByRole('button', { name: 'พิมพ์', exact: true }).click();
  await expect.poll(() => page.evaluate(() => window.e2ePrints)).toBe(1);
});
