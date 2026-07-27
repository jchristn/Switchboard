import { useEffect, useRef } from 'react';
import { Outlet } from 'react-router-dom';
import Topbar from './common/Topbar';
import Sidebar from './common/Sidebar';
import Toast from './common/Toast';
import SetupWizard from './onboarding/SetupWizard';
import Tour from './onboarding/Tour';
import { useApp } from '../context/AppContext';
import { useAuth } from '../context/AuthContext';
import { useOnboarding } from '../context/OnboardingContext';
import './Dashboard.css';

function Dashboard() {
  const { notifications, removeNotification } = useApp();
  const { apiClient } = useAuth();
  const { setupCompleted, startWizard } = useOnboarding();
  const checkedRef = useRef(false);

  // On first run (config empty and setup not yet dismissed), auto-open the setup wizard.
  useEffect(() => {
    if (checkedRef.current || setupCompleted || !apiClient) return;
    checkedRef.current = true;
    let active = true;
    (async () => {
      try {
        const [origins, endpoints] = await Promise.all([
          apiClient.getOrigins(),
          apiClient.getEndpoints(),
        ]);
        const empty =
          (!Array.isArray(origins) || origins.length === 0) &&
          (!Array.isArray(endpoints) || endpoints.length === 0);
        if (active && empty) startWizard();
      } catch {
        /* if we can't tell, don't nag */
      }
    })();
    return () => {
      active = false;
    };
  }, [apiClient, setupCompleted, startWizard]);

  return (
    <div className="sb-shell">
      <Sidebar />
      <div className="sb-main-col">
        <Topbar />
        <main className="sb-content">
          <Outlet />
        </main>
      </div>
      <div className="sb-toast-container">
        {notifications.map((notification) => (
          <Toast
            key={notification.id}
            message={notification.message}
            type={notification.type}
            onClose={() => removeNotification(notification.id)}
          />
        ))}
      </div>
      <SetupWizard />
      <Tour />
    </div>
  );
}

export default Dashboard;
