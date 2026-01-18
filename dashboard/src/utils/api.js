import axios from 'axios';

// Convert PascalCase to camelCase, handling acronyms like GUID
function toCamelCase(str) {
  // Handle all-caps strings like "GUID" -> "guid"
  if (str === str.toUpperCase() && str.length > 1) {
    return str.toLowerCase();
  }
  // Handle mixed case with trailing acronym like "EndpointGUID" -> "endpointGuid"
  // First lowercase the first char, then fix any trailing all-caps sequences
  let result = str.charAt(0).toLowerCase() + str.slice(1);
  // Convert trailing GUID to Guid (e.g., "endpointGUID" -> "endpointGuid")
  result = result.replace(/GUID$/, 'Guid');
  result = result.replace(/ID$/, 'Id');
  result = result.replace(/URL$/, 'Url');
  return result;
}

// Convert camelCase to PascalCase, handling acronyms
function toPascalCase(str) {
  let result = str.charAt(0).toUpperCase() + str.slice(1);
  // Convert trailing Guid back to GUID for server
  result = result.replace(/Guid$/, 'GUID');
  result = result.replace(/Id$/, 'ID');
  result = result.replace(/Url$/, 'URL');
  return result;
}

// Recursively transform object keys to camelCase
function keysToCamelCase(obj) {
  if (Array.isArray(obj)) {
    return obj.map(keysToCamelCase);
  }
  if (obj !== null && typeof obj === 'object') {
    return Object.keys(obj).reduce((result, key) => {
      result[toCamelCase(key)] = keysToCamelCase(obj[key]);
      return result;
    }, {});
  }
  return obj;
}

// Recursively transform object keys to PascalCase
function keysToPascalCase(obj) {
  if (Array.isArray(obj)) {
    return obj.map(keysToPascalCase);
  }
  if (obj !== null && typeof obj === 'object') {
    return Object.keys(obj).reduce((result, key) => {
      result[toPascalCase(key)] = keysToPascalCase(obj[key]);
      return result;
    }, {});
  }
  return obj;
}

export class ApiClient {
  constructor(baseUrl, token, basePath = '/_sb/v1.0') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.token = token;
    this.basePath = basePath.replace(/\/$/, '');

