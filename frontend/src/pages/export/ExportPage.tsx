import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { RangeFields } from '../../shared/components/RangeFields';
import { boxClass, focusRing, linkClass } from '../../shared/components/styles';
import { checkRange, lastSevenDays, rangeTexts, readBuildingId, readRange } from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { thaiToday } from '../../shared/utils/thaiTime';
import { useExportExcelMutation } from './exportApi';
import { EXPORT_MAX_DAYS, exportSets, fileNameFor, setDetail, setTitle, type ExportSet } from './exportRules';

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
    <main className="flex flex-col gap-4 text-sm text-text">
      <header className="flex flex-col gap-0.5">
        <span className="text-muted">แอดมินเท่านั้น · ช่วงทดลองใช้ไฟล์ Excel แทนหน้ารายงาน</span>
        <h1 className="text-xl font-bold">ส่งออก Excel</h1>
      </header>
      {/* The choices take the wider column, the download the narrower, as in the wireframe; no token describes the
          1.4 : 1 track list */}
      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <section className={sectionClass} aria-labelledby="export-choose">
          <h2 id="export-choose" className={titleClass}>
            1. เลือกข้อมูล
          </h2>
          {exportSets.map((set) => (
            <label
              key={set}
              className="flex cursor-pointer items-start gap-3 rounded border border-border px-3.5 py-3 has-checked:border-primary"
            >
              <input
                type="checkbox"
                className={`mt-0.5 size-5 shrink-0 accent-primary ${focusRing}`}
                checked={sets.includes(set)}
                onChange={() => toggle(set)}
              />
              <span className="flex flex-col gap-0.5">
                <strong>{setTitle[set]}</strong>
                <span className="text-muted">{setDetail[set]}</span>
              </span>
            </label>
          ))}
          <h2 className={titleClass}>2. ช่วงวันและตึก</h2>
          <div className="flex flex-wrap items-end gap-3">
            <RangeFields value={texts} onChange={setTexts} />
            <BuildingSelect value={buildingId} onChange={setBuildingId} />
          </div>
          <p className="text-muted">เลือกได้ 1–31 วัน นับตามวันที่ของกะ</p>
        </section>
        <section className={sectionClass} aria-labelledby="export-download">
          <h2 id="export-download" className={titleClass}>
            3. ดาวน์โหลด
          </h2>
          {typeof range === 'string' ? (
            <p className="text-muted">กรอกช่วงวันให้ถูกต้องก่อน</p>
          ) : (
            <div className="flex flex-col gap-1 rounded bg-bg px-3.5 py-3 wrap-anywhere">
              <strong>{fileNameFor(range)}</strong>
              <span className="text-muted">
                {sets.length === 0
                  ? 'ยังไม่ได้เลือกข้อมูล'
                  : `${sets.length} แผ่นงาน · ${sets.map((set) => setTitle[set]).join(' + ')}`}
              </span>
            </div>
          )}
          <div>
            <Button variant="primary" size="desk" disabled={sending} onClick={() => void download()}>
              {sending ? 'กำลังสร้างไฟล์...' : 'ดาวน์โหลด .xlsx'}
            </Button>
          </div>
          {notice && (
            <Note tone={notice.kind === 'ok' ? 'info' : 'error'} status={notice.kind === 'ok'}>
              {notice.text}
            </Note>
          )}
          <p className="text-muted">ไฟล์ที่ดาวน์โหลดแล้วห้ามส่งต่อนอกบริษัท</p>
          <MoreInfo summary="เกี่ยวกับไฟล์นี้">
            <span>ไม่มีพิกัดตำแหน่งดิบ มีแค่ระยะห่างจากป้าย และผลในรัศมี / นอกรัศมี</span>
            <span>การส่งออกเก็บลงประวัติการแก้ไขของแอดมิน (ใคร เมื่อไร ชุดไหน ช่วงวันไหน)</span>
            <span>ไฟล์ที่ดาวน์โหลดแล้วอยู่นอกการลบตามระยะเวลาเก็บข้อมูล</span>
          </MoreInfo>
          <Link className={`self-start ${linkClass}`} to="/dashboard">
            ‹ กลับไปหน้าสถานะจุด
          </Link>
        </section>
      </div>
    </main>
  );
}

const sectionClass = `flex flex-col gap-3.5 ${boxClass}`;
const titleClass = 'text-lg font-semibold';
