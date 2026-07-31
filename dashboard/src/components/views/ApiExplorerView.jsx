import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import {
  PageHeader,
  Segmented,
  MethodBadge,
  StatusBadge,
  Badge,
  CopyButton,
  EmptyState,
  ErrorBanner,
  Collapsible,
  ConfirmModal,
  Icons,
} from '../ui';
import './ApiExplorerView.css';

const BODY_METHODS = new Set(['POST', 'PUT', 'PATCH']);
const OPERATION_METHODS = ['get', 'put', 'post', 'delete', 'patch', 'options', 'head'];
const CONTENT_TYPES = [
  'application/json',
  'application/x-www-form-urlencoded',
  'text/plain',
  'multipart/form-data',
];
const HISTORY_LIMIT = 12;
const OTHER_TAG = '__other__';
const SYSTEM_TAG = '__system_endpoints__';

// Extract {name} path parameters from a route URL pattern so they can be filled in the builder.
function extractPathParams(path) {
  const out = [];
  const re = /\{([^}]+)\}/g;
  let m;
  while ((m = re.exec(path || '')) !== null) {
    out.push({ name: m[1], in: 'path', required: true, schema: { type: 'string' } });
  }
  return out;
}

const historyKey = (serverUrl) => `sb_api_explorer_history::${serverUrl || ''}`;
const operationKey = (method, path) => `${method.toUpperCase()} ${path}`;
const stripTrailingSlash = (url) => (url || '').replace(/\/+$/, '');

// Resolve a possible { $ref } into the object it points at within the spec.
// Only the shallow local-reference form (#/components/...) is handled — enough
// for the management document's parameters and request bodies.
function resolveRef(node, spec) {
  if (!node || typeof node !== 'object') return node;
  if (!node.$ref || typeof node.$ref !== 'string') return node;
  const parts = node.$ref.replace(/^#\//, '').split('/');
  let cursor = spec;
  for (const part of parts) {
    if (cursor == null) return node;
    cursor = cursor[part];
  }
  return cursor || node;
}

// Flatten spec.paths into a list of operations, merging path-level parameters
// with operation-level parameters (operation wins on name+location collisions).
function flattenOperations(spec) {
  const out = [];
  const paths = spec?.paths;
  if (!paths || typeof paths !== 'object') return out;

  Object.entries(paths).forEach(([path, pathItem]) => {
    if (!pathItem || typeof pathItem !== 'object') return;
    const sharedParams = Array.isArray(pathItem.parameters) ? pathItem.parameters : [];

    OPERATION_METHODS.forEach((method) => {
      const op = pathItem[method];
      if (!op || typeof op !== 'object') return;

      const merged = new Map();
      [...sharedParams, ...(Array.isArray(op.parameters) ? op.parameters : [])].forEach((raw) => {
        const p = resolveRef(raw, spec);
        if (!p || !p.name) return;
        merged.set(`${p.in}:${p.name}`, p);
      });

      out.push({
        key: operationKey(method, path),
        method: method.toUpperCase(),
        path,
        operationId: op.operationId || '',
        summary: op.summary || op.description || '',
        tags: Array.isArray(op.tags) && op.tags.length ? op.tags : [],
        parameters: Array.from(merged.values()),
        requestBody: op.requestBody ? resolveRef(op.requestBody, spec) : null,
      });
    });
  });

  return out;
}

// Group operations by tag, honoring spec.tags order; untagged fall under "Other".
function groupOperations(operations, specTags) {
  const byTag = new Map();
  const add = (tag, op) => {
    if (!byTag.has(tag)) byTag.set(tag, []);
    byTag.get(tag).push(op);
  };

  operations.forEach((op) => {
    if (op.tags.length) op.tags.forEach((tag) => add(tag, op));
    else add(OTHER_TAG, op);
  });

  const ordered = [];
  const seen = new Set();
  (Array.isArray(specTags) ? specTags : []).forEach((tagDef) => {
    const name = typeof tagDef === 'string' ? tagDef : tagDef?.name;
    if (name && byTag.has(name) && !seen.has(name)) {
      ordered.push({ tag: name, operations: byTag.get(name) });
      seen.add(name);
    }
  });
  // Any tags present on operations but absent from spec.tags, alphabetically.
  Array.from(byTag.keys())
    .filter((tag) => tag !== OTHER_TAG && !seen.has(tag))
    .sort((a, b) => a.localeCompare(b))
    .forEach((tag) => ordered.push({ tag, operations: byTag.get(tag) }));

  if (byTag.has(OTHER_TAG)) {
    ordered.push({ tag: OTHER_TAG, operations: byTag.get(OTHER_TAG) });
  }
  return ordered;
}

// Best-effort JSON body template from a requestBody's example/schema example.
function bodyTemplate(requestBody, spec) {
  const json = requestBody?.content?.['application/json'];
  if (!json) return '';
  const example =
    json.example ??
    (json.examples && Object.values(json.examples)[0]?.value) ??
    resolveRef(json.schema, spec)?.example;
  if (example === undefined) return '';
  try {
    return JSON.stringify(example, null, 2);
  } catch {
    return '';
  }
}

function prettyJson(text) {
  if (!text) return '';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return null;
  }
}

