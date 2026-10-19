import { formatDate } from '../../shared/utils/thaiTime';
import type { PrintSign } from './printRules';
import { QrCode } from './QrCode';

/** One sign of wireframe D5 (facility-0021 item 6): what it is, where, the QR, how to scan, and the full URL. */
export function SignCard({ sign }: { sign: PrintSign }) {
  return (
    <article className="print-sign" aria-label={`ป้าย ${sign.code}`}>
      <span className="print-sign__type">{sign.type}</span>
      <h2 className="print-sign__name">{sign.name}</h2>
      <span className="print-sign__place">{sign.place}</span>
      <QrCode text={sign.url} label={`QR ${sign.code}`} />
      <span className="print-sign__scan">สแกนด้วยกล้องมือถือ</span>
      <span className="print-sign__hint">{sign.hint}</span>
      <span className="print-sign__code">{`รหัสป้าย ${sign.code} · ออก QR ${formatDate(sign.issuedAt)}`}</span>
      <span className="print-sign__url">{sign.url}</span>
    </article>
  );
}
