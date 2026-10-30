import { useState } from 'react';
import { flushSync } from 'react-dom';
import { useSearchParams } from 'react-router';
import { useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { nothingToPrint, perSheet, printableSigns, sheetsOf, summaryLine, type SignSize } from './printRules';
import { useGetPrintAreasQuery } from './printSignsApi';

export function usePrintSignsPage() {
  const [params, setParams] = useSearchParams();
  const which = params.get('area') ?? '';
  const signParam = params.get('sign');
  const signId = signParam ? Number(signParam) : undefined;
  const [size, setSize] = useState<SignSize>('small');
  const [printing, setPrinting] = useState(false);
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const print = useGetPrintAreasQuery(which, { ...freshOnOpen, skip: which === '' });
  const details = print.currentData;
  const signs = details ? printableSigns(details, signId) : [];
  const activeAreas = areas.data?.filter((area) => area.isActive) ?? [];
  const groups = Array.from(new Set(activeAreas.map((area) => area.buildingId))).map((id) => ({
    id,
    code: activeAreas.find((area) => area.buildingId === id)!.buildingCode,
    count: activeAreas.filter((area) => area.buildingId === id).length,
  }));
  const singleArea = which !== '' && which !== 'all' && !which.startsWith('building:');
  // A tab may hold revoked tokens. Commit the refreshed result to the DOM before opening the browser dialog.
  const printNow = async () => {
    setPrinting(true);
    const result = await print.refetch();
    flushSync(() => setPrinting(false));
    if (result.data && !result.error && printableSigns(result.data, signId).length > 0) {
      window.print();
    }
  };

  return {
    which, signId, size, setSize, areas, print, details, activeAreas, groups,
    sheets: sheetsOf(signs, perSheet[size]),
    summary: signs.length > 0 ? summaryLine(signs, size) : 'เลือกพื้นที่และขนาดแล้วกดพิมพ์',
    emptyMessage: details && signs.length === 0 ? nothingToPrint(details, signId) : undefined,
    disabled: signs.length === 0 || print.isFetching || printing,
    printing,
    singleArea,
    printNow,
    selectArea: (area: string) => setParams({ area }),
  };
}
