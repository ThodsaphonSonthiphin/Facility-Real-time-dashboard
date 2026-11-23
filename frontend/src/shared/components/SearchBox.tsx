import { Search } from 'lucide-react';
import { TextInput } from './Field';
import type { InputSize } from './styles';

/** A search box with the magnifier inside it (the design file's search fields). */
export function SearchBox({
  label,
  value,
  onChange,
  size = 'desk',
  className = 'w-full',
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  size?: InputSize;
  className?: string;
}) {
  return (
    <span className={`relative flex items-center ${className}`}>
      <Search aria-hidden="true" className="pointer-events-none absolute left-2.5 size-4 text-text-2" />
      <TextInput
        size={size}
        type="search"
        aria-label={label}
        placeholder={label}
        className="w-full pl-8.5"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </span>
  );
}
