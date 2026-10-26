import { useMemo } from 'react';
import { qrPath } from './qr';

/**
 * The QR as SVG written by React: no HTML string, no image from elsewhere (facility-0066, 0021). `className` gives its
 * printed size.
 */
export function QrCode({ text, label, className }: { text: string; label: string; className: string }) {
  const { size, d } = useMemo(() => qrPath(text), [text]);
  return (
    <svg
      className={className}
      viewBox={`0 0 ${size} ${size}`}
      role="img"
      aria-label={label}
      shapeRendering="crispEdges"
    >
      <rect width={size} height={size} className="fill-sign-paper" />
      <path d={d} className="fill-sign-ink" />
    </svg>
  );
}
