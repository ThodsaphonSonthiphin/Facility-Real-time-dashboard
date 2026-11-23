import { ArrowRightLeft, Check, Coffee, MapPin, Minus, Pencil, UserX, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { AreaAttendanceState, BoardEvent, BoardRow } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { AvatarBadge } from '../../shared/components/AvatarBadge/AvatarBadge';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Badge, EditedMark } from '../../shared/components/Badge';
import { PageEmpty } from '../../shared/components/PageState';
import { buttonClass, tableCell } from '../../shared/components/styles';
import { attendanceEventLabel, attendanceEvents, entryLabel, transferStateLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { useBuildingNames, buildingText } from '../../shared/api/buildingNames';
import { areaStateLabel, attendanceTargets, coverDetail, eventOf } from './attendanceRules';

interface AttendanceTableProps {
  rows: BoardRow[];
  shift?: Shift;
  /** "เพิ่มเวลา" / "แก้เวลา" on a row with a regular Cleaner (facility-0051). */
  onCorrect?: (row: BoardRow, userId: number) => void;
  /** The bar under the table, inside its box: the count, the [แก้] legend and the pages. */
  footer?: ReactNode;
}

// The Area's state as a light chip in the status colours, always with its words and an icon; พัก has its own
// (MASTER.md, facility-0087)
const stateChip: Record<AreaAttendanceState, { tone: string; Icon: LucideIcon }> = {
  Empty: { tone: 'border-overdue-border bg-overdue-bg text-overdue', Icon: UserX },
  Working: { tone: 'border-passed-border bg-passed-bg text-passed', Icon: Check },
  OnBreak: { tone: 'border-break-border bg-break-bg text-break', Icon: Coffee },
  Done: { tone: 'border-idle-border bg-idle-bg text-idle', Icon: Minus },
  Covered: { tone: 'border-pending-border bg-pending-bg text-pending', Icon: ArrowRightLeft },
};

const subClass = 'block text-xs font-normal text-text-2';

/** Wireframe D2's table, in the server's order: Areas with nobody first (facility-0050). Names are React text only. */
export function AttendanceTable({ rows, shift, onCorrect, footer }: AttendanceTableProps) {
  const names = useBuildingNames();
  const actions = attendanceEvents;
  const showActionColumn = Boolean(onCorrect || rows.some((row) => row.state === 'Empty'));
  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-border bg-surface">
        <PageEmpty title="ไม่พบพื้นที่ที่ตรงกับที่ค้นหา">ลองค้นหาด้วยรหัสหรือชื่อพื้นที่อื่น</PageEmpty>
      </div>
    );
  }
  return (
    <Table label="การเข้างานรายพื้นที่" minWidth="min-w-225" footer={footer}>
      <thead className="[&_th]:font-bold">
        <tr>
          <th scope="col" className="min-w-40">
            พื้นที่
          </th>
          <th scope="col" className="min-w-36">
            แม่บ้าน
          </th>
          <th scope="col" className={tableCell.chip}>
            สถานะ
          </th>
          {actions.map((type) => (
            <th key={type} scope="col" className={`${tableCell.num} whitespace-nowrap`}>
              {attendanceEventLabel(type, shift)}
            </th>
          ))}
          {showActionColumn && (
            <th scope="col" className={`relative ${tableCell.actions}`}>
              {/* Contain the absolute sr-only label inside the scrolling table, so it cannot widen the document. */}
              <span className="sr-only">การกระทำ</span>
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          // An Area with nobody is tinted across the row, as in the old look; [&>*] reaches its th and td cells
          <tr key={row.areaId} className={row.state === 'Empty' ? '[&>*]:bg-overdue-bg/50' : undefined}>
            <th scope="row">
              {`${row.areaCode} ${row.areaName}`}
              <span className={subClass}>{`ตึก ${buildingText(row.buildingCode, names)}`}</span>
            </th>
            <td>
              <Who row={row} />
            </td>
            <td className={tableCell.chip}>
              <StateChip state={row.state} />
              <EntryDetails row={row} />
            </td>
            {actions.map((type) => (
              <td key={type} className={`${tableCell.num} w-26`}>
                <EventTime event={eventOf(row, type)} />
              </td>
            ))}
            {showActionColumn && (
              <td className={tableCell.actions}>
                <span className="inline-flex flex-col items-start gap-2">
                  {onCorrect && attendanceTargets(row).map(({ cleaner }) => (
                    <Button
                      key={cleaner.id}
                      variant="link"
                      size="desk"
                      aria-label={`แก้เวลา ${cleaner.displayName}`}
                      onClick={() => onCorrect(row, cleaner.id)}
                    >
                      <Pencil aria-hidden="true" className="size-4" />
                      {row.additionalCleaners?.length ? `แก้เวลา ${cleaner.displayName}` : 'แก้เวลา'}
                    </Button>
                  ))}
                  {row.state === 'Empty' && (
                    <Link to="/admin/schedules" data-tap="" className={buttonClass('link', 'desk')}>
                      <ArrowRightLeft aria-hidden="true" className="size-4" />
                      ไปจัดคนแทนที่ตารางงาน
                    </Link>
                  )}
                </span>
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function StateChip({ state }: { state: AreaAttendanceState }) {
  const { tone, Icon } = stateChip[state];
  return (
    <span
      className={`inline-flex min-h-7 w-31 items-center justify-center gap-1.5 rounded-md border px-2.5 font-semibold whitespace-nowrap ${tone}`}
    >
      <Icon aria-hidden="true" className="size-4 shrink-0" />
      {areaStateLabel[state]}
    </span>
  );
}

function EntryDetails({ row }: { row: BoardRow }) {
  const responsibilities = row.responsibilities ?? (row.responsibility ? [row.responsibility] : []);
  return (
    <>
      {row.entry && <span className={subClass}>{entryLabel[row.entry]}</span>}
      {row.entryEvidence?.map((evidence, index) => (
        <span key={index} className={subClass}>
          {evidence.kind === 'shift-in' ? 'ลงเวลาเข้างาน' : 'ยืนยันเข้าพื้นที่ใหม่'} {formatTime(evidence.recordedAt)}
        </span>
      ))}
      {responsibilities.map((responsibility) => (
        <span key={responsibility.userId} className={subClass}>
          {responsibility.displayName} · {transferStateLabel[responsibility.state]}
        </span>
      ))}
    </>
  );
}

function Who({ row }: { row: BoardRow }) {
  return (
    <div className="flex flex-col">
      {row.cleaner ? (
        <span className="flex items-center gap-2">
          <AvatarBadge name={row.cleaner.displayName} size="sm" tone={row.state === 'Empty' ? 'amber' : 'blue'} />
          <span>{row.cleaner.displayName}</span>
        </span>
      ) : (
        <span>ไม่มีแม่บ้านประจำ</span>
      )}
      {row.cleaner && !eventOf(row, 'ShiftIn') && <span className={subClass}>ยังไม่ลงเวลา</span>}
      {row.additionalCleaners?.map(({ cleaner }) => (
        <span key={cleaner.id} className="mt-1 flex items-center gap-2">
          <AvatarBadge name={cleaner.displayName} size="sm" tone="teal" />
          <span>{cleaner.displayName}</span>
        </span>
      ))}
      {row.covers.map((cover) => (
        <span key={cover.id} className="mt-1 flex flex-col">
          <span className="flex flex-wrap items-center gap-1.5">
            <AvatarBadge name={cover.displayName} size="sm" tone="purple" />
            {cover.displayName}
            <Badge kind="cover">ทำแทน</Badge>
          </span>
          <span className={subClass}>{coverDetail(cover)}</span>
        </span>
      ))}
    </div>
  );
}

/**
 * The time on the right of a 112 px column, the [✎ แก้] mark before it when an Admin set it, and how far from its sign
 * it was scanned when outside the radius (facility-0035), in the far flag's colours.
 */
function EventTime({ event }: { event?: BoardEvent }) {
  if (!event) {
    return <>–</>;
  }
  return (
    <span className="flex items-center justify-end gap-1.5 whitespace-nowrap">
      {event.source === 'SupervisorAdd' && <span className="text-xs text-text-2">หัวหน้าเติมเวลา</span>}
      {(event.source === 'AdminAdd' || event.source === 'AdminEdit') && <EditedMark />}
      {formatTime(event.occurredAt)}
      {event.withinRadius === false && (
        <span
          role="img"
          aria-label={`ไกลป้าย ${event.distanceM ?? '?'} ม.`}
          title={`ไกลป้าย ${event.distanceM ?? '?'} ม.`}
          className="inline-flex min-h-5 items-center gap-0.5 rounded-sm border border-overdue-border bg-surface px-1.5 text-xs font-semibold text-overdue"
        >
          <MapPin aria-hidden="true" className="size-3 shrink-0" />
          {`${event.distanceM ?? '?'} ม.`}
        </span>
      )}
    </span>
  );
}
