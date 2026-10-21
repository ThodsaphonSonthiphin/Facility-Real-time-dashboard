import { vi } from 'vitest';

export interface SavedFile {
  fileName: string;
  blob: Blob;
}

/**
 * jsdom has no object URLs and cannot save a file, so this stands in for the browser's download (a browser seam,
 * regression-net #32): every link the page clicks with a `download` name is recorded with the Blob behind it.
 * The click spy is restored after each test (vi.restoreAllMocks in setup.ts). The two URL functions stay defined,
 * so a revoke that runs after the test ends is harmless.
 */
export function fakeDownload() {
  const saved: SavedFile[] = [];
  const blobs = new Map<string, Blob>();
  let next = 0;
  Object.defineProperty(URL, 'createObjectURL', {
    configurable: true,
    writable: true,
    value: (blob: Blob) => {
      next += 1;
      const url = `blob:fake/${next}`;
      blobs.set(url, blob);
      return url;
    },
  });
  Object.defineProperty(URL, 'revokeObjectURL', {
    configurable: true,
    writable: true,
    value: (url: string) => {
      blobs.delete(url);
    },
  });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    const blob = blobs.get(this.href);
    if (this.download && blob) {
      saved.push({ fileName: this.download, blob });
    }
  });
  return { saved };
}
