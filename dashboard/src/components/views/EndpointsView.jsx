import { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import PropTypes from 'prop-types';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import { useTranslation } from 'react-i18next';
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
  MethodBadge,
  Segmented,
  CopyableId,
  EmptyState,
  Icons,
} from '../ui';
import './EndpointsView.css';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

const EMPTY_ENDPOINT = {
  identifier: '',
  name: '',
  loadBalancingMode: 'RoundRobin',
  timeoutMs: 60000,
  blockHttp10: false,
  maxRequestBodySize: 536870912,
  includeAuthContextHeader: false,
  authContextHeader: '',
  useGlobalBlockedHeaders: true,
};

// Normalize an endpoint record into the editable form shape.
function endpointToForm(e) {
  if (!e) return { ...EMPTY_ENDPOINT };
  return {
    identifier: e.identifier || '',
    name: e.name || '',
    loadBalancingMode: e.loadBalancingMode || 'RoundRobin',
    timeoutMs: e.timeoutMs ?? 60000,
    blockHttp10: e.blockHttp10 || false,
    maxRequestBodySize: e.maxRequestBodySize ?? 536870912,
    includeAuthContextHeader: e.includeAuthContextHeader || false,
    authContextHeader: e.authContextHeader || '',
    useGlobalBlockedHeaders: e.useGlobalBlockedHeaders !== false,
  };
}

