import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { BuildingSelect } from '../../shared/components/BuildingSelect';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { RangeFields } from '../../shared/components/RangeFields';
import { buttonClass } from '../../shared/components/styles';
import {
  checkRange,
  exportLink,
  lastSevenDays,
  rangeTexts,
  readBuildingId,
  readRange,
  toRangeParams,
  type DateRange,
} from '../../shared/utils/dateRange';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatShiftDate, thaiToday } from '../../shared/utils/thaiTime';
import { useGetInspectionSummaryQuery, type InspectionSummary } from './summaryApi';
import { SUMMARY_MAX_DAYS } from './summaryRules';
import { SummaryTable } from './SummaryTable';

/**
 * facility-0048 rule 5, wireframe D7: each active Supervisor's inspections over a range, as the server counts them.
 * The page judges nothing (plan 12 decision 4). With no range in the address the server's last 7 dates apply; the
 * address keeps a chosen range and building.
 */
export function InspectionSummaryPage() {
  const [params, setParams] = useSearchParams();
  const asked = readRange(params);
  const buildingId = readBuildingId(params);
  const { currentData: summary, error, isFetching, refetch } = useGetInspectionSummaryQuery({ ...asked, buildingId }, freshOnOpen);
  const [texts, setTexts] = useState(() => rangeTexts(asked ?? lastSevenDays(thaiToday())));
  const [problem, setProblem] = useState<string | null>(null);
  // the fields follow the address when it changes under the open page (menu link, Back)
  const [seenRange, setSeenRange] = useState(rangeKey(asked));
  if (seenRange !== rangeKey(asked)) {
    setSeenRange(rangeKey(asked));
    setTexts(rangeTexts(asked ?? lastSevenDays(thaiToday())));
    setProblem(null);
  }
  const go = (range: DateRange | undefined, building: number | undefined) => setParams(toRangeParams(range, building));

  const show = () => {
    const range = checkRange(texts, SUMMARY_MAX_DAYS);
    if (typeof range === 'string') {
      setProblem(range);
      return;
    }
    setProblem(null);
    if (asked && rangeKey(asked) === rangeKey(range)) {
      // the same address would send no request and add a history entry
      void refetch();
      return;
    }
    go(range, buildingId);
  };

  return (
    <main className="flex flex-col gap-6 text-sm text-text">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-bold">สรุปการตรวจของหัวหน้า</h1>
        <span className="text-text-2">{summary ? rangeLine(summary, Boolean(asked)) : 'แอดมินเท่านั้น'}</span>
      </header>
      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-surface px-4 py-3">
        <RangeFields value={texts} onChange={setTexts} onEnter={show} />
        <Button size="desk" onClick={show}>
          แสดง
        </Button>
        <BuildingSelect value={buildingId} onChange={(building) => go(asked, building)} />
        <Link
          className={`ml-auto ${buttonClass('secondary', 'desk')}`}
          data-tap=""
          to={exportLink(summary ? { from: summary.from, to: summary.to } : asked, buildingId)}
        >
          ส่งออก Excel
        </Link>
      </div>
      {problem && <Note tone="error">{problem}</Note>}
      {isFetching && !summary && <PageLoading lines={3} />}
      {error && (
        <Note tone="error" onRetry={() => void refetch()}>
          {getErrorMessage(error)}
        </Note>
      )}
      {summary && <SummaryTable rows={summary.rows} />}
      <SummaryNotes />
    </main>
  );
}

function rangeKey(range: DateRange | undefined): string {
  return range ? `${range.from}_${range.to}` : '';
}

/** The range the server counted, which is the one asked for, or its default. */
function rangeLine(summary: InspectionSummary, asked: boolean): string {
  return `แอดมินเท่านั้น · ${asked ? 'ช่วงที่เลือก' : '7 วันล่าสุด'} (${formatShiftDate(summary.from)}–${formatShiftDate(summary.to)})`;
}

/** Wireframe D7's notes, with "จำนวนครั้งที่ตรวจ" and plan 6 decision 17 added; shut until asked. */
function SummaryNotes() {
  return (
    <MoreInfo summary="วิธีนับตัวเลข">
      <span>
        หน้านี้แสดงข้อมูลให้แอดมินเห็น ระบบไม่ประเมินผลเอง และไม่มีขั้นต่ำว่าหัวหน้าต้องตรวจกี่ครั้ง · "จุดที่ตรวจแล้ว"
        นับจุดในตึกและกะของหัวหน้าที่ถูกตรวจอย่างน้อย 1 ครั้งในช่วงที่เลือก
      </span>
      <span>
        "ไม่ได้ตรวจ" คืองานที่แม่บ้านส่งแล้ว แต่รอบจบก่อนหัวหน้ามาตรวจ · "ตรวจตอนอยู่ไกลป้าย"
        คือตรวจตอนที่มือถืออยู่นอกรัศมีของป้าย · "จำนวนครั้งที่ตรวจ" นับทุกครั้งที่หัวหน้าคนนั้นกดบันทึกผลตรวจ งานชิ้นเดียวตรวจซ้ำได้จึงนับหลายครั้ง และรวมตอนตรวจแทนตึกอื่น
      </span>
      <span>งานที่แม่บ้านส่งหลังจบกะไม่นับในตัวเลขของหัวหน้า เพราะไม่มีใครตรวจได้แล้ว</span>
      <span>
        งานที่แม่บ้านส่งทับด้วยงานใหม่ในรอบเดียวกันก่อนที่หัวหน้าจะตรวจ ก็ไม่นับ เพราะหัวหน้าตรวจได้เฉพาะงานล่าสุดของรอบ
      </span>
      <span>ดูรายละเอียดรายครั้งได้จากไฟล์ Excel</span>
    </MoreInfo>
  );
}
