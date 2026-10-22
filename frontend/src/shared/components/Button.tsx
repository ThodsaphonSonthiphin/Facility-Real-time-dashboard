import type { ButtonHTMLAttributes } from 'react';
import { buttonClass, type ButtonSize, type ButtonVariant } from './styles';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

/**
 * The new look's button (facility-0087). `data-tap` gives it the press that shows at once (tapFeedback, owner
 * 2026-10-21). Icons go in the children, beside the words, with `aria-hidden` (facility-0088).
 */
export function Button({ variant, size, type = 'button', className, ...rest }: ButtonProps) {
  const classes = className ? `${buttonClass(variant, size)} ${className}` : buttonClass(variant, size);
  return <button type={type} data-tap="" className={classes} {...rest} />;
}
