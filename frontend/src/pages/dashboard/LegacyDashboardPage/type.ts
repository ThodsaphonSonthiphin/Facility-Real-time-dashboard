import type { AreaGroup } from '../boardRules';

/** How many folded Areas the design shows before "แสดงอีก n พื้นที่". */
export const QUIET_ROWS = 5;

export interface AreaSectionProps {
  group: AreaGroup;
}
