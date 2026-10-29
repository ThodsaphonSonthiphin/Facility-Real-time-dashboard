import type { AreaDetail } from '../../shared/api/adminSetupApi';
import { useGetBuildingsQuery, useGetCleanersQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Drawer } from '../../shared/components/Modal';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AreaActiveSection } from './AreaActiveSection';
import { AreaForm } from './AreaForm';

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
  const buildings = useGetBuildingsQuery(undefined, freshOnOpen);
  const cleaners = useGetCleanersQuery(undefined, freshOnOpen);
  const error = buildings.error ?? cleaners.error;
  const titleId = 'area-editor-title';

  return (
    <Drawer labelledBy={titleId} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <h2 id={titleId} tabIndex={-1} className="text-lg font-semibold">
            {`แก้ไขพื้นที่ · ${detail.area.code} ${detail.area.name}`}
          </h2>
          <Button size="desk" onClick={onClose}>
            ปิด
          </Button>
        </div>
        {error && <Note tone="error">{getErrorMessage(error)}</Note>}
        {buildings.data && cleaners.data ? (
          <AreaForm
            key={detail.area.id}
            detail={detail}
            buildings={buildings.data}
            cleaners={cleaners.data}
            onSaved={(saved) => onSaved(`บันทึกพื้นที่ ${saved.area.code} ${saved.area.name} แล้ว`)}
            onCancel={onClose}
          />
        ) : (
          !error && <p className="text-text-2">กำลังโหลด...</p>
        )}
        <AreaActiveSection detail={detail} />
        <MoreInfo summary="เกี่ยวกับการบันทึก">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</MoreInfo>
      </div>
    </Drawer>
  );
}
