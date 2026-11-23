/** Thai wall-clock "HH:mm". en-GB keeps Arabic digits and a 24-hour clock whatever language the phone uses. */
const thaiClock = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Day/month/year with Arabic digits and the Gregorian year, in Thai time. */
const thaiDate = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** A moment from the server (UTC ISO ending in "Z") or from the phone, as Thai time. */
export function formatTime(moment: string | Date): string {
  return thaiClock.format(typeof moment === 'string' ? new Date(moment) : moment);
}

/** A moment as the Thai calendar date "16/10/2026" (e.g. when a QR was issued, on the printed sign). */
export function formatDate(moment: string | Date): string {
  return thaiDate.format(typeof moment === 'string' ? new Date(moment) : moment);
}

/** A shift date from the server ("2026-10-15", already a Thai calendar date) as "15/10/2026". */
export function formatShiftDate(date: string): string {
  const [year, month, day] = date.split('-');
  return `${day}/${month}/${year}`;
}

export const THAI_MONTHS = [
  'ม.ค.', 'ก.พ.', 'มี.ค.', 'เม.ย.', 'พ.ค.', 'มิ.ย.',
  'ก.ค.', 'ส.ค.', 'ก.ย.', 'ต.ค.', 'พ.ย.', 'ธ.ค.',
] as const;

/** A shift date ("2026-11-23") as a readable Thai Buddhist-year date, "23 พ.ย. 2569". */
export function formatThaiDate(date: string): string {
  const [year, month, day] = date.split('-').map(Number);
  return `${day} ${THAI_MONTHS[month - 1]} ${year + 543}`;
}

/** A shift date ("2026-11-23") as short day + Thai month, "23 พ.ย.". */
export function formatThaiShortDate(date: string): string {
  const [, month, day] = date.split('-').map(Number);
  return `${day} ${THAI_MONTHS[month - 1]}`;
}

const weekdays = ['อา.', 'จ.', 'อ.', 'พ.', 'พฤ.', 'ศ.', 'ส.'];

/** A shift date ("2026-10-16", already a Thai calendar date) as its short Thai weekday, "ศ." (D3's shift picker). */
export function formatWeekday(date: string): string {
  const [year, month, day] = date.split('-').map(Number);
  return weekdays[new Date(Date.UTC(year, month - 1, day)).getUTCDay()];
}

/** Today's Thai calendar date as the server writes a shift date, "2026-10-15" (to fill a date range). */
export function thaiToday(now: Date = new Date()): string {
  const [day, month, year] = formatDate(now).split('/');
  return `${year}-${month}-${day}`;
}

/** A moment as Thai "15/10 09:10", for lists that cover more than one day (the alert list, wireframe D3). */
export function formatDayTime(moment: string | Date): string {
  return `${formatDate(moment).slice(0, 5)} ${formatTime(moment)}`;
}

/** A date typed as "15/10/2026", as the server's "2026-10-15"; null when it is not a real date in that form. */
export function parseShiftDate(text: string): string | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text.trim());
  if (!match) {
    return null;
  }
  const [, day, month, year] = match;
  const iso = `${year}-${month}-${day}`;
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  return date.toISOString().slice(0, 10) === iso ? iso : null;
}

/** A Round Window time ("HH:mm:ss") is already Thai wall-clock (RoundWindowDto); only the seconds go. */
export function formatClock(time: string): string {
  return time.slice(0, 5);
}

export function formatRound(round: { start: string; end: string }): string {
  return `${formatClock(round.start)}–${formatClock(round.end)}`;
}

export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) {
    return `${rest} นาที`;
  }
  return rest === 0 ? `${hours} ชม.` : `${hours} ชม. ${rest} นาที`;
}
