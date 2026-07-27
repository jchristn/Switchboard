import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icons } from '../ui';
import { NAV_GROUPS } from './navConfig';
import { useApp } from '../../context/AppContext';
import { useAuth } from '../../context/AuthContext';
import { useOnboarding } from '../../context/OnboardingContext';
import { APP_VERSION } from '../../version';
import './Sidebar.css';

function Sidebar() {
  const { t } = useTranslation();
  const { sidebarCollapsed } = useApp();
  const { isAdmin } = useAuth();
  const { startWizard, startTour } = useOnboarding();

  return (
    <aside className={`sb-sidebar${sidebarCollapsed ? ' is-collapsed' : ''}`}>
      <div className="sb-sidebar-brand">
        <img src="/logo.png" alt="" className="sb-sidebar-logo" />
        {!sidebarCollapsed && <span className="sb-sidebar-name">{t('app.name')}</span>}
      </div>

      <nav className="sb-sidebar-nav" aria-label={t('app.name')}>
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((it) => !it.adminOnly || isAdmin);
          if (items.length === 0) return null;
          return (
            <div className="sb-nav-group" key={group.sectionKey}>
              {!sidebarCollapsed && (
                <div className="sb-nav-group-label">{t(group.sectionKey)}</div>
              )}
              {items.map((item) => {
                const Ico = Icons[item.icon] || Icons.Server;
                return (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.end}
                    data-tour={`nav-${item.icon}`}
                    className={({ isActive }) => `sb-nav-item${isActive ? ' is-active' : ''}`}
                    title={sidebarCollapsed ? t(item.labelKey) : undefined}
                  >
                    <span className="sb-nav-icon">
                      <Ico size={18} />
                    </span>
                    {!sidebarCollapsed && <span className="sb-nav-label">{t(item.labelKey)}</span>}
                  </NavLink>
                );
              })}
            </div>
          );
        })}
      </nav>

      <div className="sb-sidebar-footer">
        {!sidebarCollapsed && (
          <>
            <button type="button" className="sb-footer-btn" onClick={startWizard}>
              {t('nav.setupWizard')}
            </button>
            <button type="button" className="sb-footer-btn" onClick={startTour}>
              {t('nav.takeTour')}
            </button>
            <div className="sb-footer-version">{t('nav.version', { version: APP_VERSION })}</div>
          </>
        )}
      </div>
    </aside>
  );
}

export default Sidebar;
