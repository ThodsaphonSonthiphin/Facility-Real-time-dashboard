import { useState } from 'react';
import { useLocation } from 'react-router';
import { useAppSelector } from '../../../store/hooks';
import type { CleanerGuide, CleanerGuideKey, CleanerTabKey } from './type';

const guides: Record<CleanerGuideKey, CleanerGuide> = {
  'check-in': {
    title: 'สแกนป้ายลงเวลาด้วยกล้องมือถือ',
    detail: 'เปิดกล้องมือถือสแกนป้าย QR ที่จุดลงเวลา เพื่อเข้างาน ออกพัก หรือเลิกงาน',
  },
  'scan-qr': {
    title: 'สแกนป้าย QR ที่จุดทำความสะอาด',
    detail: 'เปิดกล้องมือถือสแกนป้าย QR ที่ติดอยู่หน้าจุดนั้น เพื่อส่งงานหรือแจ้งปัญหา',
  },
};

function activeTabOf(pathname: string): CleanerTabKey | null {
  if (pathname === '/my-work' || pathname.startsWith('/my-work/')) return 'my-work';
  if (pathname.startsWith('/check-in/')) return 'check-in';
  if (pathname.startsWith('/scan/')) return 'scan-qr';
  if (pathname === '/my-schedule' || pathname.startsWith('/my-schedule/')) return 'my-schedule';
  return null;
}

/**
 * Role-gated R-01 Option A bottom navigation state for Cleaner mobile routes.
 * Tapping ลงเวลา or สแกน QR on non-token routes toggles an inline physical-QR guidance banner
 * instead of fabricating token URLs (facility-0059, 0060).
 */
export function useCleanerBottomNav() {
  const isCleaner = useAppSelector((state) => state.auth.currentUser?.role === 'cleaner');
  const { pathname } = useLocation();
  const activeTab = activeTabOf(pathname);
  const [guideState, setGuideState] = useState<{ pathname: string; key: CleanerGuideKey } | null>(null);

  const openGuide = guideState?.pathname === pathname ? guideState.key : null;

  const toggleGuide = (key: CleanerGuideKey) => {
    setGuideState((prev) =>
      prev?.pathname === pathname && prev.key === key ? null : { pathname, key },
    );
  };

  return {
    isCleaner,
    activeTab,
    openGuide,
    guide: openGuide ? guides[openGuide] : null,
    toggleGuide,
  };
}
