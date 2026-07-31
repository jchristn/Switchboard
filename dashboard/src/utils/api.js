// Single hand-rolled API client for the Switchboard management API. Every view goes through this;
// no component calls fetch directly. The server speaks PascalCase JSON, so request bodies are
// converted to PascalCase and responses back to camelCase at the boundary.

function toCamelCase(str) {
  if (str === str.toUpperCase() && str.length > 1) return str.toLowerCase();
  let result = str.charAt(0).toLowerCase() + str.slice(1);
  result = result.replace(/GUID$/, 'Guid');
  result = result.replace(/ID$/, 'Id');
  result = result.replace(/URL$/, 'Url');
  return result;
}

function toPascalCase(str) {
  let result = str.charAt(0).toUpperCase() + str.slice(1);
  result = result.replace(/Guid$/, 'GUID');
  result = result.replace(/Id$/, 'ID');
  result = result.replace(/Url$/, 'URL');
  return result;
}

function keysToCamelCase(obj) {
  if (Array.isArray(obj)) return obj.map(keysToCamelCase);
  if (obj !== null && typeof obj === 'object') {
    return Object.keys(obj).reduce((result, key) => {
      result[toCamelCase(key)] = keysToCamelCase(obj[key]);
      return result;
    }, {});
  }
  return obj;
}

function keysToPascalCase(obj) {
  if (Array.isArray(obj)) return obj.map(keysToPascalCase);
  if (obj !== null && typeof obj === 'object') {
    return Object.keys(obj).reduce((result, key) => {
      result[toPascalCase(key)] = keysToPascalCase(obj[key]);
      return result;
    }, {});
  }
  return obj;
}

/**
 * Error thrown for non-2xx responses. Carries the HTTP status and the parsed error body.
 */
