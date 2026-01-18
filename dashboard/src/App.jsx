import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from './context/AuthContext';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import OverviewView from './components/views/OverviewView';
import OriginsView from './components/views/OriginsView';
import EndpointsView from './components/views/EndpointsView';
import HistoryView from './components/views/HistoryView';
import SettingsView from './components/views/SettingsView';
import UsersView from './components/views/UsersView';
import CredentialsView from './components/views/CredentialsView';

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return children;
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<Login />} />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <Dashboard />
          </ProtectedRoute>
        }
      >
        <Route index element={<OverviewView />} />
        <Route path="origins" element={<OriginsView />} />
        <Route path="endpoints" element={<EndpointsView />} />
        <Route path="history" element={<HistoryView />} />
        <Route path="users" element={<UsersView />} />
        <Route path="credentials" element={<CredentialsView />} />
        <Route path="settings" element={<SettingsView />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;
