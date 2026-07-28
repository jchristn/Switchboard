import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icons, CopyButton } from '../ui';
import LanguageSelector from './LanguageSelector';
import { useApp } from '../../context/AppContext';
import { useAuth } from '../../context/AuthContext';
import { metaForPath } from './navConfig';
import './Topbar.css';

const GITHUB_URL = 'https://github.com/jchristn/switchboard';

function Topbar() {
  const { t } = useTranslation();
  const location = useLocation();
  const { toggleSidebar, theme, toggleTheme } = useApp();
  const { serverUrl, currentUser, isAdmin, apiClient, disconnect } = useAuth();
  const [health, setHealth] = useState('checking');

  const meta = metaForPath(location.pathname);
  // Keep the browser tab title in sync with the active page; the page title/subtitle themselves are
  // rendered in the page workspace (PageHeader), so they are intentionally not repeated in the topbar.
  const title = meta?.titleKey ? t(meta.titleKey) : t('app.name');

  useEffect(() => {
    document.title = `${title} · ${t('app.name')}`;
  }, [title, t]);

  useEffect(() => {
    if (!apiClient) return undefined;
    let active = true;
    const check = async () => {
      try {
        await apiClient.getHealth();
        if (active) setHealth('healthy');
      } catch {
        if (active) setHealth('unreachable');
      }
    };
    check();
    const id = setInterval(check, 30000);
    return () => {
      active = false;
      clearInterval(id);
    };
  }, [apiClient]);

  const healthLabel =
    health === 'healthy'
      ? t('topbar.healthy')
      : health === 'unreachable'
        ? t('topbar.unreachable')
        : t('topbar.checking');

  return (
    <header className="sb-topbar">
      <div className="sb-topbar-left">
        <button
          type="button"
          className="sb-icon-btn"
          onClick={toggleSidebar}
          aria-label={t('nav.collapse')}
          title={t('nav.collapse')}
        >
          <Icons.Menu size={20} />
        </button>
      </div>

      <div className="sb-topbar-right">
        <span className={`sb-health sb-health--${health}`} title={healthLabel}>
          <span className="sb-health-dot" aria-hidden="true" />
          <span className="sb-health-label">{healthLabel}</span>
        </span>

        <span className="sb-topbar-chip">
          <span className="sb-chip-label">{t('topbar.server')}</span>
          <code className="sb-chip-value">{serverUrl}</code>
          <CopyButton value={serverUrl} title={t('topbar.copyServer')} size={14} />
        </span>

        {currentUser && (
          <span className={`sb-role-pill${isAdmin ? ' is-admin' : ''}`}>
            {isAdmin ? t('topbar.roleAdmin') : t('topbar.roleReadOnly')}
          </span>
        )}

        <a
          className="sb-icon-btn"
          href={GITHUB_URL}
          target="_blank"
          rel="noreferrer"
          aria-label={t('topbar.github')}
          title={t('topbar.github')}
        >
          <Icons.Github size={18} />
        </a>

        <LanguageSelector compact />

        <button
          type="button"
          className="sb-icon-btn"
          onClick={toggleTheme}
          aria-label={t('topbar.toggleTheme')}
          title={t('topbar.toggleTheme')}
        >
          {theme === 'dark' ? <Icons.Sun size={18} /> : <Icons.Moon size={18} />}
        </button>

        <button
          type="button"
          className="sb-icon-btn sb-icon-btn--danger"
          onClick={disconnect}
          aria-label={t('topbar.logout')}
          title={t('topbar.logout')}
        >
          <Icons.Logout size={18} />
        </button>
      </div>
    </header>
  );
}

export default Topbar;
