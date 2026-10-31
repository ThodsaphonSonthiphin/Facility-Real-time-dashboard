import { ConfirmationDialog } from './ConfirmationDialog';

interface CoverCancellationDialogProps {
  areaCode: string;
  name: string;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}

export function CoverCancellationDialog({ areaCode, name, onConfirm, onCancel }: CoverCancellationDialogProps) {
  return (
    <ConfirmationDialog
      kind="cover"
      title={`ยกเลิกการทำแทน ${areaCode}?`}
      body={`${name} จะไม่เห็นและส่งงานของ ${areaCode} ได้อีกในกะนี้`}
      impacts={['งานที่ส่งไปแล้วยังนับ', 'สถานะพื้นที่ขึ้นอยู่กับการเข้างานของแม่บ้านประจำและคนทำแทนที่เหลือ']}
      confirmLabel="ยกเลิกการทำแทน"
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