let rowSeq = 0;
const newHeaderRow = () => ({ id: `h${(rowSeq += 1)}`, key: '', value: '' });

function ApiExplorerView() {
  const { t } = useTranslation();
  const { apiClient, serverUrl } = useAuth();
  const base = stripTrailingSlash(serverUrl);

  const [spec, setSpec] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  const [selectedKey, setSelectedKey] = useState(null);
  const [systemOps, setSystemOps] = useState([]);

  // Request builder state (reset whenever the selected operation changes).
  const [paramValues, setParamValues] = useState({});
  const [headerRows, setHeaderRows] = useState([]);
  const [contentType, setContentType] = useState('application/json');
  const [bodyText, setBodyText] = useState('');
  const [bodyError, setBodyError] = useState(null);

  const [running, setRunning] = useState(false);
  const [response, setResponse] = useState(null);
  const [responseError, setResponseError] = useState(null);
  const [responseTab, setResponseTab] = useState('body');
  const [confirmOpen, setConfirmOpen] = useState(false);

  const [history, setHistory] = useState([]);

  // ---- Load the OpenAPI document on mount ----
  const loadSpec = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const doc = await apiClient.getOpenApiSpec();
      setSpec(doc);
    } catch (err) {
      setSpec(null);
      setLoadError(err?.message || t('apiExplorer.specError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    loadSpec();
  }, [loadSpec]);

  // ---- Load the system's configured API endpoints (routes) as executable operations ----
  const loadSystemOps = useCallback(async () => {
    try {
      const [eps, routes] = await Promise.all([apiClient.getEndpoints(), apiClient.getRoutes()]);
      const nameByEndpoint = new Map(
        (Array.isArray(eps) ? eps : []).map((e) => [e.identifier, e.name || e.identifier])
      );
      const ops = (Array.isArray(routes) ? routes : []).map((r) => {
        const method = String(r.httpMethod || 'GET').toUpperCase();
        const path = r.urlPattern || '/';
        return {
          key: operationKey(method, path),
          method,
          path,
          operationId: '',
          summary: nameByEndpoint.get(r.endpointIdentifier) || r.endpointIdentifier || '',
          tags: [SYSTEM_TAG],
          parameters: extractPathParams(path),
          requestBody: null,
        };
      });
      setSystemOps(ops);
    } catch {
      setSystemOps([]);
    }
  }, [apiClient]);

  useEffect(() => {
    loadSystemOps();
  }, [loadSystemOps]);

  // ---- Request history (localStorage, keyed by server) ----
  useEffect(() => {
    try {
      const raw = localStorage.getItem(historyKey(serverUrl));
      const parsed = raw ? JSON.parse(raw) : [];
      setHistory(Array.isArray(parsed) ? parsed : []);
    } catch {
      setHistory([]);
    }
  }, [serverUrl]);

  const pushHistory = useCallback(
    (entry) => {
      setHistory((prev) => {
        const next = [entry, ...prev].slice(0, HISTORY_LIMIT);
        try {
          localStorage.setItem(historyKey(serverUrl), JSON.stringify(next));
        } catch {
          /* storage may be unavailable — keep the in-memory list */
        }
        return next;
      });
    },
    [serverUrl]
  );

  // ---- Derived operation data ----
  // Management API operations from the OpenAPI document, plus the proxy API endpoints configured in
  // the system (each endpoint route becomes a selectable operation executed through the gateway).
  const operations = useMemo(() => [...flattenOperations(spec), ...systemOps], [spec, systemOps]);
  const operationsByKey = useMemo(() => {
    const map = new Map();
    operations.forEach((op) => map.set(op.key, op));
    return map;
  }, [operations]);

  const groups = useMemo(() => groupOperations(operations, spec?.tags), [operations, spec]);

  const selected = selectedKey ? operationsByKey.get(selectedKey) : null;

  const pathParams = useMemo(
    () => (selected ? selected.parameters.filter((p) => p.in === 'path') : []),
    [selected]
  );
  const queryParams = useMemo(
    () => (selected ? selected.parameters.filter((p) => p.in === 'query') : []),
    [selected]
  );

  const hasBody = useMemo(
    () => Boolean(selected && (BODY_METHODS.has(selected.method) || selected.requestBody)),
    [selected]
  );

  // ---- Select / load an operation into the builder ----
  const applyOperation = useCallback(
    (op, preset) => {
      if (!op) return;
      setSelectedKey(op.key);
      setParamValues(preset?.paramValues || {});
      setHeaderRows(
        Array.isArray(preset?.headerRows) && preset.headerRows.length
          ? preset.headerRows.map((r) => ({ ...newHeaderRow(), ...r }))
          : [newHeaderRow()]
      );
      setContentType(preset?.contentType || 'application/json');
      setBodyText(preset?.bodyText ?? bodyTemplate(op.requestBody, spec));
      setBodyError(null);
      setResponse(null);
      setResponseError(null);
      setResponseTab('body');
    },
    [spec]
  );

  const selectOperation = useCallback((op) => applyOperation(op, null), [applyOperation]);

  const loadFromHistory = useCallback(
    (entry) => {
      const op = operationsByKey.get(operationKey(entry.method, entry.path));
      if (!op) return;
      applyOperation(op, {
        paramValues: entry.paramValues,
        headerRows: entry.headerRows,
        contentType: entry.contentType,
        bodyText: entry.bodyText,
      });
    },
    [operationsByKey, applyOperation]
  );

  // ---- Builder mutations ----
  const setParam = (param, value) =>
    setParamValues((prev) => ({ ...prev, [`${param.in}:${param.name}`]: value }));
  const getParam = useCallback(
    (param) => paramValues[`${param.in}:${param.name}`] ?? '',
    [paramValues]
  );

  const updateHeader = (id, patch) =>
    setHeaderRows((prev) => prev.map((r) => (r.id === id ? { ...r, ...patch } : r)));
  const addHeader = () => setHeaderRows((prev) => [...prev, newHeaderRow()]);
  const removeHeader = (id) => setHeaderRows((prev) => prev.filter((r) => r.id !== id));

  // ---- Resolved path / URL ----
  const resolvedPath = useMemo(() => {
    if (!selected) return '';
    let path = selected.path;
    pathParams.forEach((p) => {
      const raw = getParam(p);
      if (raw !== '') {
        path = path.replace(`{${p.name}}`, encodeURIComponent(raw));
      }
    });
    const usp = new URLSearchParams();
    queryParams.forEach((p) => {
      const raw = getParam(p);
      if (raw !== '') usp.append(p.name, raw);
    });
    const qs = usp.toString();
    return qs ? `${path}?${qs}` : path;
  }, [selected, pathParams, queryParams, getParam]);

  const resolvedUrl = useMemo(
    () => (selected ? `${base}${resolvedPath}` : ''),
    [base, resolvedPath, selected]
  );

  // Custom request headers (excludes Authorization, which apiClient.execute adds).
  const customHeaders = useMemo(() => {
    const headers = {};
    headerRows.forEach((r) => {
      const key = r.key.trim();
      if (key) headers[key] = r.value;
    });
    if (hasBody && bodyText.trim()) headers['Content-Type'] = contentType;
    return headers;
  }, [headerRows, hasBody, bodyText, contentType]);

  const requiresConfirm = useMemo(
    () => Boolean(selected && (selected.method === 'DELETE' || selected.path.includes('/bulk'))),
    [selected]
  );

  // ---- Execute ----
  const validateBody = useCallback(() => {
    if (!hasBody || !bodyText.trim()) return true;
    if (contentType !== 'application/json') return true;
    try {
      JSON.parse(bodyText);
      setBodyError(null);
      return true;
    } catch (err) {
      setBodyError(err?.message || t('apiExplorer.invalidJson'));
      return false;
    }
  }, [hasBody, bodyText, contentType, t]);

  const runExecute = useCallback(async () => {
    if (!selected) return;
    setRunning(true);
    setResponse(null);
    setResponseError(null);
    const body = hasBody && bodyText.trim() ? bodyText : undefined;
    try {
      const res = await apiClient.execute(selected.method, resolvedPath, {
        headers: customHeaders,
        body,
      });
      setResponse(res);
      setResponseTab('body');
      pushHistory({
        method: selected.method,
        path: selected.path,
        summary: selected.summary,
        paramValues,
        headerRows: headerRows.filter((r) => r.key.trim()),
        contentType,
        bodyText,
        status: res.status,
        ts: Date.now(),
      });
    } catch (err) {
      setResponseError(err?.message || t('apiExplorer.executeError'));
    } finally {
      setRunning(false);
    }
  }, [
    selected,
    hasBody,
    bodyText,
    apiClient,
    resolvedPath,
    customHeaders,
    paramValues,
    headerRows,
    contentType,
    pushHistory,
    t,
  ]);

  const handleExecute = () => {
    if (!validateBody()) return;
    if (requiresConfirm) {
      setConfirmOpen(true);
      return;
    }
    runExecute();
  };

  const confirmExecute = () => {
    setConfirmOpen(false);
    runExecute();
  };

  // ---- Generated code snippets (recomputed on every change) ----
  const snippets = useMemo(() => {
    if (!selected) return { curl: '', fetch: '', csharp: '' };
    const url = resolvedUrl;
    const method = selected.method;
    const codeHeaders = { Authorization: 'Bearer ***', ...customHeaders };
    const includeBody = hasBody && bodyText.trim();

    // curl
    const curlLines = [`curl -X ${method} '${url}'`];
    Object.entries(codeHeaders).forEach(([k, v]) => curlLines.push(`  -H '${k}: ${v}'`));
    if (includeBody) curlLines.push(`  --data '${bodyText.replace(/'/g, "'\\''")}'`);
    const curl = curlLines.join(' \\\n');

    // fetch
    const fetchHeaders = JSON.stringify(codeHeaders, null, 2)
      .split('\n')
      .map((line, i) => (i === 0 ? line : `  ${line}`))
      .join('\n');
    const fetchOpts = [`  method: '${method}'`, `  headers: ${fetchHeaders}`];
    if (includeBody) fetchOpts.push(`  body: ${JSON.stringify(bodyText)}`);
    const fetchSnippet = `const response = await fetch('${url}', {\n${fetchOpts.join(
      ',\n'
    )}\n});\nconst data = await response.text();`;

    // C# HttpClient
    const csLines = [
      'using var client = new HttpClient();',
      `var request = new HttpRequestMessage(HttpMethod.${
        method.charAt(0) + method.slice(1).toLowerCase()
      }, "${url}");`,
    ];
    Object.entries(codeHeaders).forEach(([k, v]) => {
      if (k.toLowerCase() === 'content-type') return; // set via StringContent below
      csLines.push(`request.Headers.Add("${k}", "${v}");`);
    });
    if (includeBody) {
      const escaped = bodyText.replace(/"/g, '""');
      csLines.push(
        `request.Content = new StringContent(@"${escaped}", System.Text.Encoding.UTF8, "${contentType}");`
      );
    }
    csLines.push('var response = await client.SendAsync(request);');
    csLines.push('var body = await response.Content.ReadAsStringAsync();');
    const csharp = csLines.join('\n');

    return { curl, fetch: fetchSnippet, csharp };
  }, [selected, resolvedUrl, customHeaders, hasBody, bodyText, contentType]);

  // ---- Response formatting ----
  const prettyBody = useMemo(() => (response ? prettyJson(response.body) : null), [response]);

  const tabOptions = useMemo(
    () => [
      { value: 'body', label: t('apiExplorer.tabBody') },
      { value: 'headers', label: t('apiExplorer.tabHeaders') },
      { value: 'timing', label: t('apiExplorer.tabTiming') },
      { value: 'code', label: t('apiExplorer.tabCode') },
    ],
    [t]
  );

  const tagLabel = (tag) =>
    tag === OTHER_TAG ? t('apiExplorer.otherGroup') : tag === SYSTEM_TAG ? t('nav.endpoints') : tag;

  // ---- Render ----
  if (loading) {
    return (
      <div className="view api-explorer">
        <PageHeader title={t('apiExplorer.title')} subtitle={t('apiExplorer.subtitle')} />
        <div className="api-explorer__loading">{t('common.loading')}</div>
      </div>
    );
  }

  if (loadError || !spec) {
    return (
      <div className="view api-explorer">
        <PageHeader title={t('apiExplorer.title')} subtitle={t('apiExplorer.subtitle')} />
        <ErrorBanner message={t('apiExplorer.specError')} onRetry={loadSpec} />
        <p className="api-explorer__note">{t('apiExplorer.specErrorNote')}</p>
      </div>
    );
  }

  return (
    <div className="view api-explorer">
      <PageHeader
        title={t('apiExplorer.title')}
        subtitle={spec.info?.title ? `${spec.info.title} · ${t('apiExplorer.subtitle')}` : t('apiExplorer.subtitle')}
      />

      <div className="api-explorer__layout">
        {/* Full-width operation dropdown: categories are optgroups, operations are options */}
        <div className="api-explorer__selectbar">
          <label
            className="api-explorer__select-label"
            htmlFor="ax-operation"
            title={t('apiExplorer.operationsTip')}
          >
            {t('apiExplorer.operations')}
          </label>
          <select
            id="ax-operation"
            className="sb-input api-explorer__select"
            title={t('apiExplorer.operationsTip')}
            value={selectedKey || ''}
            onChange={(e) => {
              const op = operationsByKey.get(e.target.value);
              if (op) selectOperation(op);
            }}
          >
            <option value="" disabled>
              {t('apiExplorer.selectPrompt')}
            </option>
            {groups.map((group) => (
              <optgroup key={group.tag} label={`${tagLabel(group.tag)} (${group.operations.length})`}>
                {group.operations.map((op) => (
                  <option key={op.key} value={op.key}>
                    {op.method} {op.path}
                    {op.summary ? ` — ${op.summary}` : ''}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>

          {history.length > 0 && (
            <Collapsible title={t('apiExplorer.recent')} defaultOpen={false} className="api-explorer__recent">
              <ul className="api-explorer__recent-list">
                {history.map((entry, i) => (
                  <li key={`${entry.method}-${entry.path}-${entry.ts}-${i}`}>
                    <button
                      type="button"
                      className="api-explorer__op"
                      onClick={() => loadFromHistory(entry)}
                    >
                      <MethodBadge method={entry.method} />
                      <span className="api-explorer__op-path">{entry.path}</span>
                      {entry.status != null && <StatusBadge status={entry.status} />}
                    </button>
                  </li>
                ))}
              </ul>
            </Collapsible>
          )}
        </div>

        {/* Request builder, then the response below it */}
        <section className="api-explorer__main" aria-label={t('apiExplorer.requestBuilder')}>
          {!selected ? (
            <EmptyState
              icon={<Icons.Terminal size={28} />}
              message={t('apiExplorer.selectPrompt')}
              hint={t('apiExplorer.selectHint')}
            />
          ) : (
            <>
              <div className="api-explorer__op-header">
                <div className="api-explorer__op-title">
                  <MethodBadge method={selected.method} />
                  <span className="api-explorer__op-title-path">{selected.path}</span>
                </div>
                {selected.summary && (
                  <p className="api-explorer__op-desc">{selected.summary}</p>
                )}
                {selected.operationId && (
                  <p className="api-explorer__op-id">{selected.operationId}</p>
                )}
              </div>

              {/* Parameters */}
              {(pathParams.length > 0 || queryParams.length > 0) && (
                <div className="api-explorer__section">
                  <h3 className="api-explorer__section-title">{t('apiExplorer.parameters')}</h3>
                  <div className="api-explorer__params">
                    {[...pathParams, ...queryParams].map((p) => (
                      <label
                        key={`${p.in}:${p.name}`}
                        className="api-explorer__field"
                        title={t('apiExplorer.paramTip')}
                      >
                        <span className="api-explorer__field-label">
                          <span className="api-explorer__param-name">{p.name}</span>
                          {p.required && (
                            <span className="api-explorer__required" title={t('common.required')}>
                              *
                            </span>
                          )}
                          <Badge tone={p.in === 'path' ? 'accent' : 'info'}>
                            {p.in === 'path' ? t('apiExplorer.inPath') : t('apiExplorer.inQuery')}
                          </Badge>
                        </span>
                        <input
                          type="text"
                          className="sb-input api-explorer__mono-input"
                          title={t('apiExplorer.paramTip')}
                          value={getParam(p)}
                          placeholder={p.schema?.example != null ? String(p.schema.example) : ''}
                          onChange={(e) => setParam(p, e.target.value)}
                        />
                        {p.description && (
                          <span className="api-explorer__field-hint">{p.description}</span>
                        )}
                      </label>
                    ))}
                  </div>
                </div>
              )}

              {/* Headers */}
              <div className="api-explorer__section">
                <h3 className="api-explorer__section-title">{t('apiExplorer.headers')}</h3>
                <div className="api-explorer__headers">
                  {headerRows.map((row) => (
                    <div key={row.id} className="api-explorer__header-row">
                      <input
                        type="text"
                        className="sb-input"
                        title={t('apiExplorer.headerNameTip')}
                        placeholder={t('apiExplorer.headerName')}
                        value={row.key}
                        onChange={(e) => updateHeader(row.id, { key: e.target.value })}
                      />
                      <input
                        type="text"
                        className="sb-input"
                        title={t('apiExplorer.headerValueTip')}
                        placeholder={t('apiExplorer.headerValue')}
                        value={row.value}
                        onChange={(e) => updateHeader(row.id, { value: e.target.value })}
                      />
                      <button
                        type="button"
                        className="sb-btn sb-btn--ghost api-explorer__icon-btn"
                        onClick={() => removeHeader(row.id)}
                        aria-label={t('common.delete')}
                      >
                        <Icons.Close size={16} />
                      </button>
                    </div>
                  ))}
                  <button type="button" className="sb-btn sb-btn--ghost" onClick={addHeader}>
                    <Icons.Plus size={16} />
                    <span>{t('apiExplorer.addHeader')}</span>
                  </button>
                </div>
                <label
                  className="api-explorer__field api-explorer__content-type"
                  title={t('apiExplorer.contentTypeTip')}
                >
                  <span className="api-explorer__field-label">{t('apiExplorer.contentType')}</span>
                  <select
                    className="sb-input"
                    title={t('apiExplorer.contentTypeTip')}
                    value={contentType}
                    onChange={(e) => setContentType(e.target.value)}
                  >
                    {CONTENT_TYPES.map((ct) => (
                      <option key={ct} value={ct}>
                        {ct}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              {/* Body */}
              {hasBody && (
                <div className="api-explorer__section">
                  <h3 className="api-explorer__section-title">{t('apiExplorer.requestBody')}</h3>
                  <textarea
                    className="api-explorer__body-editor"
                    title={t('apiExplorer.requestBodyTip')}
                    value={bodyText}
                    spellCheck={false}
                    rows={8}
                    placeholder={t('apiExplorer.bodyPlaceholder')}
                    onChange={(e) => {
                      setBodyText(e.target.value);
                      if (bodyError) setBodyError(null);
                    }}
                  />
                  {bodyError && (
                    <p className="api-explorer__inline-error" role="alert">
                      {t('apiExplorer.invalidJson')}: {bodyError}
                    </p>
                  )}
                </div>
              )}

              {/* Resolved URL + execute */}
              <div className="api-explorer__section">
                <h3 className="api-explorer__section-title">{t('apiExplorer.resolvedUrl')}</h3>
                <div className="api-explorer__url">
                  <code className="api-explorer__url-text">{resolvedUrl}</code>
                  <CopyButton value={resolvedUrl} />
                </div>
                <div className="api-explorer__actions">
                  <button
                    type="button"
                    className="sb-btn sb-btn--primary"
                    onClick={handleExecute}
                    disabled={running}
                  >
                    {running ? (
                      t('apiExplorer.running')
                    ) : (
                      <>
                        <Icons.ArrowRight size={16} />
                        <span>{t('apiExplorer.execute')}</span>
                      </>
                    )}
                  </button>
                  {requiresConfirm && (
                    <span className="api-explorer__warn-note">
                      <Icons.Warning size={14} /> {t('apiExplorer.confirmHint')}
                    </span>
                  )}
                </div>
              </div>

              {/* Response */}
              {responseError && <ErrorBanner message={responseError} onRetry={runExecute} />}

              {response && (
                <div className="api-explorer__section api-explorer__response">
                  <div className="api-explorer__response-summary">
                    <StatusBadge status={response.status} label={`${response.status} ${response.statusText}`} />
                    <span className="api-explorer__meta">
                      {t('apiExplorer.duration')}: {response.durationMs} ms
                    </span>
                    <span className="api-explorer__meta">
                      {t('apiExplorer.size')}: {response.bytes} B
                    </span>
                  </div>

                  <Segmented
                    options={tabOptions}
                    value={responseTab}
                    onChange={setResponseTab}
                    ariaLabel={t('apiExplorer.responseTabs')}
                  />

                  {responseTab === 'body' && (
                    <div className="api-explorer__code-block">
                      <div className="api-explorer__code-toolbar">
                        <CopyButton value={response.body} />
                      </div>
                      <pre className="api-explorer__pre">
                        {prettyBody != null ? prettyBody : response.body || t('apiExplorer.emptyBody')}
                      </pre>
                    </div>
                  )}

                  {responseTab === 'headers' && (
                    <div className="api-explorer__code-block">
                      <div className="api-explorer__code-toolbar">
                        <CopyButton
                          value={Object.entries(response.headers)
                            .map(([k, v]) => `${k}: ${v}`)
                            .join('\n')}
                        />
                      </div>
                      <table className="api-explorer__headers-table">
                        <tbody>
                          {Object.entries(response.headers).map(([k, v]) => (
                            <tr key={k}>
                              <th scope="row">{k}</th>
                              <td>{v}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {responseTab === 'timing' && (
                    <dl className="api-explorer__timing">
                      <div>
                        <dt>{t('apiExplorer.status')}</dt>
                        <dd>
                          {response.status} {response.statusText}
                        </dd>
                      </div>
                      <div>
                        <dt>{t('apiExplorer.duration')}</dt>
                        <dd>{response.durationMs} ms</dd>
                      </div>
                      <div>
                        <dt>{t('apiExplorer.size')}</dt>
                        <dd>{response.bytes} B</dd>
                      </div>
                    </dl>
                  )}

                  {responseTab === 'code' && (
                    <div className="api-explorer__snippets">
                      {[
                        { label: 'cURL', code: snippets.curl },
                        { label: 'fetch()', code: snippets.fetch },
                        { label: 'C# HttpClient', code: snippets.csharp },
                      ].map((snip) => (
                        <div className="api-explorer__code-block" key={snip.label}>
                          <div className="api-explorer__code-toolbar">
                            <span className="api-explorer__code-label">{snip.label}</span>
                            <CopyButton value={snip.code} />
                          </div>
                          <pre className="api-explorer__pre">{snip.code}</pre>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* Generated code is also available before executing. */}
              {!response && !responseError && (
                <div className="api-explorer__section">
                  <h3 className="api-explorer__section-title">{t('apiExplorer.tabCode')}</h3>
                  <div className="api-explorer__snippets">
                    {[
                      { label: 'cURL', code: snippets.curl },
                      { label: 'fetch()', code: snippets.fetch },
                      { label: 'C# HttpClient', code: snippets.csharp },
                    ].map((snip) => (
                      <div className="api-explorer__code-block" key={snip.label}>
                        <div className="api-explorer__code-toolbar">
                          <span className="api-explorer__code-label">{snip.label}</span>
                          <CopyButton value={snip.code} />
                        </div>
                        <pre className="api-explorer__pre">{snip.code}</pre>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
        </section>
      </div>

      <ConfirmModal
        open={confirmOpen}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={confirmExecute}
        variant="danger"
        title={t('apiExplorer.confirmTitle')}
        message={t('apiExplorer.confirmMessage', {
          method: selected?.method || '',
          path: selected?.path || '',
        })}
        confirmLabel={t('apiExplorer.execute')}
      />
    </div>
  );
}

export default ApiExplorerView;
