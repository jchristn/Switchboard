import { Outlet } from 'react-router-dom';
import Header from './common/Header';
import Sidebar from './common/Sidebar';
import Toast from './common/Toast';
import { useApp } from '../context/AppContext';
import './Dashboard.css';

function Dashboard() {
  const { sidebarCollapsed, notifications, removeNotification } = useApp();

  return (
    <div className={`dashboard ${sidebarCollapsed ? 'sidebar-collapsed' : ''}`}>
      <Header />
      <Sidebar />
      <main className="dashboard-main">
        <Outlet />
      </main>
      <div className="toast-container">
        {notifications.map(notification => (
          <Toast
            key={notification.id}
            message={notification.message}
            type={notification.type}
            onClose={() => removeNotification(notification.id)}
          />
        ))}
      </div>
    </div>
  );
}

export default Dashboard;
