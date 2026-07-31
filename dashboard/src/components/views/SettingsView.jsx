import { useState, useEffect, useCallback, useMemo, useRef, useId } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import {
  PageHeader,
  Collapsible,
  ConfirmModal,
  JsonViewerModal,
  Badge,
  ErrorBanner,
  EmptyState,
  Icons,
} from '../ui';
import './SettingsView.css';

// Metadata arrays the server returns alongside the config tree. They describe the
// tree but are not part of it, so they must be stripped before PUTting settings back.
const META_KEYS = ['restartRequiredSettings', 'runtimeEditableSettings'];

// Literal the server uses to mask stored secrets. Left untouched, it tells the
// server to keep the currently stored value.
const SECRET_MASK = '********';

// Section + field registry. Field `path` is the camelCase dotted location of the
// value in the settings tree; `restartPath` (defaults to `path`) is what we match
// against the server's restartRequiredSettings metadata.
const SECTIONS = [
  {
    key: 'webserver',
    titleKey: 'settings.sectionWebserver',
    defaultOpen: true,
    fields: [
      { path: 'webserver.hostname', labelKey: 'settings.fieldWebserverHostname', tipKey: 'settings.fieldWebserverHostnameTip', type: 'text', placeholder: 'localhost' },
      { path: 'webserver.port', labelKey: 'settings.fieldWebserverPort', tipKey: 'settings.fieldWebserverPortTip', type: 'number' },
      { path: 'webserver.ssl.enable', restartPath: 'webserver.ssl', labelKey: 'settings.fieldSslEnable', tipKey: 'settings.fieldSslEnableTip', type: 'toggle' },
    ],
  },
  {
    key: 'logging',
    titleKey: 'settings.sectionLogging',
    defaultOpen: true,
    fields: [
      { path: 'logging.minimumSeverity', labelKey: 'settings.fieldMinimumSeverity', tipKey: 'settings.fieldMinimumSeverityTip', type: 'number', min: 0, max: 7 },
      { path: 'logging.consoleLogging', labelKey: 'settings.fieldConsoleLogging', tipKey: 'settings.fieldConsoleLoggingTip', type: 'toggle' },
      { path: 'logging.enableColors', labelKey: 'settings.fieldEnableColors', tipKey: 'settings.fieldEnableColorsTip', type: 'toggle' },
      { path: 'logging.logDirectory', labelKey: 'settings.fieldLogDirectory', tipKey: 'settings.fieldLogDirectoryTip', type: 'text', placeholder: './logs/' },
      { path: 'logging.logFilename', labelKey: 'settings.fieldLogFilename', tipKey: 'settings.fieldLogFilenameTip', type: 'text', placeholder: 'switchboard.log' },
    ],
  },
  {
    key: 'database',
    titleKey: 'settings.sectionDatabase',
    fields: [
      {
        path: 'database.type',
        labelKey: 'settings.fieldDbType',
        tipKey: 'settings.fieldDbTypeTip',
        type: 'select',
        options: [
          { value: 'Sqlite', labelKey: 'settings.dbSqlite' },
          { value: 'Mysql', labelKey: 'settings.dbMysql' },
          { value: 'Postgres', labelKey: 'settings.dbPostgres' },
          { value: 'SqlServer', labelKey: 'settings.dbSqlServer' },
        ],
      },
      { path: 'database.filename', labelKey: 'settings.fieldDbFilename', tipKey: 'settings.fieldDbFilenameTip', type: 'text', placeholder: 'switchboard.db' },
      { path: 'database.hostname', labelKey: 'settings.fieldDbHostname', tipKey: 'settings.fieldDbHostnameTip', type: 'text' },
      { path: 'database.port', labelKey: 'settings.fieldDbPort', tipKey: 'settings.fieldDbPortTip', type: 'number' },
      { path: 'database.databaseName', labelKey: 'settings.fieldDbName', tipKey: 'settings.fieldDbNameTip', type: 'text' },
      { path: 'database.username', labelKey: 'settings.fieldDbUsername', tipKey: 'settings.fieldDbUsernameTip', type: 'text' },
      { path: 'database.password', labelKey: 'settings.fieldDbPassword', tipKey: 'settings.fieldDbPasswordTip', type: 'password', secret: true },
    ],
  },
  {
    key: 'management',
    titleKey: 'settings.sectionManagement',
    fields: [
      { path: 'management.enable', labelKey: 'settings.fieldMgmtEnable', tipKey: 'settings.fieldMgmtEnableTip', type: 'toggle' },
      { path: 'management.basePath', labelKey: 'settings.fieldMgmtBasePath', tipKey: 'settings.fieldMgmtBasePathTip', type: 'text', placeholder: '/_sb/v1.0' },
      { path: 'management.adminToken', labelKey: 'settings.fieldAdminToken', tipKey: 'settings.fieldAdminTokenTip', type: 'password', secret: true },
      { path: 'management.requireAuthentication', labelKey: 'settings.fieldRequireAuth', tipKey: 'settings.fieldRequireAuthTip', type: 'toggle' },
    ],
  },
  {
    key: 'requestHistory',
    titleKey: 'settings.sectionRequestHistory',
    fields: [
      { path: 'requestHistory.enable', labelKey: 'settings.fieldHistEnable', tipKey: 'settings.fieldHistEnableTip', type: 'toggle' },
      { path: 'requestHistory.captureRequestBody', labelKey: 'settings.fieldCaptureRequestBody', tipKey: 'settings.fieldCaptureRequestBodyTip', type: 'toggle' },
      { path: 'requestHistory.captureResponseBody', labelKey: 'settings.fieldCaptureResponseBody', tipKey: 'settings.fieldCaptureResponseBodyTip', type: 'toggle' },
      { path: 'requestHistory.captureRequestHeaders', labelKey: 'settings.fieldCaptureRequestHeaders', tipKey: 'settings.fieldCaptureRequestHeadersTip', type: 'toggle' },
      { path: 'requestHistory.captureResponseHeaders', labelKey: 'settings.fieldCaptureResponseHeaders', tipKey: 'settings.fieldCaptureResponseHeadersTip', type: 'toggle' },
      { path: 'requestHistory.retentionDays', labelKey: 'settings.fieldRetentionDays', tipKey: 'settings.fieldRetentionDaysTip', type: 'number', min: 0 },
      { path: 'requestHistory.maxRecords', labelKey: 'settings.fieldMaxRecords', tipKey: 'settings.fieldMaxRecordsTip', type: 'number', min: 0 },
      { path: 'requestHistory.cleanupIntervalSeconds', labelKey: 'settings.fieldCleanupInterval', tipKey: 'settings.fieldCleanupIntervalTip', type: 'number', min: 0 },
    ],
  },
  {
    key: 'openApi',
    titleKey: 'settings.sectionOpenApi',
    fields: [
      { path: 'openApi.enable', labelKey: 'settings.fieldOpenApiEnable', tipKey: 'settings.fieldOpenApiEnableTip', type: 'toggle' },
      { path: 'openApi.enableSwaggerUi', labelKey: 'settings.fieldEnableSwaggerUi', tipKey: 'settings.fieldEnableSwaggerUiTip', type: 'toggle' },
      { path: 'openApi.title', labelKey: 'settings.fieldOpenApiTitle', tipKey: 'settings.fieldOpenApiTitleTip', type: 'text' },
      { path: 'openApi.version', labelKey: 'settings.fieldOpenApiVersion', tipKey: 'settings.fieldOpenApiVersionTip', type: 'text' },
      { path: 'openApi.description', labelKey: 'settings.fieldOpenApiDescription', tipKey: 'settings.fieldOpenApiDescriptionTip', type: 'text' },
    ],
  },
];

