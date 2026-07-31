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
  HealthBadge,
  HealthHistogram,
  HealthDetailModal,
  Badge,
  CopyableId,
  Icons,
} from '../ui';
import './ResourceViews.css';

const EMPTY_FORM = {
  identifier: '',
  name: '',
  hostname: '',
  port: 80,
  ssl: false,
  healthCheckUrl: '/',
  healthCheckMethod: 'HEAD',
  healthCheckIntervalMs: 10000,
  unhealthyThreshold: 2,
  healthyThreshold: 1,
  maxParallelRequests: 10,
  rateLimitRequestsThreshold: 30,
  slowStartMs: 0,
  maxFailures: 5,
  ejectionDurationMs: 30000,
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

export default function OriginsView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
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

  // Live per-origin health keyed by GUID, refreshed on an interval separate from the config load.
  const [health, setHealth] = useState({});
  const [healthDetail, setHealthDetail] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await apiClient.getOrigins();
      setRows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || t('origins.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  const loadHealth = useCallback(async () => {
    try {
      const data = await apiClient.getOriginsHealth();
      const map = {};
      (Array.isArray(data) ? data : []).forEach((h) => {
        if (h && h.guid) map[h.guid] = h;
      });
      setHealth(map);
    } catch {
      // Health is best-effort telemetry; leave the last known map in place on a transient failure.
    }
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  // Poll health every 15s, matching the sibling dashboards.
  useEffect(() => {
    loadHealth();
    const id = setInterval(loadHealth, 15000);
    return () => clearInterval(id);
  }, [loadHealth]);

  // Keep the open detail modal's data fresh as health polls in.
  useEffect(() => {
    if (healthDetail && health[healthDetail.guid]) setHealthDetail(health[healthDetail.guid]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [health]);

  const openCreate = () => {
    setEditing(null);
    setForm(EMPTY_FORM);
    setFormOpen(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setForm({
      identifier: row.identifier || '',
      name: row.name || '',
      hostname: row.hostname || '',
      port: row.port ?? 80,
      ssl: row.ssl || false,
      healthCheckUrl: row.healthCheckUrl || '/',
      healthCheckMethod: row.healthCheckMethod || 'HEAD',
      healthCheckIntervalMs: row.healthCheckIntervalMs ?? 10000,
      unhealthyThreshold: row.unhealthyThreshold ?? 2,
      healthyThreshold: row.healthyThreshold ?? 1,
      maxParallelRequests: row.maxParallelRequests ?? 10,
      rateLimitRequestsThreshold: row.rateLimitRequestsThreshold ?? 30,
      slowStartMs: row.slowStartMs ?? 0,
      maxFailures: row.maxFailures ?? 5,
      ejectionDurationMs: row.ejectionDurationMs ?? 30000,
    });
    setFormOpen(true);
  };

  const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editing) {
        await apiClient.updateOrigin(editing.guid, form);
        showSuccess(t('origins.updated'));
      } else {
        await apiClient.createOrigin(form);
        showSuccess(t('origins.created'));
      }
      setFormOpen(false);
      await load();
    } catch (err) {
      showError(`${t('origins.saveError')}: ${err.message}`);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      await apiClient.deleteOrigin(deleteRow.guid);
      showSuccess(t('origins.deleted'));
      setDeleteRow(null);
      await load();
    } catch (err) {
      showError(`${t('origins.deleteError')}: ${err.message}`);
    } finally {
      setDeleting(false);
    }
  };

  const columns = [
    { key: 'identifier', label: t('origins.identifier'), sortable: true, filterable: true, mono: true },
    { key: 'name', label: t('origins.name'), sortable: true, filterable: true, render: (r) => r.name || '—' },
    { key: 'hostname', label: t('origins.hostname'), sortable: true, filterable: true, mono: true },
    { key: 'port', label: t('origins.port'), align: 'end', sortable: true },
    {
      key: 'ssl',
      label: t('origins.ssl'),
      render: (r) => <Badge tone={r.ssl ? 'success' : 'neutral'}>{r.ssl ? t('common.yes') : t('common.no')}</Badge>,
    },
    {
      key: 'health',
      label: t('origins.health'),
      sortable: true,
      sortValue: (r) => {
        const h = health[r.guid];
        const val = h ? h.isHealthy : r.healthy;
        return val == null ? -1 : val ? 1 : 0;
      },
      render: (r) => {
        const h = health[r.guid];
        if (!h) {
          return r.healthy == null ? (
            <Badge tone="neutral">{t('common.unknown')}</Badge>
          ) : (
            <HealthBadge healthy={r.healthy} />
          );
        }
        const openDetail = (e) => {
          e.stopPropagation();
          setHealthDetail(h);
        };
        return (
          <span
            className="sb-health-cell"
            role="button"
            tabIndex={0}
            title={t('health.details')}
            onClick={openDetail}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') openDetail(e);
            }}
          >
            <HealthBadge healthy={!!h.isHealthy} />
            <HealthHistogram history={h.history || []} slots={10} width={90} height={18} />
          </span>
        );
      },
    },
    { key: 'maxParallelRequests', label: t('origins.maxParallel'), align: 'end', sortable: true },
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
        title={t('origins.title')}
        subtitle={t('origins.subtitle')}
        actions={
          <button type="button" className="btn btn-primary" onClick={openCreate}>
            <Icons.Plus size={16} />
            <span>{t('origins.add')}</span>
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
        rowKey={(r) => r.guid}
        loading={loading}
        error={error}
        onRetry={load}
        onRowClick={(r) => setViewRow(r)}
        emptyMessage={t('origins.empty')}
        emptyHint={t('origins.emptyHint')}
      />

      {/* Create / Edit form */}
      <Modal
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="xl"
        title={editing ? t('origins.editTitle') : t('origins.createTitle')}
        footer={
          <>
            <button type="button" className="btn btn-secondary" onClick={() => setFormOpen(false)}>
              {t('common.cancel')}
            </button>
            <button type="submit" form="origin-form" className="btn btn-primary" disabled={saving}>
              {saving ? t('common.saving') : editing ? t('common.update') : t('common.create')}
            </button>
          </>
        }
      >
        <form id="origin-form" onSubmit={submit}>
          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-identifier">{t('origins.identifier')}</label>
              <input
                id="o-identifier"
                className="form-input"
                value={form.identifier}
                onChange={(e) => setField('identifier', e.target.value)}
                required
                disabled={!!editing}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-name">{t('origins.name')}</label>
              <input id="o-name" className="form-input" value={form.name} onChange={(e) => setField('name', e.target.value)} />
            </div>
          </div>

          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-hostname">{t('origins.hostname')}</label>
              <input
                id="o-hostname"
                className="form-input"
                value={form.hostname}
                onChange={(e) => setField('hostname', e.target.value)}
                required
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-port">{t('origins.port')}</label>
              <input
                id="o-port"
                type="number"
                className="form-input"
                value={form.port}
                onChange={(e) => setField('port', parseInt(e.target.value, 10) || 0)}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label className="rv-check">
              <input type="checkbox" checked={form.ssl} onChange={(e) => setField('ssl', e.target.checked)} />
              <span>{t('origins.useSsl')}</span>
            </label>
          </div>

          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-hcurl">{t('origins.healthCheckUrl')}</label>
              <input
                id="o-hcurl"
                className="form-input"
                value={form.healthCheckUrl}
                onChange={(e) => setField('healthCheckUrl', e.target.value)}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-hcmethod">{t('origins.healthCheckMethod')}</label>
              <select
                id="o-hcmethod"
                className="form-input"
                value={form.healthCheckMethod}
                onChange={(e) => setField('healthCheckMethod', e.target.value)}
              >
                <option value="HEAD">HEAD</option>
                <option value="GET">GET</option>
                <option value="OPTIONS">OPTIONS</option>
              </select>
            </div>
          </div>

          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-hcint">{t('origins.healthCheckInterval')}</label>
              <input
                id="o-hcint"
                type="number"
                className="form-input"
                value={form.healthCheckIntervalMs}
                onChange={(e) => setField('healthCheckIntervalMs', parseInt(e.target.value, 10) || 0)}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-maxpar">{t('origins.maxParallel')}</label>
              <input
                id="o-maxpar"
                type="number"
                className="form-input"
                value={form.maxParallelRequests}
                onChange={(e) => setField('maxParallelRequests', parseInt(e.target.value, 10) || 0)}
              />
            </div>
          </div>

          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-unhealthy">{t('origins.unhealthyThreshold')}</label>
              <input
                id="o-unhealthy"
                type="number"
                className="form-input"
                value={form.unhealthyThreshold}
                onChange={(e) => setField('unhealthyThreshold', parseInt(e.target.value, 10) || 0)}
              />
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-healthy">{t('origins.healthyThreshold')}</label>
              <input
                id="o-healthy"
                type="number"
                className="form-input"
                value={form.healthyThreshold}
                onChange={(e) => setField('healthyThreshold', parseInt(e.target.value, 10) || 0)}
              />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="o-rate">{t('origins.rateLimit')}</label>
            <input
              id="o-rate"
              type="number"
              className="form-input"
              value={form.rateLimitRequestsThreshold}
              onChange={(e) => setField('rateLimitRequestsThreshold', parseInt(e.target.value, 10) || 0)}
            />
          </div>

          <div className="rv-form-grid">
            <div className="form-group">
              <label className="form-label" htmlFor="o-slowstart">{t('origins.slowStart')}</label>
              <input
                id="o-slowstart"
                type="number"
                className="form-input"
                value={form.slowStartMs}
                onChange={(e) => setField('slowStartMs', parseInt(e.target.value, 10) || 0)}
              />
              <span className="form-hint">{t('origins.slowStartHint')}</span>
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="o-maxfail">{t('origins.maxFailures')}</label>
              <input
                id="o-maxfail"
                type="number"
                className="form-input"
                value={form.maxFailures}
                onChange={(e) => setField('maxFailures', parseInt(e.target.value, 10) || 0)}
              />
              <span className="form-hint">{t('origins.maxFailuresHint')}</span>
            </div>
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="o-eject">{t('origins.ejectionDuration')}</label>
            <input
              id="o-eject"
              type="number"
              className="form-input"
              value={form.ejectionDurationMs}
              onChange={(e) => setField('ejectionDurationMs', parseInt(e.target.value, 10) || 0)}
            />
            <span className="form-hint">{t('origins.ejectionDurationHint')}</span>
          </div>
        </form>
      </Modal>

      {/* View detail */}
      <Modal open={!!viewRow} onClose={() => setViewRow(null)} size="medium" title={t('origins.viewTitle')}>
        {viewRow && (
          <div className="rv-detail-grid">
            <DetailItem label={t('origins.guid')} full mono>
              <CopyableId value={viewRow.guid} />
            </DetailItem>
            <DetailItem label={t('origins.identifier')} mono>{viewRow.identifier}</DetailItem>
            <DetailItem label={t('origins.name')}>{viewRow.name || '—'}</DetailItem>
            <DetailItem label={t('origins.hostname')} mono>{viewRow.hostname}</DetailItem>
            <DetailItem label={t('origins.port')}>{viewRow.port}</DetailItem>
            <DetailItem label={t('origins.ssl')}>{viewRow.ssl ? t('common.yes') : t('common.no')}</DetailItem>
            <DetailItem label={t('origins.health')} full>
              {(() => {
                const h = health[viewRow.guid];
                if (!h) {
                  return viewRow.healthy == null ? (
                    <Badge tone="neutral">{t('common.unknown')}</Badge>
                  ) : (
                    <HealthBadge healthy={viewRow.healthy} />
                  );
                }
                return (
                  <span className="sb-health-cell" onClick={() => setHealthDetail(h)} role="button" tabIndex={0}>
                    <HealthBadge healthy={!!h.isHealthy} />
                    <HealthHistogram history={h.history || []} slots={10} width={140} height={18} />
                  </span>
                );
              })()}
            </DetailItem>
            <DetailItem label={t('origins.healthCheckMethod')}>{viewRow.healthCheckMethod || '—'}</DetailItem>
            <DetailItem label={t('origins.healthCheckUrl')} mono>{viewRow.healthCheckUrl || '—'}</DetailItem>
            <DetailItem label={t('origins.healthCheckInterval')}>{viewRow.healthCheckIntervalMs ?? '—'}</DetailItem>
            <DetailItem label={t('origins.maxParallel')}>{viewRow.maxParallelRequests ?? '—'}</DetailItem>
            <DetailItem label={t('origins.rateLimit')}>{viewRow.rateLimitRequestsThreshold ?? '—'}</DetailItem>
            <DetailItem label={t('origins.unhealthyThreshold')}>{viewRow.unhealthyThreshold ?? '—'}</DetailItem>
            <DetailItem label={t('origins.healthyThreshold')}>{viewRow.healthyThreshold ?? '—'}</DetailItem>
            <DetailItem label={t('origins.slowStart')}>{viewRow.slowStartMs ?? '—'}</DetailItem>
            <DetailItem label={t('origins.maxFailures')}>{viewRow.maxFailures ?? '—'}</DetailItem>
            <DetailItem label={t('origins.ejectionDuration')}>{viewRow.ejectionDurationMs ?? '—'}</DetailItem>
          </div>
        )}
      </Modal>

      <HealthDetailModal open={!!healthDetail} onClose={() => setHealthDetail(null)} health={healthDetail} />

      <JsonViewerModal
        open={!!jsonRow}
        onClose={() => setJsonRow(null)}
        data={jsonRow}
        id={jsonRow?.guid}
        title={t('origins.viewTitle')}
      />

      <ConfirmModal
        open={!!deleteRow}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('origins.deleteTitle')}
        message={t('origins.deleteMessage', { name: deleteRow?.name || deleteRow?.identifier })}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />
    </div>
  );
}
