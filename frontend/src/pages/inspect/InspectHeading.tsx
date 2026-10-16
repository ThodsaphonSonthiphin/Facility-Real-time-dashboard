import type { InspectionVisit } from './inspectApi';

/** The top of M6, M16 and the failure screens: where the point is, its name, and whether this is a cover. */
export function InspectHeading({ visit }: { visit: InspectionVisit }) {
  const { point } = visit;
  return (
    <header className="inspect-heading">
      <span className="inspect-kicker">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1>{point.name}</h1>
      {visit.isCover && <p className="inspect-sub">{`ตรวจแทนตึก ${point.buildingCode} ในกะนี้ (Admin มอบหมาย)`}</p>}
    </header>
  );
}
