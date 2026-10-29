import { Ban, List } from 'lucide-react';
import { Link } from 'react-router';
import type { BlockedScan } from '../api/types';
import { Facts, Panel } from './Boxes';
import { Note } from './Note';
import { ResultScreen } from './ResultScreen';
import { buttonClass } from './styles';

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

/**
 * Wireframe M8 (facility-0041, 0048): nothing was recorded; the Admin already sees this Blocked Scan and can assign a
 * cover. A full red screen (the design file's สแกนผิดพื้นที่), for a Cleaner and for a Supervisor.
 */
export function BlockedScanScreen({ blocked, sign, ownAreaName }: BlockedScanScreenProps) {
  const home =
    sign === 'inspect'
      ? { to: '/my-building', label: 'กลับไปตึกของฉัน' }
      : { to: '/my-work', label: 'กลับไปงานของฉัน' };
  return (
    <ResultScreen
      tone="err"
      icon={<Ban aria-hidden="true" className="size-14" strokeWidth={2.25} />}
      title={blocked.message}
      bottom={
        <Link className={buttonClass('primary', 'main')} data-tap="" to={home.to}>
          <List aria-hidden="true" className="size-6" />
          {home.label}
        </Link>
      }
    >
      <span className="text-lg text-text-3">{sign === 'check-in' ? 'ป้ายลงเวลา' : 'ป้ายจุดทำความสะอาด'}</span>
      <div className="flex w-full flex-col gap-4 text-left">
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
          <p>
            ลงเวลาที่ป้ายของพื้นที่ตัวเองเท่านั้น วันที่ทำแทนพื้นที่อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ
            แอดมินเห็นการสแกนครั้งนี้แล้ว
          </p>
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
      </div>
    </ResultScreen>
  );
}
