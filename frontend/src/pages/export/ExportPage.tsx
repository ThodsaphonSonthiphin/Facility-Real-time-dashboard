import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { RangeFields } from '../../shared/components/RangeFields';
import { checkRange, lastSevenDays, rangeTexts, readBuildingId, readRange } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { thaiToday } from '../../shared/utils/thaiTime';
import { useExportExcelMutation } from './exportApi';
import { EXPORT_MAX_DAYS, exportSets, fileNameFor, setDetail, setTitle, type ExportSet } from './exportRules';
import './export.css';

type Notice = { kind: 'ok' | 'error'; text: string } | null;

/**
 * facility-0053, wireframe D8: the Admin chooses the sets, the range (1–31 shift dates) and the building, then
 * downloads one .xlsx. The form starts on the last 7 days, or on the range and building of the link that brought the
 * Admin here (D2, D7), and follows the address when it changes under the open page (the menu link). The sets are not
 * in the address and are kept. The export is logged by the server (facility-0051).
 */
export function ExportPage() {
  const [params] = useSearchParams();
  const asked = readRange(params);
  const askedBuilding = readBuildingId(params);
  const [sets, setSets] = useState<ExportSet[]>(['Attendance', 'Submissions']);
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(thaiToday())));
  const [buildingId, setBuildingId] = useState(askedBuilding);
  const [notice, setNotice] = useState<Notice>(null);
  // the form follows the address when it changes under the open page (menu link, Back)
  const addressKey = `${asked ? `${asked.from}_${asked.to}` : ''}|${askedBuilding ?? ''}`;
  const [seenAddress, setSeenAddress] = useState(addressKey);
  if (seenAddress !== addressKey) {
    setSeenAddress(addressKey);
    setTexts(rangeTexts(asked ?? lastSevenDays(thaiToday())));
    setBuildingId(askedBuilding);
    setNotice(null);
  }
  const [exportExcel, { isLoading: sending }] = useExportExcelMutation();
  const range = checkRange(texts, EXPORT_MAX_DAYS);

  // The sets keep the file's order, whatever order they are ticked in
  const toggle = (set: ExportSet) =>
    setSets((current) => exportSets.filter((each) => (each === set ? !current.includes(each) : current.includes(each))));

  const download = async () => {
    if (sets.length === 0) {
      setNotice({ kind: 'error', text: 'ต้องเลือกข้อมูลอย่างน้อย 1 ชุด' });
      return;
    }
    if (typeof range === 'string') {
      setNotice({ kind: 'error', text: range });
      return;
    }
    setNotice(null);
    try {
      const { fileName } = await exportExcel({ ...range, buildingId, sets }).unwrap();
      setNotice({ kind: 'ok', text: `ดาวน์โหลดแล้ว: ${fileName}` });
    } catch (failure) {
      setNotice({ kind: 'error', text: getErrorMessage(failure) });
    }
  };

  return (
    <main className="export-page">
      <header>
        <span className="app-muted">แอดมินเท่านั้น · ช่วงทดลองใช้ไฟล์ Excel แทนหน้ารายงาน</span>
        <h1>ส่งออก Excel</h1>
      </header>
      <div className="export-layout">
        <section className="app-panel export-panel" aria-labelledby="export-choose">
          <h2 id="export-choose">1. เลือกข้อมูล</h2>
          {exportSets.map((set) => (
            <label key={set} className="export-set">
              <input type="checkbox" checked={sets.includes(set)} onChange={() => toggle(set)} />
              <span className="export-set__text">
                <strong>{setTitle[set]}</strong>
                <span className="app-muted">{setDetail[set]}</span>
              </span>
            </label>
          ))}
          <h2>2. ช่วงวันและตึก</h2>
          <div className="export-range">
            <RangeFields value={texts} onChange={setTexts} />
            <BuildingSelect value={buildingId} onChange={setBuildingId} />
          </div>
          <p className="app-muted">เลือกได้ 1–31 วัน นับตามวันที่ของกะ</p>
        </section>
        <section className="app-panel export-panel" aria-labelledby="export-download">
          <h2 id="export-download">3. ดาวน์โหลด</h2>
          {typeof range === 'string' ? (
            <p className="app-muted">กรอกช่วงวันให้ถูกต้องก่อน</p>
          ) : (
            <div className="export-file">
              <strong>{fileNameFor(range)}</strong>
              <span className="app-muted">
                {sets.length === 0
                  ? 'ยังไม่ได้เลือกข้อมูล'
                  : `${sets.length} แผ่นงาน · ${sets.map((set) => setTitle[set]).join(' + ')}`}
              </span>
            </div>
          )}
          <button
            type="button"
            className="app-button app-button--primary"
            disabled={sending}
            onClick={() => void download()}
          >
            {sending ? 'กำลังสร้างไฟล์...' : 'ดาวน์โหลด .xlsx'}
          </button>
          {notice && (
            <p
              className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
              role={notice.kind === 'ok' ? 'status' : 'alert'}
            >
              {notice.text}
            </p>
          )}
          <div className="export-notes">
            <span>ไม่มีพิกัดตำแหน่งดิบ มีแค่ระยะห่างจากป้าย และผลในรัศมี / นอกรัศมี</span>
            <span>การส่งออกเก็บลงประวัติการแก้ไขของแอดมิน (ใคร เมื่อไร ชุดไหน ช่วงวันไหน)</span>
            <span>ไฟล์ที่ดาวน์โหลดแล้วอยู่นอกการลบตามระยะเวลาเก็บข้อมูล ห้ามส่งต่อนอกบริษัท (เรื่องคนเก็บไฟล์รอคุยกับ HR)</span>
          </div>
          <Link to="/dashboard">‹ กลับไปหน้าสถานะจุด</Link>
        </section>
      </div>
    </main>
  );
}
