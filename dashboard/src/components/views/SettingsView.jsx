import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import './Views.css';

function SettingsView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const [blockedHeaders, setBlockedHeaders] = useState([]);
  const [newHeader, setNewHeader] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadBlockedHeaders();
  }, []);

  const loadBlockedHeaders = async () => {
    try {
      const data = await apiClient.getBlockedHeaders();
      setBlockedHeaders(data);
    } catch (err) {
      showError('Failed to load blocked headers: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleAddHeader = async (e) => {
    e.preventDefault();
    if (!newHeader.trim()) return;

    try {
      await apiClient.createBlockedHeader({ headerName: newHeader.trim() });
      showSuccess('Header added to block list');
      setNewHeader('');
      loadBlockedHeaders();
    } catch (err) {
      showError('Failed to add header: ' + err.message);
    }
  };

  const handleRemoveHeader = async (header) => {
    try {
      await apiClient.deleteBlockedHeader(header.id);
      showSuccess('Header removed from block list');
      loadBlockedHeaders();
    } catch (err) {
      showError('Failed to remove header: ' + err.message);
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
        <h2 className="view-title">Settings</h2>
      </div>

      <div className="settings-grid">
        <div className="card">
          <div className="card-header">
            <h3 className="card-title">Blocked Headers</h3>
          </div>
          <p className="setting-description">
            Headers in this list will be stripped from requests before forwarding to origin servers.
          </p>
          <form className="add-header-form" onSubmit={handleAddHeader}>
            <input
              type="text"
              className="form-input"
              placeholder="Header name"
              value={newHeader}
              onChange={(e) => setNewHeader(e.target.value)}
            />
            <button type="submit" className="btn btn-primary">
              Add
            </button>
          </form>
          <div className="blocked-headers-list">
            {blockedHeaders.length === 0 ? (
              <p className="empty-message">No blocked headers configured</p>
            ) : (
              blockedHeaders.map((header) => (
                <div key={header.id} className="blocked-header-item">
                  <span>{header.headerName}</span>
                  <button
                    className="btn btn-danger btn-sm"
                    onClick={() => handleRemoveHeader(header)}
                  >
                    Remove
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default SettingsView;
