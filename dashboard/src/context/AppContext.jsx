import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import PropTypes from 'prop-types';

const AppContext = createContext(null);

export function AppProvider({ children }) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    return localStorage.getItem('switchboard_sidebar_collapsed') === 'true';
  });
  const [theme, setTheme] = useState(() => {
    return localStorage.getItem('switchboard_theme') || 'light';
  });
  const [notifications, setNotifications] = useState([]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('switchboard_theme', theme);
  }, [theme]);

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed(prev => {
      const newValue = !prev;
      localStorage.setItem('switchboard_sidebar_collapsed', String(newValue));
      return newValue;
    });
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme(prev => prev === 'light' ? 'dark' : 'light');
  }, []);

  const addNotification = useCallback((message, type = 'info', duration = 5000) => {
    const id = Date.now();
    setNotifications(prev => [...prev, { id, message, type }]);

    if (duration > 0) {
      setTimeout(() => {
        setNotifications(prev => prev.filter(n => n.id !== id));
      }, duration);
    }

    return id;
  }, []);

  const removeNotification = useCallback((id) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  }, []);

  const showSuccess = useCallback((message) => {
    return addNotification(message, 'success');
  }, [addNotification]);

  const showError = useCallback((message) => {
    return addNotification(message, 'error', 10000);
  }, [addNotification]);

  const showWarning = useCallback((message) => {
    return addNotification(message, 'warning');
  }, [addNotification]);

  const value = {
    sidebarCollapsed,
    toggleSidebar,
    theme,
    toggleTheme,
    notifications,
    addNotification,
    removeNotification,
    showSuccess,
    showError,
    showWarning,
  };

  return (
    <AppContext.Provider value={value}>
      {children}
    </AppContext.Provider>
  );
}

AppProvider.propTypes = {
  children: PropTypes.node.isRequired,
};

export function useApp() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return context;
}

export default AppContext;