    this.client = axios.create({
      baseURL: `${this.baseUrl}${this.basePath}`,
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor to convert camelCase to PascalCase
    this.client.interceptors.request.use(
      config => {
        if (config.data && typeof config.data === 'object') {
          config.data = keysToPascalCase(config.data);
        }
        return config;
      },
      error => Promise.reject(error)
    );

    // Response interceptor for PascalCase to camelCase conversion and error handling
    this.client.interceptors.response.use(
      response => {
        if (response.data) {
          response.data = keysToCamelCase(response.data);
        }
        return response;
      },
      error => {
        if (error.response) {
          // Handle ApiErrorResponse format (with description, message, error fields)
          const data = error.response.data;
          const message = data?.description || data?.Description ||
                          data?.message || data?.Message ||
                          data?.error || data?.Error ||
                          error.message;
          throw new Error(message);
        } else if (error.request) {
          throw new Error('No response from server');
        } else {
          throw new Error(error.message);
        }
      }
    );
  }

  // Token validation
  async validateToken() {
    try {
      await this.getHealth();
      return true;
    } catch {
      return false;
    }
  }

  // Get current user
  async getMe() {
    const response = await this.client.get('/me');
    return response.data;
  }

  // ==================== Origins ====================

  async getOrigins(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    if (options.search) params.append('search', options.search);
    const response = await this.client.get(`/origins?${params}`);
    return response.data;
  }

  async getOrigin(guid) {
    const response = await this.client.get(`/origins/${guid}`);
    return response.data;
  }

  async createOrigin(data) {
    const response = await this.client.post('/origins', data);
    return response.data;
  }

  async updateOrigin(guid, data) {
    const response = await this.client.put(`/origins/${guid}`, data);
    return response.data;
  }

  async deleteOrigin(guid) {
    await this.client.delete(`/origins/${guid}`);
  }

  // ==================== Endpoints ====================

  async getEndpoints(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    if (options.search) params.append('search', options.search);
    const response = await this.client.get(`/endpoints?${params}`);
    return response.data;
  }

  async getEndpoint(guid) {
    const response = await this.client.get(`/endpoints/${guid}`);
    return response.data;
  }

  async createEndpoint(data) {
    const response = await this.client.post('/endpoints', data);
    return response.data;
  }

  async updateEndpoint(guid, data) {
    const response = await this.client.put(`/endpoints/${guid}`, data);
    return response.data;
  }

  async deleteEndpoint(guid) {
    await this.client.delete(`/endpoints/${guid}`);
  }

  // ==================== Routes ====================

  async getRoutes(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    const response = await this.client.get(`/routes?${params}`);
    return response.data;
  }

  async getRoute(id) {
    const response = await this.client.get(`/routes/${id}`);
    return response.data;
  }

  async createRoute(data) {
    const response = await this.client.post('/routes', data);
    return response.data;
  }

  async updateRoute(id, data) {
    const response = await this.client.put(`/routes/${id}`, data);
    return response.data;
  }

  async deleteRoute(id) {
    await this.client.delete(`/routes/${id}`);
  }

  // ==================== Mappings ====================

  async getMappings(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    const response = await this.client.get(`/mappings?${params}`);
    return response.data;
  }

  async getMapping(id) {
    const response = await this.client.get(`/mappings/${id}`);
    return response.data;
  }

  async createMapping(data) {
    const response = await this.client.post('/mappings', data);
    return response.data;
  }

  async deleteMapping(id) {
    await this.client.delete(`/mappings/${id}`);
  }

  // ==================== URL Rewrites ====================

  async getRewrites(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    const response = await this.client.get(`/rewrites?${params}`);
    return response.data;
  }

  async getRewrite(id) {
    const response = await this.client.get(`/rewrites/${id}`);
    return response.data;
  }

  async createRewrite(data) {
    const response = await this.client.post('/rewrites', data);
    return response.data;
  }

  async updateRewrite(id, data) {
    const response = await this.client.put(`/rewrites/${id}`, data);
    return response.data;
  }

  async deleteRewrite(id) {
    await this.client.delete(`/rewrites/${id}`);
  }

  // ==================== Blocked Headers ====================

  async getBlockedHeaders(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    const response = await this.client.get(`/headers?${params}`);
    return response.data;
  }

  async createBlockedHeader(data) {
    const response = await this.client.post('/headers', data);
    return response.data;
  }

  async deleteBlockedHeader(id) {
    await this.client.delete(`/headers/${id}`);
  }

  // ==================== Users ====================

  async getUsers(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    if (options.search) params.append('search', options.search);
    const response = await this.client.get(`/users?${params}`);
    return response.data;
  }

  async getUser(guid) {
    const response = await this.client.get(`/users/${guid}`);
    return response.data;
  }

  async createUser(data) {
    const response = await this.client.post('/users', data);
    return response.data;
  }

  async updateUser(guid, data) {
    const response = await this.client.put(`/users/${guid}`, data);
    return response.data;
  }

  async deleteUser(guid) {
    await this.client.delete(`/users/${guid}`);
  }

  // ==================== Credentials ====================

  async getCredentials(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    if (options.search) params.append('search', options.search);
    const response = await this.client.get(`/credentials?${params}`);
    return response.data;
  }

  async getCredential(guid) {
    const response = await this.client.get(`/credentials/${guid}`);
    return response.data;
  }

  async createCredential(data) {
    const response = await this.client.post('/credentials', data);
    return response.data;
  }

  async updateCredential(guid, data) {
    const response = await this.client.put(`/credentials/${guid}`, data);
    return response.data;
  }

  async deleteCredential(guid) {
    await this.client.delete(`/credentials/${guid}`);
  }

  async regenerateCredential(guid) {
    const response = await this.client.post(`/credentials/${guid}/regenerate`);
    return response.data;
  }

  // ==================== Request History ====================

  async getHistory(filters = {}) {
    const params = new URLSearchParams();
    if (filters.skip) params.append('skip', filters.skip);
    if (filters.take) params.append('take', filters.take);
    if (filters.start) params.append('start', filters.start);
    if (filters.end) params.append('end', filters.end);
    if (filters.endpoint) params.append('endpoint', filters.endpoint);
    if (filters.origin) params.append('origin', filters.origin);
    const response = await this.client.get(`/history?${params}`);
    return response.data;
  }

  async getRecentHistory(count = 100) {
    const response = await this.client.get(`/history/recent?count=${count}`);
    return response.data;
  }

  async getFailedHistory(options = {}) {
    const params = new URLSearchParams();
    if (options.skip) params.append('skip', options.skip);
    if (options.take) params.append('take', options.take);
    const response = await this.client.get(`/history/failed?${params}`);
    return response.data;
  }

  async getHistoryDetail(id) {
    const response = await this.client.get(`/history/${id}`);
    return response.data;
  }

  async deleteHistory(id) {
    await this.client.delete(`/history/${id}`);
  }

  async runHistoryCleanup(days = 0) {
    const response = await this.client.post(`/history/cleanup?days=${days}`);
    return response.data;
  }

  async getHistoryStats() {
    const response = await this.client.get('/history/stats');
    return response.data;
  }

  // ==================== Health ====================

  async getHealth() {
    const response = await this.client.get('/health');
    return response.data;
  }
}

export default ApiClient;
