import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useGetAreasQuery } from '../../shared/api/adminSetupApi';
import { freshOnOpen } from '../../shared/api/baseApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { nothingToPrint, perSheet, printableSigns, sheetsOf, summaryLine, type SignSize } from './printRules';
import { useGetPrintAreasQuery } from './printSignsApi';
import { SignCard } from './SignCard';
import './print.css';

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

  return (
    <main className="print-page">
      <div className="print-controls">
        <Link to={which && which !== 'all' ? `/admin/areas/${which}` : '/admin/areas'}>
          ‹ กลับไปหน้าจัดการพื้นที่และป้าย
        </Link>
        <h1>{heading ? `พิมพ์ป้าย QR · ${heading}` : 'พิมพ์ป้าย QR'}</h1>
        <div className="print-options">
          <div className="app-field">
            <label htmlFor="print-area">พื้นที่</label>
            <select
              id="print-area"
              className="app-input"
              value={which}
              onChange={(event) => setParams({ area: event.target.value })}
            >
              <option value="" disabled>
                เลือกพื้นที่
              </option>
              {activeAreas.map((area) => (
                <option key={area.id} value={String(area.id)}>
                  {`${area.code} ${area.name}`}
                </option>
              ))}
              <option value="all">{`ทุกพื้นที่ (${activeAreas.length})`}</option>
            </select>
          </div>
          <fieldset className="print-size">
            <legend>ขนาดป้าย</legend>
            <label>
              <input type="radio" name="print-size" checked={size === 'small'} onChange={() => setSize('small')} />
              {' เล็ก (6 ต่อแผ่น)'}
            </label>
            <label>
              <input type="radio" name="print-size" checked={size === 'A5'} onChange={() => setSize('A5')} />
              {' A5 (2 ต่อแผ่น)'}
            </label>
          </fieldset>
          <button
            type="button"
            className="app-button app-button--primary"
            disabled={signs.length === 0 || print.isFetching}
            onClick={() => void printNow()}
          >
            พิมพ์
          </button>
        </div>
        {signs.length > 0 && <p>{summaryLine(signs, size)}</p>}
        {signId !== undefined && which !== 'all' && (
          <p className="app-muted">
            พิมพ์ป้ายเดียว <Link to={`/admin/print?area=${which}`}>พิมพ์ทั้งพื้นที่ ›</Link>
          </p>
        )}
        <p className="app-muted">{`ที่อยู่บนป้าย: ${window.location.origin}`}</p>
        {window.location.protocol !== 'https:' && (
          <p className="app-note app-note--warn">
            ที่อยู่นี้ไม่ใช่ https มือถือจะอ่านตำแหน่งไม่ได้ตอนสแกน พิมพ์จากที่อยู่ https ของเซิร์ฟเวอร์
          </p>
        )}
        {print.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(print.error)}
          </p>
        )}
        {which === '' && <p className="app-note app-note--info">เลือกพื้นที่ที่จะพิมพ์</p>}
        {details && signs.length === 0 && <p className="app-note app-note--warn">{nothingToPrint(details, signId)}</p>}
      </div>
      {sheetsOf(signs, perSheet[size]).map((sheet, index) => (
        <section key={index} className={`print-sheet print-sheet--${size}`} aria-label={`แผ่นที่ ${index + 1}`}>
          {sheet.map((sign) => (
            <SignCard key={sign.key} sign={sign} />
          ))}
        </section>
      ))}
      <p className="app-muted print-controls">
        ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก · ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย
      </p>
    </main>
  );
}
