import { PhoneHeader } from '../../shared/components/PhoneScreen';
import { formatRound } from '../../shared/utils/thaiTime';
import type { InspectionVisit } from './inspectApi';

/**
 * The header of M6, M16 and the failure screens (the design file's Inspect): "‹ ตึกของฉัน", where the point is, its
 * name and its round, and whether this is a cover.
 */
export function InspectHeading({ visit }: { visit: InspectionVisit }) {
  const { point } = visit;
  return (
    <PhoneHeader back={{ to: '/my-building', label: 'ตึกของฉัน' }}>
      <span className="text-sm text-text-2">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1 className="text-2xl font-bold">{point.name}</h1>
      {point.currentRound && <span className="tabular-nums">{`รอบ ${formatRound(point.currentRound)}`}</span>}
      {visit.isCover && (
        <p className="text-sm text-text-2">{`ตรวจแทนตึก ${point.buildingCode} ในกะนี้ (แอดมินมอบหมาย)`}</p>
      )}
    </PhoneHeader>
  );
}
