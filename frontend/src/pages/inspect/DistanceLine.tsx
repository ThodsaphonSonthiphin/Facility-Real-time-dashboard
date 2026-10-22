import type { InspectionVisit } from './inspectApi';

/**
 * facility-0037: the server compared this phone's fix with the sign. Outside the radius the inspection is flagged,
 * never blocked. A sign without coordinates gives no distance, so nothing is shown.
 */
export function DistanceLine({ visit }: { visit: InspectionVisit }) {
  const accuracy = `±${Math.round(visit.accuracyM)} ม.`;
  if (visit.withinRadius === true) {
    return (
      <p className="inspect-distance">
        <span>{`ห่างป้าย ${visit.distanceM} ม. · ${accuracy}`}</span>
        <span className="inspect-radius">อยู่ในรัศมี</span>
      </p>
    );
  }
  if (visit.withinRadius === false) {
    return (
      <p className="app-note app-note--warn">
        {`ห่างป้าย ${visit.distanceM} ม. · ${accuracy} · นอกรัศมี ผลตรวจจากตรงนี้จะขึ้นว่าไกลป้ายให้แอดมินเห็น`}
      </p>
    );
  }
  return null;
}
