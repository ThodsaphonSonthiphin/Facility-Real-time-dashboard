import type { AreaAttendanceState, BoardEvent, BoardRow } from '../../shared/api/attendanceBoardApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { areaStateLabel, coverLine, eventOf, flagText } from './attendanceRules';

interface AttendanceTableProps {
  rows: BoardRow[];
  /** "เพิ่มเวลา" / "แก้เวลา" on a row with a regular Cleaner (facility-0051). Without it the table has no action column. */
  onCorrect?: (row: BoardRow) => void;
}

// The Area's state in the status colours, always with its words; พัก has its own (MASTER.md, facility-0087)
const stateBg: Record<AreaAttendanceState, string> = {
  Empty: 'bg-status-rework',
  Working: 'bg-status-passed',
  OnBreak: 'bg-status-break',
  Done: 'bg-status-idle',
  Covered: 'bg-status-pending',
};

const subClass = 'block text-xs font-normal text-muted';

/** Wireframe D2's table, in the server's order: Areas with nobody first (facility-0050). Names are React text only. */
export function AttendanceTable({ rows, onCorrect }: AttendanceTableProps) {
  if (rows.length === 0) {
    return <Note tone="info">ไม่พบพื้นที่ที่ตรงกับที่ค้นหา</Note>;
  }
  return (
    <Table label="การเข้างานรายพื้นที่">
      <thead>
        <tr>
          <th scope="col">พื้นที่</th>
          <th scope="col">แม่บ้าน</th>
          <th scope="col">สถานะ</th>
          {attendanceEvents.map((type) => (
            <th key={type} scope="col">
              {eventLabel[type]}
            </th>
          ))}
          <th scope="col">ไกลป้าย</th>
          {onCorrect && <th scope="col">แก้ไข</th>}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          // An Area with nobody is tinted across the row, as in the old look; [&>*] reaches its th and td cells
          <tr key={row.areaId} className={row.state === 'Empty' ? '[&>*]:bg-error-bg' : undefined}>
            <th scope="row">
              {`${row.areaCode} ${row.areaName}`}
              <span className={subClass}>{`ตึก ${row.buildingCode}`}</span>
            </th>
            <td>
              <Who row={row} />
            </td>
            <td>
              <span
                className={`inline-block rounded-full px-2.5 py-0.5 font-semibold whitespace-nowrap text-on-primary ${stateBg[row.state]}`}
              >
                {areaStateLabel[row.state]}
              </span>
            </td>
            {attendanceEvents.map((type) => (
              <td key={type} className="tabular-nums">
                <EventTime event={eventOf(row, type)} />
              </td>
            ))}
            <td>{flagText(row)}</td>
            {onCorrect && (
              <td>
                {row.cleaner && (
                  <Button
                    variant="link"
                    size="desk"
                    aria-label={`${row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'} ${row.cleaner.displayName}`}
                    onClick={() => onCorrect(row)}
                  >
                    {row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'}
                  </Button>
                )}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

function Who({ row }: { row: BoardRow }) {
  return (
    <div className="flex flex-col">
      <span>{row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'}</span>
      {row.cleaner && !eventOf(row, 'ShiftIn') && <span className={subClass}>ยังไม่ลงเวลา</span>}
      {row.covers.map((cover) => (
        <span key={cover.id} className={subClass}>
          {coverLine(cover)}
        </span>
      ))}
    </div>
  );
}

function EventTime({ event }: { event?: BoardEvent }) {
  if (!event) {
    return <>–</>;
  }
  return (
    <>
      {formatTime(event.occurredAt)}
      {event.source !== 'Scan' && (
        <span className="ml-1.5 rounded border border-warn-border bg-warn-bg px-1.5 text-xs whitespace-nowrap text-warn-text">
          แก้โดยแอดมิน
        </span>
      )}
    </>
  );
}
