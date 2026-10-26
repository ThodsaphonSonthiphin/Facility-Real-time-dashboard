import { boxClass, focusRing } from '../../shared/components/styles';

/** D6's side panel forms: add, edit, phone and password, each a white box with its heading. */
export const formClass = `flex flex-col gap-2.5 ${boxClass}`;
export const formTitleClass = 'text-lg font-semibold';

/**
 * One answer of a radio group (role, shift), as D10's shift pattern: 36 px high (pages/admin.md). A held shift is
 * disabled, and its words fade with the radio (MASTER.md: visibly weaker), as ChoiceTile's do.
 */
export const radioLabelClass =
  'inline-flex min-h-9 items-center gap-2 has-disabled:cursor-not-allowed has-disabled:opacity-50';
export const radioClass = `accent-primary ${focusRing}`;
