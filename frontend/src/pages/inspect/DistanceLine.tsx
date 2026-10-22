import { Note } from '../../shared/components/Note';
import { boxClass } from '../../shared/components/styles';
import type { InspectionVisit } from './inspectApi';

/**
 * facility-0037: the server compared this phone's fix with the sign. Outside the radius the inspection is flagged,
 * never blocked. A sign without coordinates gives no distance, so nothing is shown.
 */
export function DistanceLine({ visit }: { visit: InspectionVisit }) {
  const accuracy = `±${Math.round(visit.accuracyM)} ม.`;
  if (visit.withinRadius === true) {
    return (
      <p className={`flex items-center justify-between gap-2 text-md text-muted ${boxClass}`}>
        <span>{`ห่างป้าย ${visit.distanceM} ม. · ${accuracy}`}</span>
        {/* Plain words, not a chip: green is only ever a Point Status (MASTER.md "Colour") */}
        <span className="font-semibold whitespace-nowrap text-text">อยู่ในรัศมี</span>
      </p>
    );
  }
  if (visit.withinRadius === false) {
    return (
      <Note tone="warn">
        {`ห่างป้าย ${visit.distanceM} ม. · ${accuracy} · นอกรัศมี ผลตรวจจากตรงนี้จะขึ้นว่าไกลป้ายให้แอดมินเห็น`}
      </Note>
    );
  }
  return null;
}
