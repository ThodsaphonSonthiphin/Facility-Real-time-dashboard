import type { InspectionVisit } from './inspectApi';

/** The top of M6, M16 and the failure screens: where the point is, its name, and whether this is a cover. */
export function InspectHeading({ visit }: { visit: InspectionVisit }) {
  const { point } = visit;
  return (
    <header className="flex flex-col gap-1">
      <span className="text-sm tracking-wide text-text-2">{`ตรวจงาน · ${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <h1 className="text-2xl font-bold">{point.name}</h1>
      {visit.isCover && <p className="text-sm text-text-2">{`ตรวจแทนตึก ${point.buildingCode} ในกะนี้ (แอดมินมอบหมาย)`}</p>}
    </header>
  );
}
