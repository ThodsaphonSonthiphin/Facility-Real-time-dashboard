import React from 'react';
import { Search, RefreshCw, Building2, X } from 'lucide-react';
import { useDashboard } from '../hooks/useDashboard';
import { KpiSummaryBar } from './KpiSummaryBar';
import { ServicePointCard } from './ServicePointCard';

interface DashboardViewProps {
  onOpenMobileScanner?: (token: string) => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({ onOpenMobileScanner }) => {
  const {
    points,
    rawPointsCount,
    isLoading,
    error,
    kpis,
    selectedStatusFilter,
    setSelectedStatusFilter,
    searchQuery,
    setSearchQuery,
    lastUpdatedPointId,
    isSignalRConnected,
    refresh
  } = useDashboard();

  return (
    <div style={styles.page}>
      {/* Top Navigation Bar */}
      <header className="dashboard-header">
        <div className="dashboard-header-left">
          <div className="dashboard-brand-icon" style={styles.brandIcon}>
            <Building2 size={24} color="#38bdf8" />
          </div>
          <div>
            <h1 className="dashboard-header-title">Facility Real-time Dashboard</h1>
            <p className="dashboard-header-subtitle">ระบบติดตามงานทำความสะอาดและสุขอนามัยแบบเรียลไทม์</p>
          </div>
        </div>

        <div className="dashboard-header-right">
          {/* SignalR Live Indicator */}
          <div
            className="dashboard-live-badge"
            style={{
              ...styles.liveBadge,
              borderColor: isSignalRConnected ? 'rgba(34, 197, 94, 0.3)' : 'rgba(249, 115, 22, 0.3)',
              background: isSignalRConnected ? 'rgba(34, 197, 94, 0.1)' : 'rgba(249, 115, 22, 0.1)'
            }}
          >
            <span
              style={{
                ...styles.liveDot,
                backgroundColor: isSignalRConnected ? '#22c55e' : '#f97316',
                boxShadow: isSignalRConnected ? '0 0 8px #22c55e' : 'none'
              }}
            />
            <span
              className="dashboard-live-text"
              style={{ color: isSignalRConnected ? '#4ade80' : '#fb923c', fontSize: '13px', fontWeight: 600 }}
            >
              {isSignalRConnected ? 'SignalR Live' : 'Connecting...'}
            </span>
          </div>

          <button
            onClick={refresh}
            style={styles.refreshBtn}
            title="รีเฟรชข้อมูล"
          >
            <RefreshCw size={16} />
          </button>
        </div>
      </header>

      {/* Main Container */}
      <main className="dashboard-content">
        {/* KPI Summary Bar */}
        <KpiSummaryBar
          kpis={kpis}
          activeFilter={selectedStatusFilter}
          onSelectFilter={setSelectedStatusFilter}
        />

        {/* Toolbar: Search and Filter Info */}
        <div className="dashboard-toolbar">
          <div className="dashboard-search-wrapper" style={styles.searchWrapper}>
            <Search size={18} style={styles.searchIcon} />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="ค้นหาชื่อจุดบริการ, ชั้น, โซน หรือชื่อผู้สแกน..."
              style={styles.searchInput}
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => setSearchQuery('')}
                style={styles.clearSearchInputBtn}
                title="ล้างคำค้นหา"
              >
                <X size={16} />
              </button>
            )}
          </div>

          {(selectedStatusFilter !== 'All' || searchQuery.trim() !== '') && (
            <div style={styles.activeFilterNotice}>
              <span>
                กำลังกรอง:
                {selectedStatusFilter !== 'All' && <span> สถานะ <strong>{selectedStatusFilter}</strong></span>}
                {selectedStatusFilter !== 'All' && searchQuery.trim() !== '' && <span> +</span>}
                {searchQuery.trim() !== '' && <span> คำค้น <strong>"{searchQuery}"</strong></span>}
                {' '}(พบ {points.length} จุด)
              </span>
              <button
                onClick={() => {
                  setSelectedStatusFilter('All');
                  setSearchQuery('');
                }}
                style={styles.clearFilterBtn}
              >
                ล้างตัวกรองทั้งหมด
              </button>
            </div>
          )}
        </div>

        {/* Error Alert */}
        {error && (
          <div style={styles.errorAlert}>
            <span>{error}</span>
          </div>
        )}

        {/* Cards Grid */}
        {isLoading ? (
          <div style={styles.loadingContainer}>
            <div style={styles.loadingSpinner} />
            <p style={{ color: '#94a3b8', marginTop: '16px' }}>กำลังโหลดข้อมูลจุดบริการ...</p>
          </div>
        ) : points.length === 0 ? (
          <div style={styles.emptyContainer}>
            <p style={{ fontSize: '16px', color: '#94a3b8' }}>
              {rawPointsCount === 0
                ? 'ยังไม่มีจุดบริการในระบบ'
                : 'ไม่พบจุดบริการที่ตรงกับเงื่อนไขการค้นหา'}
            </p>
          </div>
        ) : (
          <div className="dashboard-cards-grid">
            {points.map((point) => (
              <ServicePointCard
                key={point.id}
                point={point}
                isHighlighted={lastUpdatedPointId === point.id}
                onOpenScanner={onOpenMobileScanner}
              />
            ))}
          </div>
        )}
      </main>
    </div>
  );
};

