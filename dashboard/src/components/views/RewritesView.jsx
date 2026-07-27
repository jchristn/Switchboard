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
  MethodBadge,
  CopyableId,
  Icons,
} from '../ui';
import './ResourceViews.css';

const METHODS = ['', 'GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

const EMPTY_FORM = {
  endpointIdentifier: '',
  httpMethod: '',
  sourcePattern: '',
  targetPattern: '',
  sortOrder: 0,
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

export default function RewritesView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();

  const [rows, setRows] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
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
      const [rewrites, eps] = await Promise.all([apiClient.getRewrites(), apiClient.getEndpoints()]);
      setRows(Array.isArray(rewrites) ? rewrites : []);
      setEndpoints(Array.isArray(eps) ? eps : []);
    } catch (err) {
      setError(err.message || t('rewrites.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ ...EMPTY_FORM, endpointIdentifier: endpoints[0]?.identifier || '' });
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setForm({
      endpointIdentifier: row.endpointIdentifier || '',
      httpMethod: row.httpMethod || '',
      sourcePattern: row.sourcePattern || '',
      targetPattern: row.targetPattern || '',
      sortOrder: row.sortOrder ?? 0,
    });
    setFormOpen(true);
  };

  const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await apiClient.updateRewrite(editing.id, form);
        showSuccess(t('rewrites.updated'));
      } else {
        await apiClient.createRewrite(form);
        showSuccess(t('rewrites.created'));
      }
      setFormOpen(false);
      await load();
    } catch (err) {
      showError(`${t('rewrites.saveError')}: ${err.message}`);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      await apiClient.deleteRewrite(deleteRow.id);
      showSuccess(t('rewrites.deleted'));
      setDeleteRow(null);
      await load();
    } catch (err) {
      showError(`${t('rewrites.deleteError')}: ${err.message}`);
    } finally {
      setDeleting(false);
    }
  };

  const columns = [
    { key: 'endpointIdentifier', label: t('rewrites.endpoint'), sortable: true, filterable: true, mono: true, render: (r) => r.endpointIdentifier || '—' },
    {
      key: 'httpMethod',
      label: t('rewrites.method'),
      sortable: true,
      render: (r) => (r.httpMethod ? <MethodBadge method={r.httpMethod} /> : t('rewrites.anyMethod')),
    },
    { key: 'sourcePattern', label: t('rewrites.sourcePattern'), sortable: true, filterable: true, mono: true },
    { key: 'targetPattern', label: t('rewrites.targetPattern'), sortable: true, filterable: true, mono: true },
    { key: 'sortOrder', label: t('rewrites.sortOrder'), align: 'end', sortable: true },
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
        title={t('rewrites.title')}
        subtitle={t('rewrites.subtitle')}
        actions={
          <button type="button" className="btn btn-primary" onClick={openCreate}>
            <Icons.Plus size={16} />
            <span>{t('rewrites.add')}</span>
          </button>
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
        rowKey={(r) => r.id}
        loading={loading}
        error={error}
        onRetry={load}
        onRowClick={(r) => setViewRow(r)}
        emptyMessage={t('rewrites.empty')}
        emptyHint={t('rewrites.emptyHint')}
      />

      {/* Create / Edit form */}
      <Modal
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="medium"
        title={editing ? t('rewrites.editTitle') : t('rewrites.createTitle')}
        footer={
          <>
            <button type="button" className="btn btn-secondary" onClick={() => setFormOpen(false)}>
              {t('common.cancel')}
            </button>
            <button type="submit" form="rewrite-form" className="btn btn-primary" disabled={saving}>
              {saving ? t('common.saving') : editing ? t('common.update') : t('common.create')}
            </button>
          </>
        }
      >
        <form id="rewrite-form" onSubmit={submit}>
          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="rw-endpoint">{t('rewrites.endpoint')}</label>
              <select
                id="rw-endpoint"
                className="form-input"
                value={form.endpointIdentifier}
                onChange={(e) => setField('endpointIdentifier', e.target.value)}
                required
              >
                <option value="">{t('common.none')}</option>
                {endpoints.map((ep) => (
                  <option key={ep.guid || ep.identifier} value={ep.identifier}>
                    {ep.name ? `${ep.name} (${ep.identifier})` : ep.identifier}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="rw-method">{t('rewrites.method')}</label>
              <select
                id="rw-method"
                className="form-input"
                value={form.httpMethod}
                onChange={(e) => setField('httpMethod', e.target.value)}
              >
                {METHODS.map((m) => (
                  <option key={m || 'any'} value={m}>
                    {m || t('rewrites.anyMethod')}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="rw-source">{t('rewrites.sourcePattern')}</label>
            <input
              id="rw-source"
              className="form-input"
              value={form.sourcePattern}
              onChange={(e) => setField('sourcePattern', e.target.value)}
              required
              placeholder="/api/v1/{path}"
            />
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="rw-target">{t('rewrites.targetPattern')}</label>
            <input
              id="rw-target"
              className="form-input"
              value={form.targetPattern}
              onChange={(e) => setField('targetPattern', e.target.value)}
              required
              placeholder="/v1/{path}"
            />
          </div>
          <div className="form-group">
            <label className="form-label" htmlFor="rw-sort">{t('rewrites.sortOrder')}</label>
            <input
              id="rw-sort"
              type="number"
              className="form-input"
              value={form.sortOrder}
              onChange={(e) => setField('sortOrder', parseInt(e.target.value, 10) || 0)}
            />
          </div>
        </form>
      </Modal>

      {/* View detail */}
      <Modal open={!!viewRow} onClose={() => setViewRow(null)} size="medium" title={t('rewrites.viewTitle')}>
        {viewRow && (
          <div className="rv-detail-grid">
            <DetailItem label={t('rewrites.id')} full mono>
              <CopyableId value={viewRow.id} />
            </DetailItem>
            <DetailItem label={t('rewrites.endpoint')} mono>{viewRow.endpointIdentifier || '—'}</DetailItem>
            <DetailItem label={t('rewrites.method')}>
              {viewRow.httpMethod ? <MethodBadge method={viewRow.httpMethod} /> : t('rewrites.anyMethod')}
            </DetailItem>
            <DetailItem label={t('rewrites.sourcePattern')} full mono>{viewRow.sourcePattern}</DetailItem>
            <DetailItem label={t('rewrites.targetPattern')} full mono>{viewRow.targetPattern}</DetailItem>
            <DetailItem label={t('rewrites.sortOrder')}>{viewRow.sortOrder ?? 0}</DetailItem>
          </div>
        )}
      </Modal>

      <JsonViewerModal
        open={!!jsonRow}
        onClose={() => setJsonRow(null)}
        data={jsonRow}
        id={jsonRow?.id}
        title={t('rewrites.viewTitle')}
      />

      <ConfirmModal
        open={!!deleteRow}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('rewrites.deleteTitle')}
        message={t('rewrites.deleteMessage')}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />
    </div>
  );
}