function EndpointsView() {
  const { apiClient, isAdmin } = useAuth();
  const { showSuccess, showError } = useApp();
  const { t } = useTranslation();
  const location = useLocation();

  // ---- Master list ----
  const [endpoints, setEndpoints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  // ---- Create / edit endpoint modal ----
  const [formOpen, setFormOpen] = useState(false);
  const [editingGuid, setEditingGuid] = useState(null);
  const [form, setForm] = useState({ ...EMPTY_ENDPOINT });
  const [saving, setSaving] = useState(false);

  // ---- JSON + delete ----
  const [jsonRow, setJsonRow] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  // ---- Detail modal ----
  const [detail, setDetail] = useState(null); // the endpoint being inspected
  const [activeTab, setActiveTab] = useState('routes');
  const [routes, setRoutes] = useState([]);
  const [mappings, setMappings] = useState([]);
  const [origins, setOrigins] = useState([]);
  const [settingsForm, setSettingsForm] = useState({ ...EMPTY_ENDPOINT });
  const [savingSettings, setSavingSettings] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);

  // ---- Route / mapping mutations ----
  const [newRoute, setNewRoute] = useState({ httpMethod: 'GET', urlPattern: '/', requiresAuthentication: false });
  const [editingRoute, setEditingRoute] = useState(null); // { id, ... } while editing
  const [deleteRoute, setDeleteRoute] = useState(null);
  const [detachTarget, setDetachTarget] = useState(null);
  const [attachValue, setAttachValue] = useState('');

  const pendingSelect = useRef(location.state?.selectIdentifier || null);

  const loadEndpoints = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Endpoints carry no routes of their own; fetch routes too so the master table can show the
      // method + URL pattern(s) for each endpoint at a glance.
      const [data, routesData] = await Promise.all([
        apiClient.getEndpoints({ search: search || undefined }),
        apiClient.getRoutes(),
      ]);
      const eps = Array.isArray(data) ? data : [];
      const allRoutes = Array.isArray(routesData) ? routesData : [];
      const byEndpoint = new Map();
      for (const r of allRoutes) {
        if (!byEndpoint.has(r.endpointIdentifier)) byEndpoint.set(r.endpointIdentifier, []);
        byEndpoint.get(r.endpointIdentifier).push(r);
      }
      setEndpoints(eps.map((e) => ({ ...e, routes: byEndpoint.get(e.identifier) || [] })));
    } catch (err) {
      setError(err.message || t('endpoints.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, search, t]);

  useEffect(() => {
    loadEndpoints();
  }, [loadEndpoints]);

  // ---- Detail data ----
  const loadDetailData = useCallback(
    async (identifier) => {
      try {
        const [routesData, mappingsData, originsData] = await Promise.all([
          apiClient.getRoutes(),
          apiClient.getMappings(),
          apiClient.getOrigins(),
        ]);
        setRoutes((Array.isArray(routesData) ? routesData : []).filter((r) => r.endpointIdentifier === identifier));
        setMappings((Array.isArray(mappingsData) ? mappingsData : []).filter((m) => m.endpointIdentifier === identifier));
        setOrigins(Array.isArray(originsData) ? originsData : []);
      } catch (err) {
        showError(err.message || t('endpoints.loadError'));
      }
    },
    [apiClient, showError, t]
  );

  const openDetail = useCallback(
    (endpoint) => {
      setDetail(endpoint);
      setActiveTab('routes');
      setSettingsForm(endpointToForm(endpoint));
      setNewRoute({ httpMethod: 'GET', urlPattern: '/', requiresAuthentication: false });
      setEditingRoute(null);
      setAttachValue('');
      setRoutes([]);
      setMappings([]);
      loadDetailData(endpoint.identifier);
    },
    [loadDetailData]
  );

  // Deep-link: select an endpoint by identifier passed via router state.
  useEffect(() => {
    if (pendingSelect.current && endpoints.length > 0) {
      const match = endpoints.find((e) => e.identifier === pendingSelect.current);
      if (match) openDetail(match);
      pendingSelect.current = null;
    }
  }, [endpoints, openDetail]);

  // ---- Derived list (client-side search + pagination) ----
  const total = endpoints.length;
  const pageRows = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return endpoints.slice(start, start + pageSize);
  }, [endpoints, pageNumber, pageSize]);

  useEffect(() => {
    // Clamp page if the list shrank.
    const pages = Math.max(1, Math.ceil(total / pageSize));
    if (pageNumber > pages) setPageNumber(pages);
  }, [total, pageSize, pageNumber]);

  // ---- Endpoint create / edit ----
  const openCreate = () => {
    setEditingGuid(null);
    setForm({ ...EMPTY_ENDPOINT });
    setFormOpen(true);
  };

  const openEdit = (endpoint) => {
    setEditingGuid(endpoint.guid);
    setForm(endpointToForm(endpoint));
    setFormOpen(true);
  };

  const submitForm = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editingGuid) {
        await apiClient.updateEndpoint(editingGuid, form);
        showSuccess(t('endpoints.updated'));
      } else {
        await apiClient.createEndpoint(form);
        showSuccess(t('endpoints.created'));
      }
      setFormOpen(false);
      loadEndpoints();
    } catch (err) {
      showError((t('endpoints.saveError')) + ': ' + (err.message || ''));
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await apiClient.deleteEndpoint(deleteTarget.guid);
      showSuccess(t('endpoints.deleted'));
      setDeleteTarget(null);
      if (detail && detail.guid === deleteTarget.guid) setDetail(null);
      loadEndpoints();
    } catch (err) {
      showError((t('endpoints.deleteError')) + ': ' + (err.message || ''));
    } finally {
      setDeleting(false);
    }
  };

  // ---- Detail: settings ----
  const settingsDirty = useMemo(() => {
    if (!detail) return false;
    const base = endpointToForm(detail);
    return Object.keys(base).some((k) => base[k] !== settingsForm[k]);
  }, [detail, settingsForm]);

  const resetSettings = () => setSettingsForm(endpointToForm(detail));

  const saveSettings = async () => {
    if (!detail) return;
    setSavingSettings(true);
    try {
      const updated = await apiClient.updateEndpoint(detail.guid, settingsForm);
      showSuccess(t('endpoints.settingsSaved'));
      const next = updated || { ...detail, ...settingsForm };
      setDetail(next);
      setSettingsForm(endpointToForm(next));
      setEndpoints((prev) => prev.map((e) => (e.guid === next.guid ? next : e)));
    } catch (err) {
      showError((t('endpoints.saveError')) + ': ' + (err.message || ''));
    } finally {
      setSavingSettings(false);
    }
  };

  // Guard the detail modal close when settings are dirty.
  const requestCloseDetail = () => {
    if (settingsDirty) setConfirmDiscard(true);
    else setDetail(null);
  };
  const closeDetailNow = () => {
    setConfirmDiscard(false);
    setDetail(null);
  };

  // ---- Detail: routes ----
  const addRoute = async () => {
    if (!newRoute.urlPattern || newRoute.urlPattern.trim() === '') {
      showError(t('endpoints.urlPatternRequired'));
      return;
    }
    try {
      await apiClient.createRoute({
        endpointIdentifier: detail.identifier,
        endpointGuid: detail.guid,
        httpMethod: newRoute.httpMethod,
        urlPattern: newRoute.urlPattern.trim(),
        requiresAuthentication: newRoute.requiresAuthentication,
      });
      showSuccess(t('endpoints.routeAdded'));
      setNewRoute({ httpMethod: 'GET', urlPattern: '/', requiresAuthentication: false });
      loadDetailData(detail.identifier);
    } catch (err) {
      showError((t('endpoints.routeSaveError')) + ': ' + (err.message || ''));
    }
  };

  const saveRouteEdit = async () => {
    if (!editingRoute) return;
    if (!editingRoute.urlPattern || editingRoute.urlPattern.trim() === '') {
      showError(t('endpoints.urlPatternRequired'));
      return;
    }
    try {
      await apiClient.updateRoute(editingRoute.id, {
        endpointIdentifier: detail.identifier,
        endpointGuid: detail.guid,
        httpMethod: editingRoute.httpMethod,
        urlPattern: editingRoute.urlPattern.trim(),
        requiresAuthentication: editingRoute.requiresAuthentication,
        sortOrder: editingRoute.sortOrder,
      });
      showSuccess(t('endpoints.routeUpdated'));
      setEditingRoute(null);
      loadDetailData(detail.identifier);
    } catch (err) {
      showError((t('endpoints.routeSaveError')) + ': ' + (err.message || ''));
    }
  };

  const confirmDeleteRoute = async () => {
    if (!deleteRoute) return;
    try {
      await apiClient.deleteRoute(deleteRoute.id);
      showSuccess(t('endpoints.routeDeleted'));
      setDeleteRoute(null);
      loadDetailData(detail.identifier);
    } catch (err) {
      showError((t('endpoints.routeDeleteError')) + ': ' + (err.message || ''));
    }
  };

  // ---- Detail: origin mappings ----
  const unmappedOrigins = useMemo(() => {
    const mapped = new Set(mappings.map((m) => m.originIdentifier));
    return origins.filter((o) => !mapped.has(o.identifier));
  }, [origins, mappings]);

  const originByIdentifier = (identifier) => origins.find((o) => o.identifier === identifier);

  const attachOrigin = async (originIdentifier) => {
    const origin = originByIdentifier(originIdentifier);
    if (!origin) return;
    try {
      await apiClient.createMapping({
        endpointIdentifier: detail.identifier,
        endpointGuid: detail.guid,
        originIdentifier: origin.identifier,
        originGuid: origin.guid,
      });
      showSuccess(t('endpoints.originAttached'));
      setAttachValue('');
      loadDetailData(detail.identifier);
    } catch (err) {
      showError((t('endpoints.attachError')) + ': ' + (err.message || ''));
    }
  };

  const confirmDetach = async () => {
    if (!detachTarget) return;
    try {
      await apiClient.deleteMapping(detachTarget.id);
      showSuccess(t('endpoints.originDetached'));
      setDetachTarget(null);
      loadDetailData(detail.identifier);
    } catch (err) {
      showError((t('endpoints.detachError')) + ': ' + (err.message || ''));
    }
  };

  // ---- Master table columns ----
  const columns = [
    {
      key: 'identifier',
      label: t('endpoints.identifier'),
      mono: true,
      sortable: true,
      filterable: true,
      render: (row) => <CopyableId value={row.identifier} />,
    },
    { key: 'name', label: t('endpoints.name'), sortable: true, filterable: true },
    {
      key: 'routes',
      label: t('endpoints.tabRoutes'),
      render: (row) => {
        const rowRoutes = row.routes || [];
        if (rowRoutes.length === 0) return <span className="ep-muted">{t('common.none')}</span>;
        return (
          <div className="ep-routes-cell">
            {rowRoutes.slice(0, 4).map((r) => (
              <div className="ep-route-line" key={r.id}>
                <MethodBadge method={r.httpMethod} />
                <code className="ep-code">{r.urlPattern}</code>
              </div>
            ))}
            {rowRoutes.length > 4 && (
              <span className="ep-muted">+{rowRoutes.length - 4}</span>
            )}
          </div>
        );
      },
    },
    {
      key: 'loadBalancingMode',
      label: t('endpoints.loadBalancing'),
      sortable: true,
      render: (row) => (
        <Badge tone={row.loadBalancingMode === 'Random' ? 'accent' : 'info'}>
          {row.loadBalancingMode === 'Random' ? t('endpoints.random') : t('endpoints.roundRobin')}
        </Badge>
      ),
    },
    {
      key: 'actions',
      label: '',
      isAction: true,
      align: 'end',
      render: (row) => (
        <ActionMenu
          items={entityActions(t, {
            onView: () => openDetail(row),
            onViewJson: () => setJsonRow(row),
            onEdit: () => openEdit(row),
            onDelete: () => setDeleteTarget(row),
            canEdit: isAdmin,
            canDelete: isAdmin,
          })}
        />
      ),
    },
  ];

  return (
    <div className="view">
      <PageHeader
        title={t('endpoints.title')}
        subtitle={t('endpoints.subtitle')}
        actions={
          isAdmin && (
            <button type="button" className="btn btn-primary" onClick={openCreate}>
              <Icons.Plus size={16} />
              {t('endpoints.add')}
            </button>
          )
        }
      />

      <TablePagination
        total={total}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageNumber(1);
        }}
        onRefresh={loadEndpoints}
      >
        <div className="ep-search">
          <Icons.Search size={16} className="ep-search__icon" />
          <input
            type="text"
            className="form-input ep-search__input"
            placeholder={t('common.search')}
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPageNumber(1);
            }}
            aria-label={t('common.search')}
          />
        </div>
      </TablePagination>

      <DataTable
        columns={columns}
        rows={pageRows}
        rowKey={(row) => row.guid}
        loading={loading}
        error={error}
        onRetry={loadEndpoints}
        onRowClick={openDetail}
        emptyMessage={t('endpoints.empty')}
        emptyHint={t('endpoints.emptyHint')}
      />

      {/* Create / edit endpoint */}
      {formOpen && (
        <Modal
          open
          onClose={() => setFormOpen(false)}
          size="large"
          title={editingGuid ? t('endpoints.editTitle') : t('endpoints.createTitle')}
          footer={
            <>
              <button type="button" className="sb-btn sb-btn--ghost" onClick={() => setFormOpen(false)} disabled={saving}>
                {t('common.cancel')}
              </button>
              <button type="submit" form="endpoint-form" className="sb-btn sb-btn--primary" disabled={saving}>
                {saving ? t('common.saving') : editingGuid ? t('common.update') : t('common.create')}
              </button>
            </>
          }
        >
          <form id="endpoint-form" onSubmit={submitForm}>
            <EndpointFormFields form={form} setForm={setForm} disableIdentifier={!!editingGuid} t={t} />
            {!editingGuid && <p className="form-hint">{t('endpoints.createHint')}</p>}
          </form>
        </Modal>
      )}

      {/* Detail modal */}
      {detail && (
        <Modal
          open
          onClose={requestCloseDetail}
          size="xl"
          title={detail.name || detail.identifier}
          subtitle={<CopyableId value={detail.identifier} />}
          footer={
            <button type="button" className="sb-btn sb-btn--ghost" onClick={requestCloseDetail}>
              {t('common.close')}
            </button>
          }
        >
          <Segmented
            className="ep-tabs"
            ariaLabel={t('endpoints.title')}
            value={activeTab}
            onChange={setActiveTab}
            options={[
              { value: 'routes', label: `${t('endpoints.tabRoutes')} (${routes.length})` },
              { value: 'origins', label: `${t('endpoints.tabOrigins')} (${mappings.length})` },
              { value: 'settings', label: t('endpoints.tabSettings') },
            ]}
          />

          <div className="ep-tabpanel">
            {activeTab === 'routes' && (
              <RoutesTab
                t={t}
                isAdmin={isAdmin}
                routes={routes}
                newRoute={newRoute}
                setNewRoute={setNewRoute}
                onAddRoute={addRoute}
                editingRoute={editingRoute}
                setEditingRoute={setEditingRoute}
                onSaveRouteEdit={saveRouteEdit}
                onDeleteRoute={setDeleteRoute}
              />
            )}

            {activeTab === 'origins' && (
              <OriginsTab
                t={t}
                isAdmin={isAdmin}
                mappings={mappings}
                originByIdentifier={originByIdentifier}
                unmappedOrigins={unmappedOrigins}
                attachValue={attachValue}
                setAttachValue={setAttachValue}
                onAttach={attachOrigin}
                onDetach={setDetachTarget}
              />
            )}

            {activeTab === 'settings' && (
              <div className="ep-settings">
                <EndpointFormFields form={settingsForm} setForm={setSettingsForm} disableIdentifier readOnly={!isAdmin} t={t} />
                {isAdmin && settingsDirty && (
                  <div className="ep-settings__actions">
                    <span className="ep-settings__dirty">{t('endpoints.unsavedChanges')}</span>
                    <button type="button" className="btn btn-secondary" onClick={resetSettings} disabled={savingSettings}>
                      {t('endpoints.reset')}
                    </button>
                    <button type="button" className="btn btn-primary" onClick={saveSettings} disabled={savingSettings}>
                      {savingSettings ? t('common.saving') : t('common.save')}
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </Modal>
      )}

      {/* JSON viewer */}
      {jsonRow && (
        <JsonViewerModal
          open
          onClose={() => setJsonRow(null)}
          data={jsonRow}
          id={jsonRow.identifier}
          title={t('endpoints.viewTitle')}
        />
      )}

      {/* Delete endpoint */}
      <ConfirmModal
        open={!!deleteTarget}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={confirmDelete}
        title={t('endpoints.deleteTitle')}
        message={t('endpoints.deleteMessage', { name: deleteTarget?.name || deleteTarget?.identifier || '' })}
        variant="danger"
        confirmLabel={t('common.delete')}
        busy={deleting}
      />

      {/* Delete route */}
      <ConfirmModal
        open={!!deleteRoute}
        onCancel={() => setDeleteRoute(null)}
        onConfirm={confirmDeleteRoute}
        title={t('endpoints.deleteRouteTitle')}
        message={t('endpoints.deleteRouteMessage', {
          route: deleteRoute ? `${deleteRoute.httpMethod || 'ANY'} ${deleteRoute.urlPattern}` : '',
        })}
        variant="danger"
        confirmLabel={t('common.delete')}
      />

      {/* Detach origin */}
      <ConfirmModal
        open={!!detachTarget}
        onCancel={() => setDetachTarget(null)}
        onConfirm={confirmDetach}
        title={t('endpoints.detachTitle')}
        message={t('endpoints.detachMessage', {
          name: detachTarget
            ? originByIdentifier(detachTarget.originIdentifier)?.name || detachTarget.originIdentifier
            : '',
        })}
        variant="danger"
        confirmLabel={t('endpoints.detach')}
      />

      {/* Discard unsaved settings */}
      <ConfirmModal
        open={confirmDiscard}
        onCancel={() => setConfirmDiscard(false)}
        onConfirm={closeDetailNow}
        title={t('endpoints.discardTitle')}
        message={t('endpoints.discardMessage')}
        variant="warning"
        confirmLabel={t('endpoints.discard')}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Shared endpoint field set (create modal + settings tab).
function EndpointFormFields({ form, setForm, disableIdentifier = false, readOnly = false, t }) {
  const set = (patch) => setForm((prev) => ({ ...prev, ...patch }));
  return (
    <>
      <div className="form-row">
        <div className="form-group">
          <label className="form-label">{t('endpoints.identifier')}</label>
          <input
            type="text"
            className="form-input"
            value={form.identifier}
            onChange={(e) => set({ identifier: e.target.value })}
            disabled={disableIdentifier}
            required
            placeholder="my-api"
          />
        </div>
        <div className="form-group">
          <label className="form-label">{t('endpoints.name')}</label>
          <input
            type="text"
            className="form-input"
            value={form.name}
            onChange={(e) => set({ name: e.target.value })}
            disabled={readOnly}
            placeholder="My API"
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label className="form-label">{t('endpoints.loadBalancing')}</label>
          <select
            className="form-input"
            value={form.loadBalancingMode}
            onChange={(e) => set({ loadBalancingMode: e.target.value })}
            disabled={readOnly}
          >
            <option value="RoundRobin">{t('endpoints.roundRobin')}</option>
            <option value="Random">{t('endpoints.random')}</option>
          </select>
        </div>
        <div className="form-group">
          <label className="form-label">{t('endpoints.timeoutMs')}</label>
          <input
            type="number"
            className="form-input"
            value={form.timeoutMs}
            onChange={(e) => set({ timeoutMs: parseInt(e.target.value, 10) || 0 })}
            disabled={readOnly}
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label className="form-label">{t('endpoints.maxRequestBodySize')}</label>
          <input
            type="number"
            className="form-input"
            value={form.maxRequestBodySize}
            onChange={(e) => set({ maxRequestBodySize: parseInt(e.target.value, 10) || 0 })}
            disabled={readOnly}
          />
        </div>
        <div className="form-group">
          <label className="form-label">{t('endpoints.authContextHeader')}</label>
          <input
            type="text"
            className="form-input"
            value={form.authContextHeader}
            onChange={(e) => set({ authContextHeader: e.target.value })}
            disabled={readOnly || !form.includeAuthContextHeader}
            placeholder="X-Auth-Context"
          />
        </div>
      </div>

      <div className="form-checks">
        <label className="form-checkbox">
          <input
            type="checkbox"
            checked={form.blockHttp10}
            onChange={(e) => set({ blockHttp10: e.target.checked })}
            disabled={readOnly}
          />
          {t('endpoints.blockHttp10')}
        </label>
        <label className="form-checkbox">
          <input
            type="checkbox"
            checked={form.includeAuthContextHeader}
            onChange={(e) => set({ includeAuthContextHeader: e.target.checked })}
            disabled={readOnly}
          />
          {t('endpoints.includeAuthContextHeader')}
        </label>
        <label className="form-checkbox">
          <input
            type="checkbox"
            checked={form.useGlobalBlockedHeaders}
            onChange={(e) => set({ useGlobalBlockedHeaders: e.target.checked })}
            disabled={readOnly}
          />
          {t('endpoints.useGlobalBlockedHeaders')}
        </label>
      </div>
    </>
  );
}

EndpointFormFields.propTypes = {
  form: PropTypes.object.isRequired,
  setForm: PropTypes.func.isRequired,
  disableIdentifier: PropTypes.bool,
  readOnly: PropTypes.bool,
  t: PropTypes.func.isRequired,
};

// ---------------------------------------------------------------------------
// Routes tab.
function RoutesTab({
  t,
  isAdmin,
  routes,
  newRoute,
  setNewRoute,
  onAddRoute,
  editingRoute,
  setEditingRoute,
  onSaveRouteEdit,
  onDeleteRoute,
}) {
  const columns = [
    {
      key: 'httpMethod',
      label: t('endpoints.httpMethod'),
      width: 120,
      render: (row) =>
        editingRoute && editingRoute.id === row.id ? (
          <select
            className="form-input"
            value={editingRoute.httpMethod}
            onChange={(e) => setEditingRoute({ ...editingRoute, httpMethod: e.target.value })}
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        ) : (
          <MethodBadge method={row.httpMethod} />
        ),
    },
    {
      key: 'urlPattern',
      label: t('endpoints.urlPattern'),
      mono: true,
      render: (row) =>
        editingRoute && editingRoute.id === row.id ? (
          <input
            type="text"
            className="form-input"
            value={editingRoute.urlPattern}
            onChange={(e) => setEditingRoute({ ...editingRoute, urlPattern: e.target.value })}
          />
        ) : (
          <code className="ep-code">{row.urlPattern}</code>
        ),
    },
    {
      key: 'requiresAuthentication',
      label: t('endpoints.requiresAuth'),
      width: 140,
      render: (row) =>
        editingRoute && editingRoute.id === row.id ? (
          <label className="form-checkbox">
            <input
              type="checkbox"
              checked={editingRoute.requiresAuthentication}
              onChange={(e) => setEditingRoute({ ...editingRoute, requiresAuthentication: e.target.checked })}
            />
          </label>
        ) : (
          <Badge tone={row.requiresAuthentication ? 'success' : 'neutral'}>
            {row.requiresAuthentication ? t('endpoints.authRequired') : t('endpoints.authPublic')}
          </Badge>
        ),
    },
    {
      key: 'actions',
      label: '',
      isAction: true,
      align: 'end',
      width: 96,
      render: (row) => {
        if (!isAdmin) return null;
        if (editingRoute && editingRoute.id === row.id) {
          return (
            <div className="ep-row-actions">
              <button type="button" className="btn btn-primary btn-sm" onClick={onSaveRouteEdit}>
                {t('common.save')}
              </button>
              <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditingRoute(null)}>
                {t('common.cancel')}
              </button>
            </div>
          );
        }
        return (
          <ActionMenu
            items={entityActions(t, {
              onEdit: () => setEditingRoute({ ...row }),
              onDelete: () => onDeleteRoute(row),
              canEdit: isAdmin,
              canDelete: isAdmin,
            })}
          />
        );
      },
    },
  ];

  return (
    <div>
      {isAdmin && (
        <div className="ep-inline-form">
          <select
            className="form-input ep-inline-form__method"
            value={newRoute.httpMethod}
            onChange={(e) => setNewRoute({ ...newRoute, httpMethod: e.target.value })}
            aria-label={t('endpoints.httpMethod')}
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
          <input
            type="text"
            className="form-input ep-inline-form__grow"
            placeholder="/api/resource/{id}"
            value={newRoute.urlPattern}
            onChange={(e) => setNewRoute({ ...newRoute, urlPattern: e.target.value })}
            aria-label={t('endpoints.urlPattern')}
          />
          <label className="form-checkbox">
            <input
              type="checkbox"
              checked={newRoute.requiresAuthentication}
              onChange={(e) => setNewRoute({ ...newRoute, requiresAuthentication: e.target.checked })}
            />
            {t('endpoints.requiresAuth')}
          </label>
          <button type="button" className="btn btn-primary" onClick={onAddRoute}>
            <Icons.Plus size={16} />
            {t('endpoints.addRoute')}
          </button>
        </div>
      )}

      <DataTable
        columns={columns}
        rows={routes}
        rowKey={(row) => row.id}
        emptyMessage={t('endpoints.routesEmpty')}
      />
    </div>
  );
}

RoutesTab.propTypes = {
  t: PropTypes.func.isRequired,
  isAdmin: PropTypes.bool,
  routes: PropTypes.array.isRequired,
  newRoute: PropTypes.object.isRequired,
  setNewRoute: PropTypes.func.isRequired,
  onAddRoute: PropTypes.func.isRequired,
  editingRoute: PropTypes.object,
  setEditingRoute: PropTypes.func.isRequired,
  onSaveRouteEdit: PropTypes.func.isRequired,
  onDeleteRoute: PropTypes.func.isRequired,
};

// ---------------------------------------------------------------------------
// Origins tab.
function OriginsTab({
  t,
  isAdmin,
  mappings,
  originByIdentifier,
  unmappedOrigins,
  attachValue,
  setAttachValue,
  onAttach,
  onDetach,
}) {
  const columns = [
    {
      key: 'originIdentifier',
      label: t('endpoints.origin'),
      mono: true,
      render: (row) => <CopyableId value={row.originIdentifier} />,
    },
    {
      key: 'name',
      label: t('endpoints.name'),
      render: (row) => {
        const o = originByIdentifier(row.originIdentifier);
        return o?.name || <span className="ep-muted">{t('common.none')}</span>;
      },
    },
    {
      key: 'host',
      label: t('origins.hostname'),
      render: (row) => {
        const o = originByIdentifier(row.originIdentifier);
        if (!o) return <span className="ep-muted">—</span>;
        return (
          <code className="ep-code">
            {o.hostname}:{o.port}
          </code>
        );
      },
    },
    {
      key: 'actions',
      label: '',
      isAction: true,
      align: 'end',
      width: 96,
      render: (row) =>
        isAdmin ? (
          <button type="button" className="btn btn-danger btn-sm" onClick={() => onDetach(row)}>
            {t('endpoints.detach')}
          </button>
        ) : null,
    },
  ];

  return (
    <div>
      {isAdmin && (
        <div className="ep-inline-form">
          <select
            className="form-input ep-inline-form__grow"
            value={attachValue}
            onChange={(e) => setAttachValue(e.target.value)}
            disabled={unmappedOrigins.length === 0}
            aria-label={t('endpoints.selectOrigin')}
          >
            <option value="">
              {unmappedOrigins.length === 0 ? t('endpoints.allOriginsMapped') : t('endpoints.selectOrigin')}
            </option>
            {unmappedOrigins.map((o) => (
              <option key={o.guid} value={o.identifier}>
                {o.name || o.identifier} ({o.hostname}:{o.port})
              </option>
            ))}
          </select>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => attachValue && onAttach(attachValue)}
            disabled={!attachValue}
          >
            <Icons.Plus size={16} />
            {t('endpoints.attachOrigin')}
          </button>
        </div>
      )}

      {mappings.length === 0 ? (
        <EmptyState message={t('endpoints.originsEmpty')} hint={t('endpoints.originsEmptyHint')} />
      ) : (
        <DataTable columns={columns} rows={mappings} rowKey={(row) => row.id} emptyMessage={t('endpoints.originsEmpty')} />
      )}
    </div>
  );
}

OriginsTab.propTypes = {
  t: PropTypes.func.isRequired,
  isAdmin: PropTypes.bool,
  mappings: PropTypes.array.isRequired,
  originByIdentifier: PropTypes.func.isRequired,
  unmappedOrigins: PropTypes.array.isRequired,
  attachValue: PropTypes.string.isRequired,
  setAttachValue: PropTypes.func.isRequired,
  onAttach: PropTypes.func.isRequired,
  onDetach: PropTypes.func.isRequired,
};

export default EndpointsView;