const styles: Record<string, React.CSSProperties> = {
  page: {
    minHeight: '100vh',
    display: 'flex',
    flexDirection: 'column'
  },
  header: {
    padding: '20px 32px',
    background: 'rgba(15, 23, 42, 0.85)',
    borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    backdropFilter: 'blur(16px)',
    position: 'sticky',
    top: 0,
    zIndex: 10
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: '14px'
  },
  brandIcon: {
    width: '46px',
    height: '46px',
    borderRadius: '14px',
    background: 'linear-gradient(135deg, rgba(56, 189, 248, 0.2), rgba(14, 165, 233, 0.05))',
    border: '1px solid rgba(56, 189, 248, 0.3)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center'
  },
  headerTitle: {
    fontSize: '20px',
    fontWeight: 800,
    color: '#f8fafc',
    letterSpacing: '-0.02em'
  },
  headerSubtitle: {
    fontSize: '13px',
    color: '#94a3b8'
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px'
  },
  liveBadge: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    padding: '6px 14px',
    borderRadius: '9999px',
    border: '1px solid'
  },
  liveDot: {
    width: '8px',
    height: '8px',
    borderRadius: '50%'
  },
  refreshBtn: {
    width: '38px',
    height: '38px',
    borderRadius: '10px',
    background: 'rgba(255, 255, 255, 0.06)',
    border: '1px solid rgba(255, 255, 255, 0.1)',
    color: '#f8fafc',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    cursor: 'pointer'
  },
  content: {
    flex: 1,
    padding: '28px 32px',
    maxWidth: '1600px',
    margin: '0 auto',
    width: '100%'
  },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '20px',
    gap: '16px',
    flexWrap: 'wrap'
  },
  searchWrapper: {
    position: 'relative',
    flex: '1',
    maxWidth: '460px',
    display: 'flex',
    alignItems: 'center'
  },
  searchIcon: {
    position: 'absolute',
    left: '14px',
    color: '#64748b'
  },
  searchInput: {
    width: '100%',
    padding: '12px 38px 12px 42px',
    background: 'rgba(30, 41, 59, 0.6)',
    border: '1px solid rgba(255, 255, 255, 0.08)',
    borderRadius: '12px',
    color: '#f8fafc',
    fontSize: '14px',
    outline: 'none',
    transition: 'border-color 0.2s'
  },
  clearSearchInputBtn: {
    position: 'absolute',
    right: '12px',
    background: 'transparent',
    border: 'none',
    color: '#94a3b8',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '4px'
  },
  activeFilterNotice: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    fontSize: '13px',
    color: '#94a3b8',
    background: 'rgba(56, 189, 248, 0.1)',
    border: '1px solid rgba(56, 189, 248, 0.25)',
    padding: '6px 12px',
    borderRadius: '8px'
  },
  clearFilterBtn: {
    background: 'transparent',
    border: 'none',
    color: '#38bdf8',
    fontSize: '12px',
    fontWeight: 600,
    cursor: 'pointer',
    textDecoration: 'underline'
  },
  cardsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
    gap: '18px'
  },
  loadingContainer: {
    padding: '60px',
    textAlign: 'center',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center'
  },
  loadingSpinner: {
    width: '36px',
    height: '36px',
    borderRadius: '50%',
    border: '3px solid rgba(56, 189, 248, 0.2)',
    borderTopColor: '#38bdf8',
    animation: 'spin 0.8s linear infinite'
  },
  emptyContainer: {
    padding: '80px',
    textAlign: 'center',
    background: 'rgba(30, 41, 59, 0.4)',
    borderRadius: '18px',
    border: '1px dashed rgba(255, 255, 255, 0.08)'
  },
  errorAlert: {
    padding: '14px 18px',
    background: 'rgba(239, 68, 68, 0.15)',
    border: '1px solid rgba(239, 68, 68, 0.3)',
    borderRadius: '12px',
    color: '#fca5a5',
    fontSize: '14px',
    marginBottom: '20px'
  }
};