export class ApiError extends Error {
  constructor(status, body, message) {
    super(message || (body && (body.description || body.message || body.error)) || `HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
    this.isNetworkError = status === 0;
  }
}

function buildQuery(params) {
  if (!params) return '';
  const usp = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') usp.append(key, value);
  });
  const qs = usp.toString();
  return qs ? `?${qs}` : '';
}

export class ApiClient {
  constructor(baseUrl, token, basePath = '/_sb/v1.0') {
    this.baseUrl = (baseUrl || '').replace(/\/$/, '');
    this.token = token;
    this.basePath = basePath.replace(/\/$/, '');
  }

  _headers(extra) {
    return {
      Authorization: `Bearer ${this.token}`,
      'Content-Type': 'application/json',
      ...(extra || {}),
    };
  }

  async _request(method, path, { query, body } = {}) {
    const url = `${this.baseUrl}${this.basePath}${path}${buildQuery(query)}`;
    let response;
    try {
      response = await fetch(url, {
        method,
        headers: this._headers(),
        body: body !== undefined ? JSON.stringify(keysToPascalCase(body)) : undefined,
      });
    } catch (err) {
      throw new ApiError(0, null, err.message || 'No response from server');
    }

    if (response.status === 204) return null;

    let payload = null;
    const text = await response.text();
    if (text) {
      try {
        payload = keysToCamelCase(JSON.parse(text));
      } catch {
        payload = text;
      }
    }

    if (response.status === 401) {
      // A genuine authentication failure ends the session. An authorization (permission) failure is
      // surfaced to the caller instead, so a read-only user isn't logged out for attempting a write.
      const code = payload && typeof payload === 'object' ? payload.error : null;
      if (code !== 'AuthorizationFailed') {
        window.dispatchEvent(new CustomEvent('auth:unauthorized'));
      }
    }

    if (!response.ok) {
      throw new ApiError(response.status, payload);
    }
    return payload;
  }

  get(path, query) {
    return this._request('GET', path, { query });
  }
  post(path, body, query) {
    return this._request('POST', path, { body, query });
  }
  put(path, body) {
    return this._request('PUT', path, { body });
  }
  del(path) {
    return this._request('DELETE', path);
  }

  // ---- Session ----
  async validateToken() {
    try {
      await this.getHealth();
      return true;
    } catch {
      return false;
    }
  }
  getMe() {
    return this.get('/me');
  }
  getHealth() {
    return this.get('/health');
  }

  // ---- Origins ----
  getOrigins(options = {}) {
    return this.get('/origins', options);
  }
  getOrigin(guid) {
    return this.get(`/origins/${guid}`);
  }
  // Live health for all origins (rolling history, uptime, timestamps, last error).
  getOriginsHealth() {
    return this.get('/origins/health');
  }
  getOriginHealth(guid) {
    return this.get(`/origins/${guid}/health`);
  }
  createOrigin(data) {
    return this.post('/origins', data);
  }
  updateOrigin(guid, data) {
    return this.put(`/origins/${guid}`, data);
  }
  deleteOrigin(guid) {
    return this.del(`/origins/${guid}`);
  }

  // ---- Endpoints ----
  getEndpoints(options = {}) {
    return this.get('/endpoints', options);
  }
  getEndpoint(guid) {
    return this.get(`/endpoints/${guid}`);
  }
  createEndpoint(data) {
    return this.post('/endpoints', data);
  }
  updateEndpoint(guid, data) {
    return this.put(`/endpoints/${guid}`, data);
  }
  deleteEndpoint(guid) {
    return this.del(`/endpoints/${guid}`);
  }

  // ---- Routes ----
  getRoutes(options = {}) {
    return this.get('/routes', options);
  }
  getRoute(id) {
    return this.get(`/routes/${id}`);
  }
  createRoute(data) {
    return this.post('/routes', data);
  }
  updateRoute(id, data) {
    return this.put(`/routes/${id}`, data);
  }
  deleteRoute(id) {
    return this.del(`/routes/${id}`);
  }

  // ---- Mappings ----
  getMappings(options = {}) {
    return this.get('/mappings', options);
  }
  createMapping(data) {
    return this.post('/mappings', data);
  }
  deleteMapping(id) {
    return this.del(`/mappings/${id}`);
  }

  // ---- URL Rewrites ----
  getRewrites(options = {}) {
    return this.get('/rewrites', options);
  }
  getRewrite(id) {
    return this.get(`/rewrites/${id}`);
  }
  createRewrite(data) {
    return this.post('/rewrites', data);
  }
  updateRewrite(id, data) {
    return this.put(`/rewrites/${id}`, data);
  }
  deleteRewrite(id) {
    return this.del(`/rewrites/${id}`);
  }

  // ---- Blocked Headers ----
  getBlockedHeaders(options = {}) {
    return this.get('/headers', options);
  }
  createBlockedHeader(data) {
    return this.post('/headers', data);
  }
  deleteBlockedHeader(id) {
    return this.del(`/headers/${id}`);
  }

  // ---- Users ----
  getUsers(options = {}) {
    return this.get('/users', options);
  }
  getUser(guid) {
    return this.get(`/users/${guid}`);
  }
  createUser(data) {
    return this.post('/users', data);
  }
  updateUser(guid, data) {
    return this.put(`/users/${guid}`, data);
  }
  deleteUser(guid) {
    return this.del(`/users/${guid}`);
  }

  // ---- Credentials ----
  getCredentials(options = {}) {
    return this.get('/credentials', options);
  }
  getCredential(guid) {
    return this.get(`/credentials/${guid}`);
  }
  createCredential(data) {
    return this.post('/credentials', data);
  }
  updateCredential(guid, data) {
    return this.put(`/credentials/${guid}`, data);
  }
  deleteCredential(guid) {
    return this.del(`/credentials/${guid}`);
  }
  regenerateCredential(guid) {
    return this.post(`/credentials/${guid}/regenerate`);
  }

  // ---- Request History ----
  getHistory(filters = {}) {
    return this.get('/history', filters);
  }
  getRecentHistory(count = 100) {
    return this.get('/history/recent', { count });
  }
  getFailedHistory(options = {}) {
    return this.get('/history/failed', options);
  }
  getHistoryDetail(id) {
    return this.get(`/history/${id}`);
  }
  deleteHistory(id) {
    return this.del(`/history/${id}`);
  }
  runHistoryCleanup(days = 0) {
    return this.post('/history/cleanup', undefined, { days });
  }
  getHistoryStats() {
    return this.get('/history/stats');
  }
  // B1 — bucketed activity for the chart. start/end are ISO8601 UTC; intervalMinutes is the bucket size.
  getHistoryTimeseries({ start, end, intervalMinutes } = {}) {
    return this.get('/history/timeseries', { start, end, intervalMinutes });
  }

  // ---- Settings (B2/B3) ----
  getSettings() {
    return this.get('/settings');
  }
  updateSettings(data) {
    return this.put('/settings', data);
  }

  // ---- System (B4/B5) ----
  restartServer() {
    return this.post('/system/restart');
  }
  validateConfig(config) {
    return this.post('/config/validate', config);
  }

  // ---- API Explorer ----
  // The management OpenAPI document is served at the server root, not under basePath.
  async getOpenApiSpec() {
    const res = await fetch(`${this.baseUrl}/openapi.json`, {
      headers: { Authorization: `Bearer ${this.token}` },
    });
    if (!res.ok) throw new ApiError(res.status, null);
    return res.json();
  }

  // Execute an arbitrary request against the server for the API Explorer, returning raw response
  // metadata (status, headers, body text, timing, size). Path is relative to the server root.
  async execute(method, path, { headers, body } = {}) {
    const url = `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
    const started = performance.now();
    let res;
    try {
      res = await fetch(url, {
        method,
        headers: { Authorization: `Bearer ${this.token}`, ...(headers || {}) },
        body,
        // Do not follow redirects. A proxied API endpoint frequently redirects to an external
        // origin that does not return CORS headers for the dashboard; following it would surface
        // as an opaque "Failed to fetch" instead of the redirect the endpoint actually returned.
        redirect: 'manual',
      });
    } catch (err) {
      throw new ApiError(
        0,
        null,
        (err && err.message) ||
          'Failed to fetch. The endpoint may be unreachable or blocked the request (CORS).'
      );
    }
    const durationMs = Math.round(performance.now() - started);

    // A cross-origin redirect that was not followed comes back as an opaque response with no
    // readable status or body. Report it as a redirect rather than letting it look like a failure.
    if (res.type === 'opaqueredirect') {
      return {
        status: 0,
        statusText: 'Redirect (not followed)',
        headers: {},
        body:
          'The endpoint returned an HTTP redirect. The API Explorer does not follow redirects, ' +
          'which commonly point to an external origin the browser cannot read (CORS).',
        durationMs,
        bytes: 0,
        redirected: true,
      };
    }

    const text = await res.text();
    const respHeaders = {};
    res.headers.forEach((v, k) => {
      respHeaders[k] = v;
    });
    return {
      status: res.status,
      statusText: res.statusText,
      headers: respHeaders,
      body: text,
      durationMs,
      bytes: new Blob([text]).size,
    };
  }
}

export default ApiClient;
