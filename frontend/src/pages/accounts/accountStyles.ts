import { focusRing } from '../../shared/components/styles';

/**
 * D6's forms: add, edit, phone and password. Each one fills the right-hand Drawer, so it draws no box of its own (the
 * Drawer already is one); its heading is the Drawer's name.
 */
export const formClass = 'flex min-h-0 flex-1 flex-col';

/**
 * One answer of a radio group (role, shift), as D10's shift pattern: 36 px high (pages/admin.md). A held shift is
 * disabled, and its words fade with the radio (MASTER.md: visibly weaker), as ChoiceTile's do.
 */
export const radioLabelClass =
  'inline-flex min-h-9 items-center gap-2 has-disabled:cursor-not-allowed has-disabled:opacity-50';
export const radioClass = `accent-accent ${focusRing}`;
