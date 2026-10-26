import { formatDate } from '../../shared/utils/thaiTime';
import type { PrintSign, SignSize } from './printRules';
import { QrCode } from './QrCode';

// The sign's measures are in millimetres, as printed before the move (facility-0087 rule 8); the spacing scale is in
// px, so no token fits them
const nameClass: Record<SignSize, string> = {
  small: 'text-sign-name font-bold',
  A5: 'text-sign-name-big font-bold',
};

const qrClass: Record<SignSize, string> = {
  small: 'size-[36mm] shrink-0',
  A5: 'size-[80mm] shrink-0',
};

/** One sign of wireframe D5 (facility-0021 item 6): what it is, where, the QR, how to scan, and the full URL. */
export function SignCard({ sign, size }: { sign: PrintSign; size: SignSize }) {
  return (
    <article
      className="flex flex-col items-center justify-center gap-[1.5mm] overflow-hidden border border-dashed border-sign-cut p-[4mm] text-center text-sign-ink"
      aria-label={`ป้าย ${sign.code}`}
    >
      <span className="text-sign-label font-semibold">{sign.type}</span>
      <h2 className={nameClass[size]}>{sign.name}</h2>
      <span className="text-sign-meta">{sign.place}</span>
      <QrCode text={sign.url} label={`QR ${sign.code}`} className={qrClass[size]} />
      <span className="text-sign-label font-semibold">สแกนด้วยกล้องมือถือ</span>
      <span className="text-sign-meta">{sign.hint}</span>
      <span className="text-sign-meta">{`รหัสป้าย ${sign.code} · สร้าง QR ${formatDate(sign.issuedAt)}`}</span>
      <span className="text-sign-url break-all text-sign-soft">{sign.url}</span>
    </article>
  );
}
