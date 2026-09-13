import { useState, useEffect } from 'react';
import { AuthProvider } from './features/auth/context/AuthContext';
import { useAuth } from './features/auth/hooks/useAuth';
import { LoginPage } from './features/auth/components/LoginPage';
import { ScanRecordPage } from './features/scan/components/ScanRecordPage';
import { DashboardView } from './features/dashboard/components/DashboardView';
import { LayoutDashboard, Smartphone } from 'lucide-react';

function AppContent() {
  const { currentUser, logout, isAuthenticated, isLoading } = useAuth();

  // Simple, fast path routing
  const [currentPath, setCurrentPath] = useState<string>(window.location.pathname);
  const [selectedScanToken, setSelectedScanToken] = useState<string>('token-restroom-m1');

  useEffect(() => {
    const handlePopState = () => {
      setCurrentPath(window.location.pathname);
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  const navigateTo = (path: string) => {
    window.history.pushState({}, '', path);
    setCurrentPath(path);
  };

  // Determine current route
  const isScanRoute = currentPath.startsWith('/scan');
  const tokenFromUrl = isScanRoute ? currentPath.split('/scan/')[1] || selectedScanToken : selectedScanToken;

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Dev Mode Navigation Switcher at Top */}
      <div className="dev-navbar" style={styles.navBar}>
        <div className="dev-navbar-left" style={styles.navLeft}>
          <span className="dev-navbar-brand-text" style={styles.navBrand}>🏢 Facility Real-time</span>
        </div>
        <div className="dev-navbar-center" style={styles.navCenter}>
          <button
            onClick={() => navigateTo('/dashboard')}
            className="dev-navbar-tab"
            style={{
              ...styles.navTab,
              background: !isScanRoute ? 'rgba(56, 189, 248, 0.2)' : 'transparent',
              color: !isScanRoute ? '#38bdf8' : '#94a3b8'
            }}
          >
            <LayoutDashboard size={16} />
            <span>จอ Dashboard</span>
          </button>

          <button
            onClick={() => navigateTo(`/scan/${tokenFromUrl}`)}
            className="dev-navbar-tab"
            style={{
              ...styles.navTab,
              background: isScanRoute ? 'rgba(56, 189, 248, 0.2)' : 'transparent',
              color: isScanRoute ? '#38bdf8' : '#94a3b8'
            }}
          >
            <Smartphone size={16} />
            <span>หน้าสแกนบนมือถือ</span>
          </button>
        </div>
        <div className="dev-navbar-right" style={styles.navRight}>
          {isAuthenticated ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span style={styles.userIndicator}>👤 {currentUser?.fullName}</span>
              <button onClick={logout} style={styles.logoutBtn}>
                ออก
              </button>
            </div>
          ) : (
            <button onClick={() => navigateTo('/login')} style={styles.loginBtn}>
              เข้าสู่ระบบ
            </button>
          )}
        </div>
      </div>

      {/* Main Page Routing */}
      <div style={{ flex: 1 }}>
        {isLoading ? (
          // ADR facility-0013: wait for the page-load refresh before deciding to show the login page
          <div style={styles.sessionCheck}>กำลังตรวจสอบการเข้าสู่ระบบ...</div>
        ) : !isAuthenticated ? (
          // ADR facility-0017 and facility-0018: scanning and the dashboard both need a login; return to the same page after it
          <LoginPage
            onSuccess={() => navigateTo(isScanRoute ? `/scan/${tokenFromUrl}` : '/dashboard')}
          />
        ) : isScanRoute ? (
          <ScanRecordPage
            qrToken={tokenFromUrl}
            currentUser={currentUser!}
            onLogout={logout}
            onOpenDashboard={() => navigateTo('/dashboard')}
          />
        ) : (
          <DashboardView
            onOpenMobileScanner={(token) => {
              setSelectedScanToken(token);
              navigateTo(`/scan/${token}`);
            }}
          />
        )}
      </div>
    </div>
  );
}

export function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

const styles: Record<string, React.CSSProperties> = {
  navBar: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '8px 24px',
    background: '#090d16',
    borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
    fontSize: '13px',
    zIndex: 100
  },
  navLeft: {
    display: 'flex',
    alignItems: 'center'
  },
  navBrand: {
    fontWeight: 700,
    color: '#f8fafc',
    fontSize: '13px'
  },
  navCenter: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px'
  },
  navTab: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    padding: '6px 14px',
    borderRadius: '8px',
    border: 'none',
    cursor: 'pointer',
    fontSize: '13px',
    fontWeight: 600,
    transition: 'all 0.15s'
  },
  navRight: {
    display: 'flex',
    alignItems: 'center'
  },
  userIndicator: {
    color: '#cbd5e1',
    fontSize: '12px',
    fontWeight: 600
  },
  loginBtn: {
    background: 'rgba(255, 255, 255, 0.08)',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    borderRadius: '6px',
    padding: '4px 10px',
    color: '#f8fafc',
    cursor: 'pointer',
    fontSize: '12px'
  },
  logoutBtn: {
    background: 'rgba(239, 68, 68, 0.15)',
    border: '1px solid rgba(239, 68, 68, 0.3)',
    borderRadius: '6px',
    padding: '4px 8px',
    color: '#fca5a5',
    cursor: 'pointer',
    fontSize: '11px'
  },
  sessionCheck: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '60vh',
    color: '#94a3b8',
    fontSize: '14px'
  }
};

export default App;
