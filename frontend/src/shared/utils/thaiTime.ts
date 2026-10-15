/** Thai wall-clock "HH:mm". en-GB keeps Arabic digits and a 24-hour clock whatever language the phone uses. */
const thaiClock = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Bangkok',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** A moment from the server (UTC ISO ending in "Z") or from the phone, as Thai time. */
export function formatTime(moment: string | Date): string {
  return thaiClock.format(typeof moment === 'string' ? new Date(moment) : moment);
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
