import { describe, expect, it } from 'vitest';
import { fileNameFor, fileNameOf } from './exportRules';

const range = { from: '2026-10-01', to: '2026-10-07' };

describe('exportRules (D8)', () => {
  it("fileNameFor is the server's own name for the range", () => {
    expect(fileNameFor(range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
  });

  it("fileNameOf takes ASP.NET's file name, and falls back to the server's pattern", () => {
    expect(
      fileNameOf("attachment; filename=facility_2026-10-01_2026-10-07.xlsx; filename*=UTF-8''facility_2026-10-01_2026-10-07.xlsx", range),
    ).toBe('facility_2026-10-01_2026-10-07.xlsx');
    expect(fileNameOf('attachment; filename="report.xlsx"', range)).toBe('report.xlsx');
    expect(fileNameOf("attachment; filename*=UTF-8''x.xlsx", range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
    expect(fileNameOf(null, range)).toBe('facility_2026-10-01_2026-10-07.xlsx');
  });
});
