import type { BoardEvent, BoardRow } from '../../shared/api/attendanceBoardApi';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';
import { areaStateLabel, coverLine, eventOf, flagText } from './attendanceRules';

interface AttendanceTableProps {
  rows: BoardRow[];
  /** "เพิ่มเวลา" / "แก้เวลา" on a row with a regular Cleaner (facility-0051). Without it the table has no action column. */
  onCorrect?: (row: BoardRow) => void;
}

/** Wireframe D2's table, in the server's order: Areas with nobody first (facility-0050). Names are React text only. */
export function AttendanceTable({ rows, onCorrect }: AttendanceTableProps) {
  if (rows.length === 0) {
    return <p className="app-note app-note--info">ไม่พบ Area ที่ตรงกับที่ค้นหา</p>;
  }
  return (
    <div className="app-table-scroll">
    <table className="app-table attendance-table" aria-label="การเข้างานราย Area">
      <thead>
        <tr>
          <th scope="col">Area</th>
          <th scope="col">แม่บ้าน</th>
          <th scope="col">สถานะ</th>
          {attendanceEvents.map((type) => (
            <th key={type} scope="col">
              {eventLabel[type]}
            </th>
          ))}
          <th scope="col">ธง</th>
          {onCorrect && <th scope="col">แก้ไข</th>}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.areaId} className={row.state === 'Empty' ? 'attendance-row--empty' : undefined}>
            <th scope="row">
              {`${row.areaCode} ${row.areaName}`}
              <span className="attendance-sub">{`ตึก ${row.buildingCode}`}</span>
            </th>
            <td>
              <Who row={row} />
            </td>
            <td>
              <span className={`attendance-state attendance-state--${row.state}`}>{areaStateLabel[row.state]}</span>
            </td>
            {attendanceEvents.map((type) => (
              <td key={type}>
                <EventTime event={eventOf(row, type)} />
              </td>
            ))}
            <td>{flagText(row)}</td>
            {onCorrect && (
              <td>
                {row.cleaner && (
                  <button
                    type="button"
                    className="app-link-button"
                    aria-label={`${row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'} ${row.cleaner.displayName}`}
                    onClick={() => onCorrect(row)}
                  >
                    {row.events.length === 0 ? 'เพิ่มเวลา' : 'แก้เวลา'}
                  </button>
                )}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
    </div>
  );
}

function Who({ row }: { row: BoardRow }) {
  return (
    <div className="attendance-who">
      <span>{row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'}</span>
      {row.cleaner && !eventOf(row, 'ShiftIn') && <span className="attendance-sub">ยังไม่ลงเวลา</span>}
      {row.covers.map((cover) => (
        <span key={cover.id} className="attendance-sub">
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
      {event.source !== 'Scan' && <span className="attendance-badge">แก้โดย Admin</span>}
    </>
  );
}
