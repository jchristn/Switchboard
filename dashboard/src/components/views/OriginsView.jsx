import { useState, useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import DataTable from '../common/DataTable';
import Modal from '../common/Modal';
import ConfirmModal from '../common/ConfirmModal';
import './Views.css';

function OriginsView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const location = useLocation();
  const [origins, setOrigins] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingOrigin, setEditingOrigin] = useState(null);
  const [deleteConfirm, setDeleteConfirm] = useState({ show: false, origin: null });
  const [formData, setFormData] = useState({
    identifier: '',
    name: '',
    hostname: '',
    port: 80,
    ssl: false,
    healthCheckIntervalMs: 5000,
    healthCheckMethod: 'HEAD',
    healthCheckUrl: '/',
    unhealthyThreshold: 2,
    healthyThreshold: 1,
    maxParallelRequests: 10,
    rateLimitRequestsThreshold: 30,
    captureRequestBody: false,
    captureResponseBody: false,
    captureRequestHeaders: true,
    captureResponseHeaders: true,
    maxCaptureRequestBodySize: 65536,
    maxCaptureResponseBodySize: 65536,
  });
  const pendingSelectIdentifier = useRef(location.state?.selectIdentifier || null);

  useEffect(() => {
    loadOrigins();
  }, []);

  // Handle navigation state to select origin by identifier
  useEffect(() => {
    if (pendingSelectIdentifier.current && origins.length > 0) {
      const origin = origins.find(o => o.identifier === pendingSelectIdentifier.current);
      if (origin) {
        handleEdit(origin);
      }
      pendingSelectIdentifier.current = null;
    }
  }, [origins]);

  const loadOrigins = async () => {
    try {
      const data = await apiClient.getOrigins();
      setOrigins(data);
    } catch (err) {
      showError('Failed to load origins: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setEditingOrigin(null);
    setFormData({
      identifier: '',
      name: '',
      hostname: '',
      port: 80,
      ssl: false,
      healthCheckIntervalMs: 5000,
      healthCheckMethod: 'HEAD',
      healthCheckUrl: '/',
      unhealthyThreshold: 2,
      healthyThreshold: 1,
      maxParallelRequests: 10,
      rateLimitRequestsThreshold: 30,
      captureRequestBody: false,
      captureResponseBody: false,
      captureRequestHeaders: true,
      captureResponseHeaders: true,
      maxCaptureRequestBodySize: 65536,
      maxCaptureResponseBodySize: 65536,
    });
    setShowModal(true);
  };

  const handleEdit = (origin) => {
    setEditingOrigin(origin);
    setFormData({
      identifier: origin.identifier || '',
      name: origin.name || '',
      hostname: origin.hostname || '',
      port: origin.port || 80,
      ssl: origin.ssl || false,
      healthCheckIntervalMs: origin.healthCheckIntervalMs || 5000,
      healthCheckMethod: origin.healthCheckMethod || 'HEAD',
      healthCheckUrl: origin.healthCheckUrl || '/',
      unhealthyThreshold: origin.unhealthyThreshold || 2,
      healthyThreshold: origin.healthyThreshold || 1,
      maxParallelRequests: origin.maxParallelRequests || 10,
      rateLimitRequestsThreshold: origin.rateLimitRequestsThreshold || 30,
      captureRequestBody: origin.captureRequestBody || false,
      captureResponseBody: origin.captureResponseBody || false,
      captureRequestHeaders: origin.captureRequestHeaders !== false,
      captureResponseHeaders: origin.captureResponseHeaders !== false,
      maxCaptureRequestBodySize: origin.maxCaptureRequestBodySize || 65536,
      maxCaptureResponseBodySize: origin.maxCaptureResponseBodySize || 65536,
    });
    setShowModal(true);
  };

  const handleDeleteClick = (origin) => {
    setDeleteConfirm({ show: true, origin });
  };

  const handleDeleteConfirm = async () => {
    const origin = deleteConfirm.origin;
    setDeleteConfirm({ show: false, origin: null });

    try {
      await apiClient.deleteOrigin(origin.guid);
      showSuccess('Origin deleted successfully');
      loadOrigins();
    } catch (err) {
      showError('Failed to delete origin: ' + err.message);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingOrigin) {
        await apiClient.updateOrigin(editingOrigin.guid, formData);
        showSuccess('Origin updated successfully');
      } else {
        await apiClient.createOrigin(formData);
        showSuccess('Origin created successfully');
      }
      setShowModal(false);
      loadOrigins();
    } catch (err) {
      showError('Failed to save origin: ' + err.message);
    }
  };

  const columns = [
    { key: 'identifier', label: 'Identifier' },
    { key: 'hostname', label: 'Hostname' },
    { key: 'port', label: 'Port' },
    {
      key: 'ssl',
      label: 'SSL',
      render: (value) => (
        <span className={`badge ${value ? 'badge-success' : 'badge-secondary'}`}>
          {value ? 'Yes' : 'No'}
        </span>
      ),
    },
    { key: 'maxParallelRequests', label: 'Max Parallel' },
    {
      key: 'actions',
      label: 'Actions',
      render: (_, row) => (
        <div className="table-actions">
          <button className="btn btn-secondary btn-sm" onClick={() => handleEdit(row)}>
            Edit
          </button>
          <button className="btn btn-danger btn-sm" onClick={() => handleDeleteClick(row)}>
            Delete
          </button>
        </div>
      ),
    },
  ];

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
        <h2 className="view-title">Origin Servers</h2>
        <button className="btn btn-primary" onClick={handleAdd}>
          Add Origin
        </button>
      </div>

      <DataTable columns={columns} data={origins} emptyMessage="No origin servers configured" />

      {showModal && (
        <Modal title={editingOrigin ? 'Edit Origin' : 'Add Origin'} onClose={() => setShowModal(false)} size="xlarge">
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label className="form-label">Identifier</label>
              <input
                type="text"
                className="form-input"
                value={formData.identifier}
                onChange={(e) => setFormData({ ...formData, identifier: e.target.value })}
                required
                disabled={!!editingOrigin}
              />
            </div>
            <div className="form-group">
              <label className="form-label">Name</label>
              <input
                type="text"
                className="form-input"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Hostname</label>
                <input
                  type="text"
                  className="form-input"
                  value={formData.hostname}
                  onChange={(e) => setFormData({ ...formData, hostname: e.target.value })}
                  required
                />
              </div>
              <div className="form-group">
                <label className="form-label">Port</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.port}
                  onChange={(e) => setFormData({ ...formData, port: parseInt(e.target.value) })}
                  required
                />
              </div>
            </div>
            <div className="form-group">
              <label className="form-checkbox">
                <input
                  type="checkbox"
                  checked={formData.ssl}
                  onChange={(e) => setFormData({ ...formData, ssl: e.target.checked })}
                />
                Use SSL/TLS
              </label>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Health Check URL</label>
                <input
                  type="text"
                  className="form-input"
                  value={formData.healthCheckUrl}
                  onChange={(e) => setFormData({ ...formData, healthCheckUrl: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Health Check Interval (ms)</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.healthCheckIntervalMs}
                  onChange={(e) => setFormData({ ...formData, healthCheckIntervalMs: parseInt(e.target.value) })}
                />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Max Parallel Requests</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.maxParallelRequests}
                  onChange={(e) => setFormData({ ...formData, maxParallelRequests: parseInt(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Rate Limit Threshold</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.rateLimitRequestsThreshold}
                  onChange={(e) => setFormData({ ...formData, rateLimitRequestsThreshold: parseInt(e.target.value) })}
                />
              </div>
            </div>

            <h4 style={{ marginTop: '24px', marginBottom: '16px' }}>Request History Capture</h4>
            <div className="form-row">
              <div className="form-group">
                <label className="form-checkbox">
                  <input
                    type="checkbox"
                    checked={formData.captureRequestHeaders}
                    onChange={(e) => setFormData({ ...formData, captureRequestHeaders: e.target.checked })}
                  />
                  Capture Request Headers
                </label>
              </div>
              <div className="form-group">
                <label className="form-checkbox">
                  <input
                    type="checkbox"
                    checked={formData.captureResponseHeaders}
                    onChange={(e) => setFormData({ ...formData, captureResponseHeaders: e.target.checked })}
                  />
                  Capture Response Headers
                </label>
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-checkbox">
                  <input
                    type="checkbox"
                    checked={formData.captureRequestBody}
                    onChange={(e) => setFormData({ ...formData, captureRequestBody: e.target.checked })}
                  />
                  Capture Request Body
                </label>
              </div>
              <div className="form-group">
                <label className="form-checkbox">
                  <input
                    type="checkbox"
                    checked={formData.captureResponseBody}
                    onChange={(e) => setFormData({ ...formData, captureResponseBody: e.target.checked })}
                  />
                  Capture Response Body
                </label>
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Max Capture Request Body (bytes)</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.maxCaptureRequestBodySize}
                  onChange={(e) => setFormData({ ...formData, maxCaptureRequestBodySize: parseInt(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Max Capture Response Body (bytes)</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.maxCaptureResponseBodySize}
                  onChange={(e) => setFormData({ ...formData, maxCaptureResponseBodySize: parseInt(e.target.value) })}
                />
              </div>
            </div>
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary">
                {editingOrigin ? 'Update' : 'Create'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {deleteConfirm.show && (
        <ConfirmModal
          title="Delete Origin"
          message="Are you sure you want to delete this origin server?"
          entityName={deleteConfirm.origin?.identifier}
          confirmLabel="Delete"
          onConfirm={handleDeleteConfirm}
          onClose={() => setDeleteConfirm({ show: false, origin: null })}
        />
      )}
    </div>
  );
}

export default OriginsView;
