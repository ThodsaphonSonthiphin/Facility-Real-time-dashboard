import { Check, CirclePlus, Clock, List, MapPin } from 'lucide-react';
import { Link } from 'react-router';
import type { Placement, PointCard } from '../../shared/api/types';
import { Note } from '../../shared/components/Note';
import { ResultScreen } from '../../shared/components/ResultScreen';
import { StatusChip } from '../../shared/components/StatusChip';
import { buttonClass } from '../../shared/components/styles';
import { formatMinutes, formatTime } from '../../shared/utils/thaiTime';
import type { ScanRecordCreated } from './scanApi';
import { describeDraft, type ScanDraft } from './scanRules';

const title: Record<Placement, string> = {
  OnTime: 'ส่งงานแล้ว',
  Late: 'ส่งงานแล้ว',
  Rework: 'ส่งงานแก้แล้ว',
  OffRound: 'ส่งเพิ่มแล้ว (ไม่นับรอบ)',
};

interface SubmitResultProps {
  point: PointCard;
  draft: ScanDraft;
  result: ScanRecordCreated;
}

/**
 * Wireframes M5 and M13: the server placed the submission (facility-0047 rule 3) and judged the distance
 * (facility-0037). The point's status is one line under the heading, not a card (facility-0079).
 */
export function SubmitResult({ point, draft, result }: SubmitResultProps) {
  const far = result.withinRadius === false;
  // What matters most to the Cleaner sets the colour: not counted (grey), then late or far (orange), else saved (green)
  const look =
    result.placement === 'OffRound'
      ? { tone: 'neu' as const, Icon: CirclePlus }
      : result.placement === 'Late'
        ? { tone: 'warn' as const, Icon: Clock }
        : far
          ? { tone: 'warn' as const, Icon: MapPin }
          : { tone: 'ok' as const, Icon: Check };
  return (
    <ResultScreen
      tone={look.tone}
      icon={<look.Icon aria-hidden="true" className="size-14" strokeWidth={2.25} />}
      title={`${title[result.placement]} ${formatTime(result.submittedAt)}`}
      bottom={
        <Link className={buttonClass('primary', 'main')} data-tap="" to="/my-work">
          <List aria-hidden="true" className="size-6" />
          ดูงานของฉัน
        </Link>
      }
    >
      <span className="text-xl font-semibold">{`${point.name} · ${describeDraft(draft)}`}</span>
      <span className="text-lg text-text-3">{`${point.areaName} · ตึก ${point.buildingCode}`}</span>
      <p className="flex flex-wrap items-center justify-center gap-2 text-lg">
        สถานะของจุดนี้ <StatusChip status={result.newPointStatus} size="lg" />
      </p>
      <div className="flex w-full flex-col gap-3 text-left">
        {result.placement === 'Late' && (
          <Note tone="warn" title={`ส่งช้า ${formatMinutes(result.lateMinutes ?? 0)}`}>
            <span>รอบนี้จบแล้วและยังไม่มีใครส่ง จึงนับเป็นงานของรอบนี้ แต่ติดว่าส่งช้า</span>
          </Note>
        )}
        {result.placement === 'Rework' && <Note tone="info">นับเป็นงานแก้ของรอบนี้ หัวหน้าจะตรวจอีกครั้ง</Note>}
        {result.placement === 'OffRound' && (
          <Note tone="warn">
            ตอนนี้ไม่มีรอบไหนรับงานนี้ ระบบบันทึกไว้แล้ว แต่ไม่นับเป็นรอบใด และสถานะของจุดไม่เปลี่ยน
          </Note>
        )}
        {far && (
          <Note tone="warn">
            {`ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้งแอดมินได้เลย`}
          </Note>
        )}
        {result.withinRadius === true && (
          <p className="text-center text-sm text-text-2">{`ห่างป้าย ${result.distanceM} ม. · อยู่ในรัศมี`}</p>
        )}
      </div>
    </ResultScreen>
  );
}
