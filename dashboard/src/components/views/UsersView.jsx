import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import DataTable from '../common/DataTable';
import Modal from '../common/Modal';
import ConfirmModal from '../common/ConfirmModal';
import './Views.css';

function UsersView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingUser, setEditingUser] = useState(null);
  const [deleteConfirm, setDeleteConfirm] = useState({ show: false, user: null });
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    firstName: '',
    lastName: '',
    isAdmin: false,
    active: true,
  });

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = async () => {
    try {
      const data = await apiClient.getUsers();
      setUsers(data);
    } catch (err) {
      showError('Failed to load users: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = () => {
    setEditingUser(null);
    setFormData({
      username: '',
      email: '',
      firstName: '',
      lastName: '',
      isAdmin: false,
      active: true,
    });
    setShowModal(true);
  };

  const handleEdit = (user) => {
    setEditingUser(user);
    setFormData({
      username: user.username || '',
      email: user.email || '',
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      isAdmin: user.isAdmin || false,
      active: user.active !== false,
    });
    setShowModal(true);
  };

  const handleDeleteClick = (user) => {
    setDeleteConfirm({ show: true, user });
  };

  const handleDeleteConfirm = async () => {
    const user = deleteConfirm.user;
    setDeleteConfirm({ show: false, user: null });

    try {
      await apiClient.deleteUser(user.guid);
      showSuccess('User deleted successfully');
      loadUsers();
    } catch (err) {
      showError('Failed to delete user: ' + err.message);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingUser) {
        await apiClient.updateUser(editingUser.guid, formData);
        showSuccess('User updated successfully');
      } else {
        await apiClient.createUser(formData);
        showSuccess('User created successfully');
      }
      setShowModal(false);
      loadUsers();
    } catch (err) {
      showError('Failed to save user: ' + err.message);
    }
  };

  const columns = [
    { key: 'username', label: 'Username' },
    { key: 'email', label: 'Email' },
    {
      key: 'fullName',
      label: 'Name',
      render: (_, row) => {
        const parts = [row.firstName, row.lastName].filter(Boolean);
        return parts.length > 0 ? parts.join(' ') : '-';
      },
    },
    {
      key: 'isAdmin',
      label: 'Admin',
      render: (value) => (
        <span className={`badge ${value ? 'badge-warning' : 'badge-secondary'}`}>
          {value ? 'Yes' : 'No'}
        </span>
      ),
    },
    {
      key: 'active',
      label: 'Status',
      render: (value) => (
        <span className={`badge ${value !== false ? 'badge-success' : 'badge-danger'}`}>
          {value !== false ? 'Active' : 'Inactive'}
        </span>
      ),
    },
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
        <h2 className="view-title">Users</h2>
        <button className="btn btn-primary" onClick={handleAdd}>
          Add User
        </button>
      </div>

      <DataTable columns={columns} data={users} emptyMessage="No users configured" />

      {showModal && (
        <Modal title={editingUser ? 'Edit User' : 'Add User'} onClose={() => setShowModal(false)}>
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label className="form-label">Username</label>
              <input
                type="text"
                className="form-input"
                value={formData.username}
                onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                required
                disabled={!!editingUser}
              />
            </div>
            <div className="form-group">
              <label className="form-label">Email</label>
              <input
                type="email"
                className="form-input"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">First Name</label>
                <input
                  type="text"
                  className="form-input"
                  value={formData.firstName}
                  onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Last Name</label>
                <input
                  type="text"
                  className="form-input"
                  value={formData.lastName}
                  onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                />
              </div>
            </div>
            <div className="form-group">
              <label className="form-checkbox">
                <input
                  type="checkbox"
                  checked={formData.isAdmin}
                  onChange={(e) => setFormData({ ...formData, isAdmin: e.target.checked })}
                />
                Administrator
              </label>
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
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary">
                {editingUser ? 'Update' : 'Create'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {deleteConfirm.show && (
        <ConfirmModal
          title="Delete User"
          message="Are you sure you want to delete this user? All associated credentials will also be deleted."
          entityName={deleteConfirm.user?.username}
          confirmLabel="Delete"
          onConfirm={handleDeleteConfirm}
          onClose={() => setDeleteConfirm({ show: false, user: null })}
        />
      )}
    </div>
  );
}

export default UsersView;