// Every scalar field across all sections, plus the blocked-headers list, flattened
// for change detection.
const ALL_FIELDS = [
  ...SECTIONS.flatMap((s) => s.fields),
  { path: 'blockedHeaders', restartPath: 'blockedHeaders' },
];

// ---- nested path helpers -------------------------------------------------
function getPath(obj, path) {
  return path.split('.').reduce((o, k) => (o == null ? undefined : o[k]), obj);
}

function setPath(obj, path, value) {
  const keys = path.split('.');
  const clone = structuredClone(obj);
  let cur = clone;
  for (let i = 0; i < keys.length - 1; i++) {
    const k = keys[i];
    if (cur[k] == null || typeof cur[k] !== 'object') cur[k] = {};
    cur = cur[k];
  }
  cur[keys[keys.length - 1]] = value;
  return clone;
}

// Deep-ish equality good enough for scalars and the string[] blocked-headers list.
function valuesEqual(a, b) {
  if (a === b) return true;
  return JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function SettingsView() {
  const { apiClient, isAdmin } = useAuth();
  const { showSuccess, showError } = useApp();
  const { t } = useTranslation();

  const [draft, setDraft] = useState(null); // full settings tree (with edits)
  const [original, setOriginal] = useState(null); // pristine snapshot for diffing
  const [restartSet, setRestartSet] = useState(() => new Set());

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const [newHeader, setNewHeader] = useState('');
  const [jsonOpen, setJsonOpen] = useState(false);

  const [confirmRestart, setConfirmRestart] = useState(false);
  const [restarting, setRestarting] = useState(false);
  const [restartPending, setRestartPending] = useState(false);

  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await apiClient.getSettings();
      const set = new Set(
        (Array.isArray(data?.restartRequiredSettings) ? data.restartRequiredSettings : []).map((p) =>
          String(p).toLowerCase()
        )
      );
      setDraft(data);
      setOriginal(structuredClone(data));
      setRestartSet(set);
    } catch (err) {
      setError(err.message || t('settings.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  // The lowercased camelCase path is compared against the lowercased server metadata.
  const isRestartRequired = useCallback(
    (dottedCamelPath) => restartSet.has(String(dottedCamelPath).toLowerCase()),
    [restartSet]
  );

  const updateField = useCallback((path, value) => {
    setDraft((prev) => (prev ? setPath(prev, path, value) : prev));
  }, []);

  // ---- blocked headers list mutations ----
  const blockedHeaders = useMemo(
    () => (Array.isArray(draft?.blockedHeaders) ? draft.blockedHeaders : []),
    [draft]
  );

  const addHeader = () => {
    const name = newHeader.trim();
    if (!name) return;
    if (blockedHeaders.some((h) => h.toLowerCase() === name.toLowerCase())) {
      setNewHeader('');
      return;
    }
    updateField('blockedHeaders', [...blockedHeaders, name]);
    setNewHeader('');
  };

  const removeHeader = (name) => {
    updateField(
      'blockedHeaders',
      blockedHeaders.filter((h) => h !== name)
    );
  };

  const dirty = useMemo(() => {
    if (!draft || !original) return false;
    return ALL_FIELDS.some((f) => !valuesEqual(getPath(draft, f.path), getPath(original, f.path)));
  }, [draft, original]);

  // ---- save ----
  const handleSave = async () => {
    if (!isAdmin || !draft) return;
    // Count changed fields that require a restart to take effect.
    const changedRestart = ALL_FIELDS.filter(
      (f) =>
        !valuesEqual(getPath(draft, f.path), getPath(original, f.path)) &&
        isRestartRequired(f.restartPath || f.path)
    ).length;

    const payload = structuredClone(draft);
    META_KEYS.forEach((k) => delete payload[k]);

    setSaving(true);
    try {
      await apiClient.updateSettings(payload);
      if (changedRestart > 0) {
        showSuccess(t('settings.savedRestart', { count: changedRestart }));
        setRestartPending(true);
      } else {
        showSuccess(t('settings.savedLive'));
      }
      await load();
    } catch (err) {
      showError((t('settings.saveError')) + ': ' + (err.message || ''));
    } finally {
      if (mounted.current) setSaving(false);
    }
  };

  // ---- restart ----
  const doRestart = async () => {
    setConfirmRestart(false);
    setRestarting(true);
    try {
      // The server accepts (202) then drops the connection as it exits, so a
      // rejection here is expected and not an error.
      await apiClient.restartServer();
    } catch {
      /* expected: connection dropped as the server exits */
    }

    // Poll health until the server comes back (~60s budget).
    const deadline = Date.now() + 60000;
    let backOnline = false;
    // Give the process a moment to actually go down before polling.
    await sleep(2000);
    while (Date.now() < deadline && mounted.current) {
      try {
        await apiClient.getHealth();
        backOnline = true;
        break;
      } catch {
        await sleep(2000);
      }
    }

    if (!mounted.current) return;
    setRestarting(false);
    if (backOnline) {
      setRestartPending(false);
      showSuccess(t('settings.savedLive'));
      await load();
    } else {
      showError(t('settings.restartTimeout'));
    }
  };

  // ---- render ----
  if (loading) {
    return (
      <div className="view">
        <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />
        <div className="view-loading">
          <div className="spinner" />
          <p>{t('common.loading')}</p>
        </div>
      </div>
    );
  }

  if (error || !draft) {
    return (
      <div className="view">
        <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />
        <ErrorBanner message={error || t('settings.loadError')} onRetry={load} />
      </div>
    );
  }

  return (
    <div className="view settings-view">
      <PageHeader
        title={t('settings.title')}
        subtitle={t('settings.subtitle')}
        actions={
          <div className="settings-header-actions">
            <button type="button" className="btn btn-secondary" onClick={() => setJsonOpen(true)}>
              <Icons.Code size={16} />
              {t('common.viewJson')}
            </button>
            {isAdmin && (
              <button type="button" className="btn btn-danger" onClick={() => setConfirmRestart(true)} disabled={restarting}>
                <Icons.Power size={16} />
                {t('settings.restart')}
              </button>
            )}
          </div>
        }
      />

      {!isAdmin && (
        <div className="settings-notice" role="note">
          {t('settings.readOnly')}
        </div>
      )}

      {restartPending && (
        <div className="settings-pending" role="status">
          <div className="settings-pending__text">
            <strong>{t('settings.restartPending')}</strong>
            <span>{t('settings.restartPendingBody')}</span>
          </div>
          {isAdmin && (
            <button type="button" className="btn btn-danger btn-sm" onClick={() => setConfirmRestart(true)} disabled={restarting}>
              <Icons.Power size={15} />
              {t('settings.restart')}
            </button>
          )}
        </div>
      )}

      <div className="settings-sections">
        {SECTIONS.map((section) => (
          <Collapsible key={section.key} title={t(section.titleKey)} defaultOpen={!!section.defaultOpen}>
            <div className="settings-fields">
              {section.fields.map((field) => (
                <SettingField
                  key={field.path}
                  field={field}
                  value={getPath(draft, field.path)}
                  restartRequired={isRestartRequired(field.restartPath || field.path)}
                  readOnly={!isAdmin}
                  onChange={(v) => updateField(field.path, v)}
                  t={t}
                />
              ))}
            </div>
          </Collapsible>
        ))}

        {/* Blocked Headers list editor */}
        <Collapsible
          title={t('settings.sectionBlockedHeaders')}
          defaultOpen={false}
          right={
            isRestartRequired('blockedHeaders') ? (
              <Badge tone="warning" title={t('settings.restartRequiredTip')}>
                {t('settings.restartRequired')}
              </Badge>
            ) : null
          }
        >
          {isAdmin && (
            <div className="settings-header-add">
              <input
                type="text"
                className="form-input"
                placeholder={t('settings.headerName')}
                value={newHeader}
                onChange={(e) => setNewHeader(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addHeader();
                  }
                }}
                aria-label={t('settings.headerName')}
                title={t('settings.headerNameTip')}
              />
              <button type="button" className="btn btn-primary" onClick={addHeader} disabled={!newHeader.trim()}>
                <Icons.Plus size={16} />
                {t('settings.addHeader')}
              </button>
            </div>
          )}

          {blockedHeaders.length === 0 ? (
            <EmptyState message={t('blockedHeaders.empty')} hint={t('blockedHeaders.emptyHint')} />
          ) : (
            <ul className="settings-header-list">
              {blockedHeaders.map((header) => (
                <li key={header} className="settings-header-item">
                  <code>{header}</code>
                  {isAdmin && (
                    <button
                      type="button"
                      className="settings-header-remove"
                      onClick={() => removeHeader(header)}
                      aria-label={t('common.delete')}
                      title={t('common.delete')}
                    >
                      <Icons.Close size={16} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Collapsible>
      </div>

      {/* Sticky save footer */}
      <div className="settings-footer">
        {dirty && isAdmin && <span className="settings-footer__dirty">{t('settings.unsaved')}</span>}
        <button
          type="button"
          className="btn btn-primary"
          onClick={handleSave}
          disabled={!isAdmin || saving || !dirty}
        >
          {saving ? t('common.saving') : t('common.save')}
        </button>
      </div>

      {jsonOpen && (
        <JsonViewerModal open onClose={() => setJsonOpen(false)} data={draft} title={t('settings.title')} />
      )}

      <ConfirmModal
        open={confirmRestart}
        onCancel={() => setConfirmRestart(false)}
        onConfirm={doRestart}
        title={t('settings.restartConfirmTitle')}
        message={t('settings.restartConfirmBody')}
        variant="danger"
        confirmLabel={t('settings.restart')}
      />

      {restarting && (
        <div className="settings-restart-overlay" role="alert" aria-live="assertive">
          <div className="settings-restart-overlay__card">
            <div className="spinner" />
            <p>{t('settings.restarting')}</p>
          </div>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// A single labeled setting row with its restart-required / applies-live annotation.
function SettingField({ field, value, restartRequired, readOnly, onChange, t }) {
  const id = useId();
  const { type } = field;
  const isSecret = !!field.secret;
  const tip = field.tipKey ? t(field.tipKey) : undefined;

  // Secrets arrive masked; show an empty control with a "set" placeholder until the
  // user types a replacement. Restoring to empty means "keep stored value".
  const secretUntouched = isSecret && value === SECRET_MASK;

  const annotation = restartRequired ? (
    <Badge tone="warning" title={t('settings.restartRequiredTip')}>
      {t('settings.restartRequired')}
    </Badge>
  ) : (
    <span className="settings-field__live">{t('settings.appliesLive')}</span>
  );

  const labelRow = (
    <div className="settings-field__labelrow">
      <label className="form-label" htmlFor={id} title={tip}>
        {t(field.labelKey)}
      </label>
      {annotation}
    </div>
  );

  if (type === 'toggle') {
    return (
      <div className="settings-field settings-field--toggle">
        <div className="settings-field__labelrow">
          <label className="settings-toggle" title={tip}>
            <input
              id={id}
              type="checkbox"
              checked={!!value}
              onChange={(e) => onChange(e.target.checked)}
              disabled={readOnly}
              title={tip}
            />
            <span className="settings-toggle__track" aria-hidden="true">
              <span className="settings-toggle__thumb" />
            </span>
            <span className="settings-toggle__label">{t(field.labelKey)}</span>
          </label>
          {annotation}
        </div>
      </div>
    );
  }

  let control;
  if (type === 'select') {
    control = (
      <select
        id={id}
        className="form-input"
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        disabled={readOnly}
        title={tip}
      >
        {field.options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {t(opt.labelKey)}
          </option>
        ))}
      </select>
    );
  } else if (type === 'number') {
    control = (
      <input
        id={id}
        type="number"
        className="form-input"
        value={value ?? ''}
        min={field.min}
        max={field.max}
        title={tip}
        onChange={(e) => {
          const v = e.target.value;
          if (v === '') return onChange(0);
          const n = Number(v);
          onChange(Number.isNaN(n) ? 0 : n);
        }}
        disabled={readOnly}
      />
    );
  } else if (type === 'password') {
    control = (
      <input
        id={id}
        type="password"
        className="form-input"
        autoComplete="new-password"
        value={secretUntouched ? '' : value ?? ''}
        title={tip}
        placeholder={isSecret ? t('settings.secretSet') : undefined}
        onChange={(e) => onChange(e.target.value === '' ? SECRET_MASK : e.target.value)}
        disabled={readOnly}
      />
    );
  } else {
    control = (
      <input
        id={id}
        type="text"
        className="form-input"
        value={value ?? ''}
        placeholder={field.placeholder}
        onChange={(e) => onChange(e.target.value)}
        disabled={readOnly}
        title={tip}
      />
    );
  }

  return (
    <div className="settings-field">
      {labelRow}
      {control}
    </div>
  );
}

SettingField.propTypes = {
  field: PropTypes.shape({
    path: PropTypes.string.isRequired,
    labelKey: PropTypes.string,
    tipKey: PropTypes.string,
    type: PropTypes.string.isRequired,
    options: PropTypes.array,
    placeholder: PropTypes.string,
    min: PropTypes.number,
    max: PropTypes.number,
    secret: PropTypes.bool,
    restartPath: PropTypes.string,
  }).isRequired,
  value: PropTypes.any,
  restartRequired: PropTypes.bool,
  readOnly: PropTypes.bool,
  onChange: PropTypes.func.isRequired,
  t: PropTypes.func.isRequired,
};

export default SettingsView;
