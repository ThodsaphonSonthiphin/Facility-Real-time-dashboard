import { encode } from 'uqr';

/**
 * facility-0021: the QR is drawn on this computer; no web service sees the token. Error correction M survives a
 * scratched sign. The quiet zone of 4 modules (the QR standard's) is part of the drawing. Each row's runs of dark
 * modules become one rectangle of the SVG path, in module units: `size` is the viewBox side.
 */
export function qrPath(text: string): { size: number; d: string } {
  const { data } = encode(text, { ecc: 'M', border: 4 });
  let d = '';
  data.forEach((row, y) => {
    let x = 0;
    while (x < row.length) {
      if (!row[x]) {
        x += 1;
        continue;
      }
      const start = x;
      while (x < row.length && row[x]) {
        x += 1;
      }
      d += `M${start} ${y}h${x - start}v1h-${x - start}z`;
    }
  });
  return { size: data.length, d };
}
