import { useState, useEffect, useCallback } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import {
  PageHeader,
  DataTable,
  TablePagination,
  ActionMenu,
  entityActions,
  Modal,
  ConfirmModal,
  JsonViewerModal,
  Badge,
  CopyableId,
  Icons,
} from '../ui';
import './ResourceViews.css';

const EMPTY_FORM = {
  username: '',
  email: '',
  firstName: '',
  lastName: '',
  isAdmin: false,
  active: true,
};

function DetailItem({ label, children, full = false, mono = false }) {
  return (
    <div className={`rv-detail-item ${full ? 'rv-detail-item--full' : ''}`.trim()}>
      <span className="rv-detail-label">{label}</span>
      <span className={`rv-detail-value ${mono ? 'rv-detail-value--mono' : ''}`.trim()}>
        {children}
      </span>
    </div>
  );
}

DetailItem.propTypes = {
  label: PropTypes.node,
  children: PropTypes.node,
  full: PropTypes.bool,
  mono: PropTypes.bool,
};

function fullName(row) {
  const parts = [row.firstName, row.lastName].filter(Boolean);
  return parts.length ? parts.join(' ') : '';
}

export default function UsersView() {
  const { t } = useTranslation();
  const { apiClient, isAdmin } = useAuth();
  const { showSuccess, showError } = useApp();

  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);

  const [viewRow, setViewRow] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [deleteRow, setDeleteRow] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await apiClient.getUsers();
      setRows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || t('users.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm(EMPTY_FORM);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setForm({
      username: row.username || '',
      email: row.email || '',
      firstName: row.firstName || '',
      lastName: row.lastName || '',
      isAdmin: row.isAdmin || false,
      active: row.active !== false,
    });
    setFormOpen(true);
  };

  const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await apiClient.updateUser(editing.guid, form);
        showSuccess(t('users.updated'));
      } else {
        await apiClient.createUser(form);
        showSuccess(t('users.created'));
      }
      setFormOpen(false);
      await load();
    } catch (err) {
      showError(`${t('users.saveError')}: ${err.message}`);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      await apiClient.deleteUser(deleteRow.guid);
      showSuccess(t('users.deleted'));
      setDeleteRow(null);
      await load();
    } catch (err) {
      showError(`${t('users.deleteError')}: ${err.message}`);
    } finally {
      setDeleting(false);
    }
  };

  const columns = [
    { key: 'username', label: t('users.username'), sortable: true, filterable: true, mono: true },
    { key: 'email', label: t('users.email'), sortable: true, filterable: true, render: (r) => r.email || '—' },
    {
      key: 'fullName',
      label: t('users.fullName'),
      sortable: true,
      sortValue: (r) => fullName(r),
      render: (r) => fullName(r) || '—',
    },
    {
      key: 'isAdmin',
      label: t('users.role'),
      sortable: true,
      sortValue: (r) => (r.isAdmin ? 1 : 0),
      render: (r) => <Badge tone={r.isAdmin ? 'accent' : 'neutral'}>{r.isAdmin ? t('users.admin') : t('topbar.roleReadOnly')}</Badge>,
    },
    {
      key: 'active',
      label: t('users.status'),
      sortable: true,
      sortValue: (r) => (r.active !== false ? 1 : 0),
      render: (r) => (
        <Badge tone={r.active !== false ? 'success' : 'danger'}>
          {r.active !== false ? t('status.active') : t('status.inactive')}
        </Badge>
      ),
    },
    {
      key: 'actions',
      label: '',
      isAction: true,
      align: 'end',
      width: '56px',
      render: (row) => (
        <ActionMenu
          items={entityActions(t, {
            onView: () => setViewRow(row),
            onViewJson: () => setJsonRow(row),
            onEdit: () => openEdit(row),
            onDelete: () => setDeleteRow(row),
            canEdit: isAdmin,
            canDelete: isAdmin,
          })}
        />
      ),
    },
  ];

  const start = (pageNumber - 1) * pageSize;
  const paged = rows.slice(start, start + pageSize);

  return (
    <div className="rv-view">
      <PageHeader
        title={t('users.title')}
        subtitle={t('users.subtitle')}
        actions={
          isAdmin ? (
            <button type="button" className="btn btn-primary" onClick={openCreate}>
              <Icons.Plus size={16} />
              <span>{t('users.add')}</span>
            </button>
          ) : null
        }
      />

      <TablePagination
        total={rows.length}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageNumber(1);
        }}
        onRefresh={load}
      />

      <DataTable
        columns={columns}
        rows={paged}
        rowKey={(r) => r.guid}
        loading={loading}
        error={error}
        onRetry={load}
        onRowClick={(r) => setViewRow(r)}
        emptyMessage={t('users.empty')}
        emptyHint={t('users.emptyHint')}
      />

      {/* Create / Edit form */}
      <Modal
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="medium"
        title={editing ? t('users.editTitle') : t('users.createTitle')}
        footer={
          <>
            <button type="button" className="btn btn-secondary" onClick={() => setFormOpen(false)}>
              {t('common.cancel')}
            </button>
            <button type="submit" form="user-form" className="btn btn-primary" disabled={saving}>
              {saving ? t('common.saving') : editing ? t('common.update') : t('common.create')}
            </button>
          </>
        }
      >
        <form id="user-form" onSubmit={submit}>
          <div className="form-group">
            <label className="form-label" htmlFor="u-username">{t('users.username')}</label>
            <input
              id="u-username"
              className="form-input"
              value={form.username}
              onChange={(e) => setField('username', e.target.value)}
              required
              disabled={!!editing}
            />
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="u-email">{t('users.email')}</label>
            <input
              id="u-email"
              type="email"
              className="form-input"
              value={form.email}
              onChange={(e) => setField('email', e.target.value)}
            />
          </div>
          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="u-first">{t('users.firstName')}</label>
              <input
                id="u-first"
                className="form-input"
                value={form.firstName}
                onChange={(e) => setField('firstName', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="u-last">{t('users.lastName')}</label>
              <input
                id="u-last"
                className="form-input"
                value={form.lastName}
                onChange={(e) => setField('lastName', e.target.value)}
              />
            </div>
          </div>
          <div className="form-group">
            <label className="rv-check">
              <input type="checkbox" checked={form.isAdmin} onChange={(e) => setField('isAdmin', e.target.checked)} />
              <span>{t('users.administrator')}</span>
            </label>
          </div>
          <div className="form-group">
            <label className="rv-check">
              <input type="checkbox" checked={form.active} onChange={(e) => setField('active', e.target.checked)} />
              <span>{t('users.active')}</span>
            </label>
          </div>
        </form>
      </Modal>

      {/* View detail */}
      <Modal open={!!viewRow} onClose={() => setViewRow(null)} size="medium" title={t('users.viewTitle')}>
        {viewRow && (
          <div className="rv-detail-grid">
            <DetailItem label={t('users.guid')} full mono>
              <CopyableId value={viewRow.guid} />
            </DetailItem>
            <DetailItem label={t('users.username')} mono>{viewRow.username}</DetailItem>
            <DetailItem label={t('users.email')}>{viewRow.email || '—'}</DetailItem>
            <DetailItem label={t('users.firstName')}>{viewRow.firstName || '—'}</DetailItem>
            <DetailItem label={t('users.lastName')}>{viewRow.lastName || '—'}</DetailItem>
            <DetailItem label={t('users.role')}>
              <Badge tone={viewRow.isAdmin ? 'accent' : 'neutral'}>
                {viewRow.isAdmin ? t('users.admin') : t('topbar.roleReadOnly')}
              </Badge>
            </DetailItem>
            <DetailItem label={t('users.status')}>
              <Badge tone={viewRow.active !== false ? 'success' : 'danger'}>
                {viewRow.active !== false ? t('status.active') : t('status.inactive')}
              </Badge>
            </DetailItem>
          </div>
        )}
      </Modal>

      <JsonViewerModal
        open={!!jsonRow}
        onClose={() => setJsonRow(null)}
        data={jsonRow}
        id={jsonRow?.guid}
        title={t('users.viewTitle')}
      />

      <ConfirmModal
        open={!!deleteRow}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('users.deleteTitle')}
        message={t('users.deleteMessage', { name: deleteRow?.username })}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />
    </div>
  );
}
