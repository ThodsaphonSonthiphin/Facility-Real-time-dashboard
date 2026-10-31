import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { DialogHeader, Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';
import { CheckInSignManagement } from './CheckInSignManagement';
import { useAreaEditorDrawer } from './useAreaEditorDrawer';

interface AreaEditorDrawerProps {
  detail: AreaDetail;
  onClose: () => void;
  /** The save closes the Drawer and floats its answer. */
  onSaved: (message: string) => void;
}

/**
 * D4's "แก้ไขพื้นที่" (owner, 2026-10-23): D10's form, and the Area's deactivation, in the right-hand Drawer, so the
 * Area's table stays visible on the left. The building and Cleaner lists are asked for only while it is open.
 */
export function AreaEditorDrawer({ detail, onClose, onSaved }: AreaEditorDrawerProps) {
  const { buildings, cleaners, error, dismissal } = useAreaEditorDrawer(onClose);
  const titleId = 'area-editor-title';

  return (
    <Drawer width="lg" labelledBy={titleId} onClose={dismissal.close}>
      <div className="flex h-full min-h-0 flex-col">
        <DialogHeader id={titleId} title="แก้ไขพื้นที่" detail={`${detail.area.code} ${detail.area.name} · ตึก ${detail.area.buildingCode}`} closeDisabled={dismissal.pending} onClose={dismissal.close} />
        {error && <Note tone="error">{getErrorMessage(error)}</Note>}
        {buildings.data && cleaners.data ? (
          <AreaForm
            key={detail.area.id}
            detail={detail}
            buildings={buildings.data}
            cleaners={cleaners.data}
            framed={false}
            onSaved={(saved) => onSaved(`บันทึกพื้นที่ ${saved.area.code} ${saved.area.name} แล้ว`)}
            onCancel={dismissal.close}
          >
            <CheckInSignManagement detail={detail} />
            <MoreInfo summary="การใช้งานพื้นที่และประวัติ"><AreaActiveSection detail={detail} /></MoreInfo>
            <MoreInfo summary="เกี่ยวกับการบันทึก">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</MoreInfo>
          </AreaForm>
        ) : (
          !error && <p className="text-text-2">กำลังโหลด...</p>
        )}
      </div>
    </Drawer>
  );
}
