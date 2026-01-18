import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import './Header.css';

function Header() {
  const { disconnect, serverUrl, currentUser, isAdmin } = useAuth();
  const { toggleSidebar, toggleTheme, theme } = useApp();

  return (
    <header className="header">
      <div className="header-left">
        <button className="header-menu-btn" onClick={toggleSidebar}>
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="3" y1="12" x2="21" y2="12"></line>
            <line x1="3" y1="6" x2="21" y2="6"></line>
            <line x1="3" y1="18" x2="21" y2="18"></line>
          </svg>
        </button>
        <img src="/logo.png" alt="Switchboard" className="header-logo" />
        <h1 className="header-title">Switchboard</h1>
        <div className="header-badges">
          <span className="header-badge header-badge-server" title="Connected server">
            endpoint: {serverUrl}
          </span>
          {currentUser && (
            <>
              <span className="header-badge header-badge-user" title="Logged in user">
                user: {currentUser.firstName || currentUser.username}
              </span>
              <span className={`header-badge ${isAdmin ? 'header-badge-admin' : 'header-badge-readonly'}`}>
                role: {isAdmin ? 'Admin' : 'Read-only'}
              </span>
            </>
          )}
        </div>
      </div>

      <div className="header-right">
        <button className="header-btn" onClick={toggleTheme} title="Toggle theme">
          {theme === 'light' ? (
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
            </svg>
          ) : (
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="5"></circle>
              <line x1="12" y1="1" x2="12" y2="3"></line>
              <line x1="12" y1="21" x2="12" y2="23"></line>
              <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
              <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
              <line x1="1" y1="12" x2="3" y2="12"></line>
              <line x1="21" y1="12" x2="23" y2="12"></line>
              <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
              <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
            </svg>
          )}
        </button>
        <button className="header-btn header-logout" onClick={disconnect}>
          Logout
        </button>
      </div>
    </header>
  );
}

export default Header;
