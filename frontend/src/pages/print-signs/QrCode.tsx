import { useMemo } from 'react';
import { qrPath } from './qr';

/** The QR as SVG written by React: no HTML string, no image from elsewhere (facility-0066, 0021). */
export function QrCode({ text, label }: { text: string; label: string }) {
  const { size, d } = useMemo(() => qrPath(text), [text]);
  return (
    <svg className="print-qr" viewBox={`0 0 ${size} ${size}`} role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width={size} height={size} fill="#fff" />
      <path d={d} fill="#000" />
    </svg>
  );
}
