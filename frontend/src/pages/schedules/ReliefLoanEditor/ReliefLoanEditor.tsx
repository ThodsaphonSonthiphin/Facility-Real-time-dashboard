import { ReliefAssignmentEditor } from '../ReliefAssignmentEditor/ReliefAssignmentEditor';
import type { ReliefAssignmentEditorProps } from '../ReliefAssignmentEditor/type';

export function ReliefLoanEditor(props: ReliefAssignmentEditorProps) {
  return <ReliefAssignmentEditor {...props} mode="loan" />;
}
