import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { roundsText } from '../../shared/utils/labels';

/** Six signs of about 97 × 94 mm on an A4 sheet (facility-0021 item 6), or two A5 signs for a wall that needs them big. */
export type SignSize = 'small' | 'A5';

export const perSheet: Record<SignSize, number> = { small: 6, A5: 2 };

/** One printed sign (wireframe D5). */
export interface PrintSign {
  key: string;
  kind: 'check-in' | 'point';
  type: string;
  name: string;
  place: string;
  code: string;
  hint: string;
  url: string;
  issuedAt: string;
}

/** Plan 8: a check-in sign opens /check-in/<token>, a point's sign /scan/<token>, on this page's own origin (facility-0067). */
export function signUrl(kind: PrintSign['kind'], token: string): string {
  return `${window.location.origin}/${kind === 'check-in' ? 'check-in' : 'scan'}/${token}`;
}

/**
 * Every sign that can be scanned: the check-in sign and the active points of each active Area, in the D4 order.
 * With `signId`, only that sign.
 */
export function printableSigns(details: AreaDetail[], signId?: number): PrintSign[] {
  const signs = details
    .filter((detail) => detail.area.isActive)
    .flatMap((detail): PrintSign[] => {
      const { area, checkInSign } = detail;
      const place = `พื้นที่ ${area.code} · ตึก ${area.buildingCode}`;
      const checkIn: PrintSign = {
        key: `sign-${checkInSign.id}`,
        kind: 'check-in',
        type: 'ป้ายลงเวลา',
        name: area.name,
        place,
        code: checkInSign.code,
        hint: 'สแกนตอนเข้างาน ออกพัก กลับจากพัก และเลิกงาน',
        url: signUrl('check-in', checkInSign.qrToken),
        issuedAt: checkInSign.qrIssuedAt,
      };
      const points = detail.points
        .filter((point) => point.isActive)
        .map(
          (point): PrintSign => ({
            key: `sign-${point.sign.id}`,
            kind: 'point',
            type: 'จุดทำความสะอาด',
            name: point.name,
            place,
            code: point.sign.code,
            hint: point.roundWindows.length > 0 ? `รอบ ${roundsText(point.roundWindows)}` : 'สแกนหลังทำความสะอาดเสร็จ',
            url: signUrl('point', point.sign.qrToken),
            issuedAt: point.sign.qrIssuedAt,
          }),
        );
      return [checkIn, ...points];
    });
  return signId === undefined ? signs : signs.filter((sign) => sign.key === `sign-${signId}`);
}

export function sheetsOf<T>(items: T[], size: number): T[][] {
  const sheets: T[][] = [];
  for (let index = 0; index < items.length; index += size) {
    sheets.push(items.slice(index, index + size));
  }
  return sheets;
}

/** Wireframe D5: "ป้ายลงเวลา 1 + จุดทำความสะอาด 4 = 5 ป้าย · กระดาษ A4 แผ่นละ 6 ป้าย · 1 แผ่น · ตัดตามเส้นประ". */
export function summaryLine(signs: PrintSign[], size: SignSize): string {
  const checkIns = signs.filter((sign) => sign.kind === 'check-in').length;
  const sheets = Math.ceil(signs.length / perSheet[size]);
  return `ป้ายลงเวลา ${checkIns} + จุดทำความสะอาด ${signs.length - checkIns} = ${signs.length} ป้าย · กระดาษ A4 แผ่นละ ${perSheet[size]} ป้าย · ${sheets} แผ่น · ตัดตามเส้นประ`;
}

/** Why nothing is printed. */
export function nothingToPrint(details: AreaDetail[], signId: number | undefined): string {
  if (signId !== undefined) {
    return 'ป้ายนี้ปิดใช้งานหรือไม่มีแล้ว';
  }
  if (details.length === 1 && !details[0].area.isActive) {
    return 'พื้นที่นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์';
  }
  return 'ไม่มีป้ายให้พิมพ์';
}
