export function sortedCopy<T>(values: readonly T[], compare: (a: T, b: T) => number): T[] {
  if (typeof values.toSorted === 'function') {
    return values.toSorted(compare);
  }

  // Vite's default browser target includes Firefox 114. toSorted starts at 115.
  return [...values].sort(compare);
}
