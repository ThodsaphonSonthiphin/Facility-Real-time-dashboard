import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { useGetAllBuildingsQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { checkRange, lastSevenDays, rangeDays, rangeTexts, readBuildingId, readRange } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { useExportExcelMutation } from './exportApi';
import { EXPORT_MAX_DAYS, exportSets, fileNameFor, setTitle, type ExportSet } from './exportRules';

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/** facility-0100: explicit empty selection never means all; old single-building URLs keep their request shape. */
export function useExportPage() {
  const [params] = useSearchParams();
  const asked = readRange(params);
  const askedBuilding = readBuildingId(params);
  const [sets, setSets] = useState<ExportSet[]>(['Attendance', 'Submissions']);
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(thaiToday())));
  const [buildingIds, setBuildingIds] = useState<number[] | null>(askedBuilding ? [askedBuilding] : null);
  const [legacyBuilding, setLegacyBuilding] = useState(askedBuilding);
  const [acknowledged, setAcknowledged] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const addressKey = `${asked ? `${asked.from}_${asked.to}` : ''}|${askedBuilding ?? ''}`;
  const [seenAddress, setSeenAddress] = useState(addressKey);
  if (seenAddress !== addressKey) {
    setSeenAddress(addressKey);
    setTexts(rangeTexts(asked ?? lastSevenDays(thaiToday())));
    setBuildingIds(askedBuilding ? [askedBuilding] : null);
    setLegacyBuilding(askedBuilding);
    setNotice(null);
  }
  const buildingQuery = useGetAllBuildingsQuery(undefined, freshOnOpen);
  const buildings = buildingQuery.data ?? [];
  const buildingsReady = !buildingQuery.isFetching && !buildingQuery.error && buildingQuery.data !== undefined;
  const selectedBuildings = buildings.filter((building) => buildingIds === null || buildingIds.includes(building.id));
  const missingBuilding = buildingsReady && buildingIds !== null && buildingIds.some((id) => !buildings.some((building) => building.id === id));
  const [exportExcel, { isLoading: sending }] = useExportExcelMutation();
  const range = checkRange(texts, EXPORT_MAX_DAYS);
  const days = typeof range === 'string' ? undefined : rangeDays(range);

  const toggleSet = (set: ExportSet) =>
    setSets((current) => exportSets.filter((each) => (each === set ? !current.includes(each) : current.includes(each))));
  const toggleBuilding = (id: number) => {
    setLegacyBuilding(undefined);
    setBuildingIds((current) => {
      const ids = current ?? buildings.map((building) => building.id);
      return ids.includes(id) ? ids.filter((each) => each !== id) : [...ids, id];
    });
  };
  const toggleAllBuildings = () => {
    setLegacyBuilding(undefined);
    setBuildingIds(buildingIds === null ? [] : null);
  };
  const download = async () => {
    const problem = sets.length === 0 ? 'ต้องเลือกข้อมูลอย่างน้อย 1 ชุด'
      : typeof range === 'string' ? range
      : !buildingsReady ? 'โหลดรายชื่อตึกให้สำเร็จก่อนดาวน์โหลด'
      : missingBuilding ? 'ไม่พบตึกที่เลือก กรุณาเลือกตึกใหม่'
      : selectedBuildings.length === 0 ? 'ต้องเลือกตึกอย่างน้อย 1 ตึก'
      : !acknowledged ? 'กรุณายืนยันการเก็บไฟล์ตามข้อกำหนดข้อมูลส่วนบุคคล' : undefined;
    if (problem || typeof range === 'string') {
      setNotice({ kind: 'error', text: problem ?? 'กรอกช่วงวันให้ถูกต้องก่อน' });
      return;
    }
    setNotice(null);
    try {
      const selector = legacyBuilding !== undefined ? { buildingId: legacyBuilding }
        : buildingIds === null ? {} : { buildingIds };
      const { fileName } = await exportExcel({ ...range, ...selector, sets }).unwrap();
      setNotice({ kind: 'ok', text: `ดาวน์โหลดแล้ว: ${fileName}` });
    } catch (failure) {
      setNotice({ kind: 'error', text: getErrorMessage(failure) });
    }
  };
  return {
    sets, texts, setTexts, toggleSet, buildingIds, buildings, selectedBuildings, missingBuilding,
    buildingsReady, buildingLoading: buildingQuery.isFetching, buildingError: buildingQuery.error,
    retryBuildings: () => void buildingQuery.refetch(), toggleBuilding, toggleAllBuildings,
    acknowledged, setAcknowledged, sending, notice, download, days,
    fileName: typeof range === 'string' ? undefined : fileNameFor(range),
    rangeSummary: typeof range === 'string' ? 'กรอกช่วงวันให้ถูกต้องก่อน'
      : `${formatShiftDate(range.from)}–${formatShiftDate(range.to)} (${days} วัน)`,
    setsSummary: sets.length ? sets.map((set) => setTitle[set]).join(' + ') : 'ยังไม่ได้เลือกข้อมูล',
    buildingsSummary: !buildingsReady ? 'รอรายชื่อตึก' : missingBuilding ? 'ไม่พบตึกที่เลือก'
      : buildingIds === null ? `ทุกตึก (${buildings.length})`
      : selectedBuildings.length ? selectedBuildings.map((building) => `${building.code} · ${building.name}`).join(', ') : 'ยังไม่ได้เลือกตึก',
  };
}
