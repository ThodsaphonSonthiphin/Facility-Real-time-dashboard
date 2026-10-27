import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { Button } from '../../shared/components/Button';
import { Field, FieldGroup, Select } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { focusRing, linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { nothingToPrint, perSheet, printableSigns, sheetsOf, summaryLine, type SignSize } from './printRules';
import { useGetPrintAreasQuery } from './printSignsApi';
import { SignCard } from './SignCard';

/**
 * facility-0021 item 6, wireframe D5: A4 sheets of QR signs for one Area (?area=<id>), one sign (&sign=<id>) or every
 * active Area (?area=all), printed by the browser. The controls and the app bar are hidden on paper.
 */
export function PrintSignsPage() {
  const [params, setParams] = useSearchParams();
  const which = params.get('area') ?? '';
  const signParam = params.get('sign');
  const signId = signParam ? Number(signParam) : undefined;
  const [size, setSize] = useState<SignSize>('small');
  const areas = useGetAreasQuery(undefined, freshOnOpen);
  const print = useGetPrintAreasQuery(which, { ...freshOnOpen, skip: which === '' });
  const details = print.currentData;
  const signs = details ? printableSigns(details, signId) : [];
  const activeAreas = areas.data?.filter((area) => area.isActive) ?? [];
  // A tab open for a while may hold a revoked token: ask the server again, then print what it answered
  const printNow = async () => {
    await print.refetch();
    window.print();
  };
  const heading =
    which === 'all' ? 'ทุกพื้นที่' : details?.[0] ? `${details[0].area.code} ${details[0].area.name}` : undefined;
  const sheets = sheetsOf(signs, perSheet[size]);

  return (
    <main className="flex flex-col gap-4 text-sm text-text print:gap-0">
      <div className="flex flex-col gap-2.5 print:hidden">
        <Link
          className={`self-start ${linkClass}`}
          to={which && which !== 'all' ? `/admin/areas/${which}` : '/admin/areas'}
        >
          ‹ กลับไปหน้าจัดการพื้นที่และป้าย
        </Link>
        <h1 className="text-xl font-bold">{heading ? `พิมพ์ป้าย QR · ${heading}` : 'พิมพ์ป้าย QR'}</h1>
        <div className="flex flex-wrap items-end gap-4">
          <Field label="พื้นที่">
            <Select size="desk" value={which} onChange={(event) => setParams({ area: event.target.value })}>
              <option value="" disabled>
                เลือกพื้นที่
              </option>
              {activeAreas.map((area) => (
                <option key={area.id} value={String(area.id)}>
                  {`${area.code} ${area.name}`}
                </option>
              ))}
              <option value="all">{`ทุกพื้นที่ (${activeAreas.length})`}</option>
            </Select>
          </Field>
          <FieldGroup legend="ขนาดป้าย">
            <div className="flex gap-4">
              {signSizes.map((each) => (
                <label key={each} className="inline-flex min-h-9 items-center gap-2">
                  <input
                    type="radio"
                    name="print-size"
                    className={`accent-primary ${focusRing}`}
                    checked={size === each}
                    onChange={() => setSize(each)}
                  />
                  {sizeLabel[each]}
                </label>
              ))}
            </div>
          </FieldGroup>
          <Button
            variant="primary"
            size="desk"
            disabled={signs.length === 0 || print.isFetching}
            onClick={() => void printNow()}
          >
            พิมพ์
          </Button>
        </div>
        {signs.length > 0 && <p>{summaryLine(signs, size)}</p>}
        {signId !== undefined && which !== 'all' && (
          <p className="text-muted">
            พิมพ์ป้ายเดียว{' '}
            <Link className={linkClass} to={`/admin/print?area=${which}`}>
              พิมพ์ทั้งพื้นที่ ›
            </Link>
          </p>
        )}
        <p className="text-muted">{`ที่อยู่บนป้าย: ${window.location.origin}`}</p>
        {window.location.protocol !== 'https:' && (
          <Note tone="warn">
            ที่อยู่นี้ไม่ใช่ https มือถือจะอ่านตำแหน่งไม่ได้ตอนสแกน พิมพ์จากที่อยู่ https ของเซิร์ฟเวอร์
          </Note>
        )}
        {print.error && <Note tone="error">{getErrorMessage(print.error)}</Note>}
        {which === '' && <Note tone="info">เลือกพื้นที่ที่จะพิมพ์</Note>}
        {details && signs.length === 0 && <Note tone="warn">{nothingToPrint(details, signId)}</Note>}
      </div>
      {sheets.map((sheet, index) => (
        <section
          key={index}
          className={`${sheetClass} ${sheetGrid[size]} ${index < sheets.length - 1 ? 'print:break-after-page' : ''}`}
          aria-label={`แผ่นที่ ${index + 1}`}
        >
          {sheet.map((sign) => (
            <SignCard key={sign.key} sign={sign} size={size} />
          ))}
        </section>
      ))}
      <div className="flex flex-col gap-1.5 print:hidden">
        <p className="text-muted">ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย</p>
        <MoreInfo summary="เกี่ยวกับ QR">ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก</MoreInfo>
      </div>
    </main>
  );
}

const signSizes: SignSize[] = ['small', 'A5'];

const sizeLabel: Record<SignSize, string> = {
  small: 'เล็ก (6 ต่อแผ่น)',
  A5: 'A5 (2 ต่อแผ่น)',
};

// One A4 sheet: 210 × 297 mm with an 8 mm margin, signs cut along the dashed lines. The sheet is measured in
// millimetres as printed before the move (facility-0087 rule 8); no token is in mm. On screen it floats on the page
const sheetClass = 'mx-auto grid h-[297mm] w-[210mm] bg-sign-paper p-[8mm] shadow-sm print:m-0 print:shadow-none';

const sheetGrid: Record<SignSize, string> = {
  small: 'grid-cols-2 grid-rows-3',
  A5: 'grid-cols-1 grid-rows-2',
};
