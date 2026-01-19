import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import DataTable from '../common/DataTable';
import Modal from '../common/Modal';
import ConfirmModal from '../common/ConfirmModal';
import './Views.css';

function CredentialsView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const [credentials, setCredentials] = useState([]);
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [showTokenModal, setShowTokenModal] = useState(false);
  const [newToken, setNewToken] = useState('');
  const [editingCredential, setEditingCredential] = useState(null);
  const [deleteConfirm, setDeleteConfirm] = useState({ show: false, credential: null });
  const [regenerateConfirm, setRegenerateConfirm] = useState({ show: false, credential: null });
  const [formData, setFormData] = useState({
    userGuid: '',
    name: '',
    description: '',
    active: true,
    isReadOnly: false,
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [credentialsData, usersData] = await Promise.all([
        apiClient.getCredentials(),
        apiClient.getUsers(),
      ]);
      setCredentials(credentialsData);
      setUsers(usersData);
    } catch (err) {
      showError('Failed to load data: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const getUserName = (userGuid) => {
    const user = users.find((u) => u.guid === userGuid);
    return user ? user.username : userGuid;
  };

  const handleAdd = () => {
    setEditingCredential(null);
    setFormData({
      userGuid: users.length > 0 ? users[0].guid : '',
      name: '',
      description: '',
      active: true,
      isReadOnly: false,
    });
    setShowModal(true);
  };

  const handleEdit = (credential) => {
    setEditingCredential(credential);
    setFormData({
      userGuid: credential.userGuid || '',
      name: credential.name || '',
      description: credential.description || '',
      active: credential.active !== false,
      isReadOnly: credential.isReadOnly || false,
    });
    setShowModal(true);
  };

  const handleDeleteClick = (credential) => {
    setDeleteConfirm({ show: true, credential });
  };

  const handleDeleteConfirm = async () => {
    const credential = deleteConfirm.credential;
    setDeleteConfirm({ show: false, credential: null });

    try {
      await apiClient.deleteCredential(credential.guid);
      showSuccess('Credential deleted successfully');
      loadData();
    } catch (err) {
      showError('Failed to delete credential: ' + err.message);
    }
  };

  const handleRegenerateClick = (credential) => {
    setRegenerateConfirm({ show: true, credential });
  };

  const handleRegenerateConfirm = async () => {
    const credential = regenerateConfirm.credential;
    setRegenerateConfirm({ show: false, credential: null });

    try {
      const updated = await apiClient.regenerateCredential(credential.guid);
      if (updated.bearerToken) {
        setNewToken(updated.bearerToken);
        setShowTokenModal(true);
      }
      showSuccess('Token regenerated successfully');
      loadData();
    } catch (err) {
      showError('Failed to regenerate token: ' + err.message);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingCredential) {
        await apiClient.updateCredential(editingCredential.guid, formData);
        showSuccess('Credential updated successfully');
      } else {
        const created = await apiClient.createCredential(formData);
        if (created.bearerToken) {
          setNewToken(created.bearerToken);
          setShowTokenModal(true);
        }
        showSuccess('Credential created successfully');
      }
      setShowModal(false);
      loadData();
    } catch (err) {
      showError('Failed to save credential: ' + err.message);
    }
  };

  const copyToken = () => {
    navigator.clipboard.writeText(newToken);
    showSuccess('Token copied to clipboard');
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString();
  };

  const columns = [
    { key: 'name', label: 'Name', render: (value) => value || '-' },
    {
      key: 'userGuid',
      label: 'User',
      render: (value) => getUserName(value),
    },
    { key: 'description', label: 'Description', render: (value) => value || '-' },
    {
      key: 'active',
      label: 'Status',
      render: (value, row) => (
        <div className="badge-group">
          <span className={`badge ${value !== false ? 'badge-success' : 'badge-danger'}`}>
            {value !== false ? 'Active' : 'Inactive'}
          </span>
          {row.isReadOnly && (
            <span className="badge badge-info">Read-only</span>
          )}
        </div>
      ),
    },
    {
      key: 'lastUsedUtc',
      label: 'Last Used',
      render: (value) => formatDate(value),
    },
    {
      key: 'expiresUtc',
      label: 'Expires',
      render: (value) => formatDate(value),
    },
    {
      key: 'actions',
      label: 'Actions',
      render: (_, row) => (
        <div className="table-actions">
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => handleEdit(row)}
            title="Edit credential"
          >
            Edit
          </button>
          <button
            className="btn btn-warning btn-sm"
            onClick={() => handleRegenerateClick(row)}
            title="Regenerate token"
          >
            Regenerate
          </button>
          <button
            className="btn btn-danger btn-sm"
            onClick={() => handleDeleteClick(row)}
            title="Delete credential"
          >
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
        <h2 className="view-title">Credentials</h2>
        <button className="btn btn-primary" onClick={handleAdd}>
          Add Credential
        </button>
      </div>

      <DataTable columns={columns} data={credentials} emptyMessage="No credentials configured" />

      {showModal && (
        <Modal
          title={editingCredential ? 'Edit Credential' : 'Add Credential'}
          onClose={() => setShowModal(false)}
        >
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label className="form-label">User</label>
              <select
                className="form-input"
                value={formData.userGuid}
                onChange={(e) => setFormData({ ...formData, userGuid: e.target.value })}
                required
                disabled={!!editingCredential}
              >
                <option value="">Select a user...</option>
                {users.map((user) => (
                  <option key={user.guid} value={user.guid}>
                    {user.username}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label">Name</label>
              <input
                type="text"
                className="form-input"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="e.g., API Key for CI/CD"
              />
            </div>
            <div className="form-group">
              <label className="form-label">Description</label>
              <textarea
                className="form-input"
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                rows={3}
              />
            </div>
            <div className="form-group">
              <label className="form-checkbox">
                <input
                  type="checkbox"
                  checked={formData.active}
                  onChange={(e) => setFormData({ ...formData, active: e.target.checked })}
                />
                Active
              </label>
            </div>
            <div className="form-group">
              <label className="form-checkbox">
                <input
                  type="checkbox"
                  checked={formData.isReadOnly}
                  onChange={(e) => setFormData({ ...formData, isReadOnly: e.target.checked })}
                />
                Read-only API access (can only read data, cannot create/update/delete)
              </label>
            </div>
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary">
                {editingCredential ? 'Update' : 'Create'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {showTokenModal && (
        <Modal title="Bearer Token" onClose={() => setShowTokenModal(false)}>
          <div className="token-display">
            <p className="token-warning">
              This token will only be shown once. Please copy it now and store it securely.
            </p>
            <div className="token-value">
              <input
                type="text"
                value={newToken}
                readOnly
                onClick={(e) => e.target.select()}
              />
            </div>
            <div className="modal-actions">
              <button className="btn btn-primary" onClick={copyToken}>
                Copy to Clipboard
              </button>
              <button className="btn btn-secondary" onClick={() => setShowTokenModal(false)}>
                Close
              </button>
            </div>
          </div>
        </Modal>
      )}

      {deleteConfirm.show && (
        <ConfirmModal
          title="Delete Credential"
          message="Are you sure you want to delete this credential? Any applications using this token will lose access."
          entityName={deleteConfirm.credential?.name || 'Unnamed credential'}
          confirmLabel="Delete"
          onConfirm={handleDeleteConfirm}
          onClose={() => setDeleteConfirm({ show: false, credential: null })}
        />
      )}

      {regenerateConfirm.show && (
        <ConfirmModal
          title="Regenerate Token"
          message="Are you sure you want to regenerate this token? The old token will immediately stop working."
          entityName={regenerateConfirm.credential?.name || 'Unnamed credential'}
          confirmLabel="Regenerate"
          confirmVariant="warning"
          onConfirm={handleRegenerateConfirm}
          onClose={() => setRegenerateConfirm({ show: false, credential: null })}
        />
      )}
    </div>
  );
}

export default CredentialsView;
