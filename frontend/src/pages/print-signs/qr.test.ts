import { describe, expect, it } from 'vitest';
import { qrPath } from './qr';

describe('qrPath (facility-0021: the QR is drawn on this computer)', () => {
  it('draws a sign URL as dark-module runs, with the standard quiet zone of 4 modules', () => {
    const { size, d } = qrPath('https://facility.example/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f');

    expect(size).toBe(45); // version 5 (37 modules) plus 4 on each side
    // The top rows of the three finder patterns: top-left, top-right, bottom-left
    expect(d.startsWith('M4 4h7v1h-7z')).toBe(true);
    expect(d).toContain('M34 4h7v1h-7z');
    expect(d).toContain('M4 34h7v1h-7z');
  });
});
