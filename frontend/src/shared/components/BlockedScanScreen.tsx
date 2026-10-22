import { Link } from 'react-router';
import type { BlockedScan } from '../api/types';
import { Facts, Panel } from './Boxes';
import { Note } from './Note';
import { buttonClass, helpScreenClass } from './styles';

interface BlockedScanScreenProps {
  blocked: BlockedScan;
  /**
   * check-in: a Cleaner at another Area's check-in sign; a cover never moves where they check in (facility-0041
   * item 4). point: a Cleaner at another Area's point. inspect: a Supervisor at a point of another building, or
   * outside their own shift (facility-0048).
   */
  sign: 'check-in' | 'point' | 'inspect';
  ownAreaName?: string;
}

const notSaved: Record<BlockedScanScreenProps['sign'], string> = {
  'check-in': 'ไม่ได้บันทึกเวลา',
  point: 'ไม่ได้บันทึกงาน',
  inspect: 'ไม่ได้ตรวจงาน',
};

/** Wireframe M8 (facility-0041, 0048): nothing was recorded; the Admin already sees this Blocked Scan and can assign a cover. */
export function BlockedScanScreen({ blocked, sign, ownAreaName }: BlockedScanScreenProps) {
  return (
    <main className={helpScreenClass.main}>
      <span className="text-md tracking-wide text-muted">{sign === 'check-in' ? 'ป้ายลงเวลา' : 'ป้ายจุดทำความสะอาด'}</span>
      <h1 className={helpScreenClass.title}>{blocked.message}</h1>
      <Note tone="error" alert={false}>
        {notSaved[sign]}
      </Note>
      <Facts
        items={[
          { label: 'ป้ายที่สแกน', value: `${blocked.areaName} (${blocked.areaCode}) · ตึก ${blocked.buildingCode}` },
          ...(ownAreaName ? [{ label: 'พื้นที่ของคุณ', value: ownAreaName }] : []),
        ]}
      />
      {sign === 'check-in' && (
        <p>ลงเวลาที่ป้ายของพื้นที่ตัวเองเท่านั้น วันที่ทำแทนพื้นที่อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ แอดมินเห็นการสแกนครั้งนี้แล้ว</p>
      )}
      {sign === 'point' && (
        <Panel title="ถ้าได้รับมอบหมายให้ทำแทน">
          <p>
            {`แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณทำแทน ${blocked.areaName} เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </Panel>
      )}
      {sign === 'inspect' && (
        <Panel title="ถ้าได้รับมอบหมายให้ตรวจแทน">
          <p>
            {`แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณตรวจแทนตึก ${blocked.buildingCode} ในกะนี้ เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ`}
          </p>
        </Panel>
      )}
      {sign === 'inspect' ? (
        <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-building">
          กลับไปตึกของฉัน
        </Link>
      ) : (
        <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-work">
          กลับไปงานของฉัน
        </Link>
      )}
    </main>
  );
}
