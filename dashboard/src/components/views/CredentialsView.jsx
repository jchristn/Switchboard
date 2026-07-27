import { useState, useEffect, useCallback, createElement } from 'react';
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
  CopyButton,
  Icons,
} from '../ui';
import './ResourceViews.css';

const EMPTY_FORM = {
  userGuid: '',
  name: '',
  active: true,
  isReadOnly: false,
};

function maskToken(token) {
  if (!token) return '—';
  const str = String(token);
  if (str.length <= 4) return '••••';
  return `••••${str.slice(-4)}`;
}

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

export default function CredentialsView() {
  const { t } = useTranslation();
  const { apiClient, isAdmin } = useAuth();
  const { showSuccess, showError } = useApp();

  const [rows, setRows] = useState([]);
  const [users, setUsers] = useState([]);
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
  const [regenRow, setRegenRow] = useState(null);
  const [regenerating, setRegenerating] = useState(false);
  const [newToken, setNewToken] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [creds, userList] = await Promise.all([apiClient.getCredentials(), apiClient.getUsers()]);
      setRows(Array.isArray(creds) ? creds : []);
      setUsers(Array.isArray(userList) ? userList : []);
    } catch (err) {
      setError(err.message || t('credentials.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  const userName = useCallback(
    (guid) => {
      const u = users.find((x) => x.guid === guid);
      return u ? u.username : guid || '—';
    },
    [users]
  );

  const openCreate = () => {
    setEditing(null);
    setForm({ ...EMPTY_FORM, userGuid: users[0]?.guid || '' });
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setForm({
      userGuid: row.userGuid || '',
      name: row.name || '',
      active: row.active !== false,
      isReadOnly: row.isReadOnly || false,
    });
    setFormOpen(true);
  };

  const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await apiClient.updateCredential(editing.guid, form);
        showSuccess(t('credentials.updated'));
      } else {
        const created = await apiClient.createCredential(form);
        showSuccess(t('credentials.created'));
        if (created?.bearerToken) setNewToken(created.bearerToken);
      }
      setFormOpen(false);
      await load();
    } catch (err) {
      showError(`${t('credentials.saveError')}: ${err.message}`);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      await apiClient.deleteCredential(deleteRow.guid);
      showSuccess(t('credentials.deleted'));
      setDeleteRow(null);
      await load();
    } catch (err) {
      showError(`${t('credentials.deleteError')}: ${err.message}`);
    } finally {
      setDeleting(false);
    }
  };

  const confirmRegenerate = async () => {
    if (!regenRow) return;
    setRegenerating(true);
    try {
      const updated = await apiClient.regenerateCredential(regenRow.guid);
      showSuccess(t('credentials.regenerated'));
      setRegenRow(null);
      if (updated?.bearerToken) setNewToken(updated.bearerToken);
      await load();
    } catch (err) {
      showError(`${t('credentials.regenerateError')}: ${err.message}`);
    } finally {
      setRegenerating(false);
    }
  };

  const columns = [
    { key: 'name', label: t('credentials.name'), sortable: true, filterable: true, render: (r) => r.name || t('credentials.unnamed') },
    { key: 'userGuid', label: t('credentials.user'), sortable: true, sortValue: (r) => userName(r.userGuid), render: (r) => userName(r.userGuid) },
    { key: 'bearerToken', label: t('credentials.token'), mono: true, render: (r) => maskToken(r.bearerToken) },
    {
      key: 'status',
      label: t('credentials.status'),
      render: (r) => (
        <span className="rv-badge-row">
          <Badge tone={r.active !== false ? 'success' : 'danger'}>
            {r.active !== false ? t('status.active') : t('status.inactive')}
          </Badge>
          {r.isReadOnly && <Badge tone="info">{t('credentials.readOnly')}</Badge>}
        </span>
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
            extra: isAdmin
              ? [
                  {
                    key: 'regenerate',
                    label: t('credentials.regenerate'),
                    icon: createElement(Icons.Refresh, { size: 16 }),
                    onClick: () => setRegenRow(row),
                  },
                ]
              : [],
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
        title={t('credentials.title')}
        subtitle={t('credentials.subtitle')}
        actions={
          isAdmin ? (
            <button type="button" className="btn btn-primary" onClick={openCreate}>
              <Icons.Plus size={16} />
              <span>{t('credentials.add')}</span>
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
        emptyMessage={t('credentials.empty')}
        emptyHint={t('credentials.emptyHint')}
      />

      {/* Create / Edit form */}
      <Modal
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="medium"
        title={editing ? t('credentials.editTitle') : t('credentials.createTitle')}
        footer={
          <>
            <button type="button" className="btn btn-secondary" onClick={() => setFormOpen(false)}>
              {t('common.cancel')}
            </button>
            <button type="submit" form="credential-form" className="btn btn-primary" disabled={saving}>
              {saving ? t('common.saving') : editing ? t('common.update') : t('common.create')}
            </button>
          </>
        }
      >
        <form id="credential-form" onSubmit={submit}>
          <div className="form-group">
            <label className="form-label" htmlFor="c-user">{t('credentials.user')}</label>
            <select
              id="c-user"
              className="form-input"
              value={form.userGuid}
              onChange={(e) => setField('userGuid', e.target.value)}
              required
              disabled={!!editing}
            >
              <option value="">{t('credentials.selectUser')}</option>
              {users.map((u) => (
                <option key={u.guid} value={u.guid}>
                  {u.username}
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="c-name">{t('credentials.name')}</label>
            <input
              id="c-name"
              className="form-input"
              value={form.name}
              onChange={(e) => setField('name', e.target.value)}
            />
          </div>
          <div className="form-group">
            <label className="rv-check">
              <input type="checkbox" checked={form.active} onChange={(e) => setField('active', e.target.checked)} />
              <span>{t('credentials.active')}</span>
            </label>
          </div>
          <div className="form-group">
            <label className="rv-check">
              <input
                type="checkbox"
                checked={form.isReadOnly}
                onChange={(e) => setField('isReadOnly', e.target.checked)}
              />
              <span>{t('credentials.readOnlyAccess')}</span>
            </label>
          </div>
        </form>
      </Modal>

      {/* View detail */}
      <Modal open={!!viewRow} onClose={() => setViewRow(null)} size="medium" title={t('credentials.viewTitle')}>
        {viewRow && (
          <div className="rv-detail-grid">
            <DetailItem label={t('credentials.guid')} full mono>
              <CopyableId value={viewRow.guid} />
            </DetailItem>
            <DetailItem label={t('credentials.name')}>{viewRow.name || t('credentials.unnamed')}</DetailItem>
            <DetailItem label={t('credentials.user')}>{userName(viewRow.userGuid)}</DetailItem>
            <DetailItem label={t('credentials.token')} full mono>
              <span className="rv-token-box">
                <input className="form-input" readOnly value={viewRow.bearerToken || ''} onFocus={(e) => e.target.select()} />
                <CopyButton value={viewRow.bearerToken || ''} />
              </span>
            </DetailItem>
            <DetailItem label={t('credentials.status')}>
              <Badge tone={viewRow.active !== false ? 'success' : 'danger'}>
                {viewRow.active !== false ? t('status.active') : t('status.inactive')}
              </Badge>
            </DetailItem>
            <DetailItem label={t('credentials.readOnly')}>
              {viewRow.isReadOnly ? t('common.yes') : t('common.no')}
            </DetailItem>
          </div>
        )}
      </Modal>

      <JsonViewerModal
        open={!!jsonRow}
        onClose={() => setJsonRow(null)}
        data={jsonRow}
        id={jsonRow?.guid}
        title={t('credentials.viewTitle')}
      />

      <ConfirmModal
        open={!!deleteRow}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('credentials.deleteTitle')}
        message={t('credentials.deleteMessage', { name: deleteRow?.name || t('credentials.unnamed') })}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />

      <ConfirmModal
        open={!!regenRow}
        onCancel={() => setRegenRow(null)}
        onConfirm={confirmRegenerate}
        variant="warning"
        title={t('credentials.regenerateTitle')}
        message={t('credentials.regenerateMessage', { name: regenRow?.name || t('credentials.unnamed') })}
        confirmLabel={t('common.regenerate')}
        busy={regenerating}
      />

      {/* Show-once token */}
      <Modal
        open={!!newToken}
        onClose={() => setNewToken(null)}
        size="medium"
        title={t('credentials.tokenTitle')}
        footer={
          <button type="button" className="btn btn-primary" onClick={() => setNewToken(null)}>
            {t('common.close')}
          </button>
        }
      >
        <p className="rv-token-note">{t('credentials.tokenOnce')}</p>
        <div className="rv-token-box">
          <input className="form-input" readOnly value={newToken || ''} onFocus={(e) => e.target.select()} />
          <CopyButton value={newToken || ''} />
        </div>
      </Modal>
    </div>
  );
}
