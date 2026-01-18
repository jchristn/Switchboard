import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import './Views.css';

function OverviewView() {
  const { apiClient } = useAuth();
  const { showError } = useApp();
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState({
    origins: 0,
    endpoints: 0,
    totalRequests: 0,
    failedRequests: 0,
    successRate: 0,
  });

  useEffect(() => {
    loadStats();
    const interval = setInterval(loadStats, 30000); // Refresh every 30 seconds
    return () => clearInterval(interval);
  }, []);

  const loadStats = async () => {
    try {
      const [origins, endpoints, historyStats] = await Promise.all([
        apiClient.getOrigins(),
        apiClient.getEndpoints(),
        apiClient.getHistoryStats(),
      ]);

      setStats({
        origins: origins.length,
        endpoints: endpoints.length,
        totalRequests: historyStats.totalRequests || 0,
        failedRequests: historyStats.failedRequests || 0,
        successRate: historyStats.successRate || 0,
      });
    } catch (err) {
      showError('Failed to load statistics: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="view-loading">
        <div className="spinner"></div>
        <p>Loading...</p>
      </div>
    );
  }

  return (
    <div className="view">
      <div className="view-header">
        <h2 className="view-title">Overview</h2>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon stat-icon-blue">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="2" width="20" height="8" rx="2" ry="2"></rect>
              <rect x="2" y="14" width="20" height="8" rx="2" ry="2"></rect>
            </svg>
          </div>
          <div className="stat-content">
            <p className="stat-value">{stats.origins}</p>
            <p className="stat-label">Origin Servers</p>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-purple">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path>
              <polyline points="22,6 12,13 2,6"></polyline>
            </svg>
          </div>
          <div className="stat-content">
            <p className="stat-value">{stats.endpoints}</p>
            <p className="stat-label">API Endpoints</p>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-green">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
            </svg>
          </div>
          <div className="stat-content">
            <p className="stat-value">{stats.totalRequests.toLocaleString()}</p>
            <p className="stat-label">Total Requests</p>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon stat-icon-red">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="15" y1="9" x2="9" y2="15"></line>
              <line x1="9" y1="9" x2="15" y2="15"></line>
            </svg>
          </div>
          <div className="stat-content">
            <p className="stat-value">{stats.failedRequests.toLocaleString()}</p>
            <p className="stat-label">Failed Requests</p>
          </div>
        </div>
      </div>

      <div className="overview-section">
        <div className="card">
          <div className="card-header">
            <h3 className="card-title">Success Rate</h3>
          </div>
          <div className="success-rate">
            <div className="success-rate-value">{stats.successRate}%</div>
            <div className="success-rate-bar">
              <div
                className="success-rate-fill"
                style={{ width: `${stats.successRate}%` }}
              ></div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default OverviewView;
