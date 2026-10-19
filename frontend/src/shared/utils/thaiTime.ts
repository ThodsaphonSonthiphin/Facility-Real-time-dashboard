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
