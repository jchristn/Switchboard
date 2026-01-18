import { useState, useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import DataTable from '../common/DataTable';
import Modal from '../common/Modal';
import ConfirmModal from '../common/ConfirmModal';
import './Views.css';

function EndpointsView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const location = useLocation();
  const [endpoints, setEndpoints] = useState([]);
  const [routes, setRoutes] = useState([]);
  const [origins, setOrigins] = useState([]);
  const [mappings, setMappings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedEndpoint, setSelectedEndpoint] = useState(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [activeTab, setActiveTab] = useState('routes');
  const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
  const [pendingAction, setPendingAction] = useState(null);
  const [newRoute, setNewRoute] = useState({
    httpMethod: 'GET',
    urlPattern: '/',
    requiresAuthentication: false,
  });
  const [formData, setFormData] = useState({
    identifier: '',
    name: '',
    timeoutMs: 60000,
    loadBalancingMode: 'RoundRobin',
    blockHttp10: false,
    maxRequestBodySize: 536870912,
    captureRequestBody: false,
    captureResponseBody: false,
    captureRequestHeaders: true,
    captureResponseHeaders: true,
    maxCaptureRequestBodySize: 65536,
    maxCaptureResponseBodySize: 65536,
  });
  const [deleteEndpointConfirm, setDeleteEndpointConfirm] = useState({ show: false });
  const [deleteRouteConfirm, setDeleteRouteConfirm] = useState({ show: false, route: null });
  const [removeOriginConfirm, setRemoveOriginConfirm] = useState({ show: false, mapping: null });
  const selectedEndpointGuidRef = useRef(null);
  const pendingSelectIdentifier = useRef(location.state?.selectIdentifier || null);

  useEffect(() => {
    loadData();
  }, []);

  // Handle navigation state to select endpoint by identifier
  useEffect(() => {
    if (pendingSelectIdentifier.current && endpoints.length > 0) {
      const endpoint = endpoints.find(e => e.identifier === pendingSelectIdentifier.current);
      if (endpoint) {
        handleSelectEndpoint(endpoint);
      }
      pendingSelectIdentifier.current = null;
    }
  }, [endpoints]);

  const loadData = async () => {
    try {
      const [endpointsData, routesData, originsData, mappingsData] = await Promise.all([
        apiClient.getEndpoints(),
        apiClient.getRoutes(),
        apiClient.getOrigins(),
        apiClient.getMappings()
      ]);
      setEndpoints(endpointsData);
      setRoutes(routesData);
      setOrigins(originsData);
      setMappings(mappingsData);

      // Update selected endpoint if it still exists (use ref to avoid stale closure)
      if (selectedEndpointGuidRef.current) {
        const updated = endpointsData.find(e => e.guid === selectedEndpointGuidRef.current);
        if (updated) {
          setSelectedEndpoint(updated);
          // Also update form data to match the refreshed endpoint
          setFormData({
            identifier: updated.identifier || '',
            name: updated.name || '',
            timeoutMs: updated.timeoutMs || 60000,
            loadBalancingMode: updated.loadBalancingMode || 'RoundRobin',
            blockHttp10: updated.blockHttp10 || false,
            maxRequestBodySize: updated.maxRequestBodySize || 536870912,
            captureRequestBody: updated.captureRequestBody || false,
            captureResponseBody: updated.captureResponseBody || false,
            captureRequestHeaders: updated.captureRequestHeaders !== false,
            captureResponseHeaders: updated.captureResponseHeaders !== false,
            maxCaptureRequestBodySize: updated.maxCaptureRequestBodySize || 65536,
            maxCaptureResponseBodySize: updated.maxCaptureResponseBodySize || 65536,
          });
        } else {
          setSelectedEndpoint(null);
          selectedEndpointGuidRef.current = null;
        }
      }
    } catch (err) {
      showError('Failed to load data: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const getRoutesForEndpoint = (endpointIdentifier) => {
    return routes.filter(r => r.endpointIdentifier === endpointIdentifier);
  };

  const getMappingsForEndpoint = (endpointIdentifier) => {
    return mappings.filter(m => m.endpointIdentifier === endpointIdentifier);
  };

  const getOriginByIdentifier = (identifier) => {
    return origins.find(o => o.identifier === identifier);
  };

  const getUnmappedOrigins = (endpointIdentifier) => {
    const mappedIdentifiers = getMappingsForEndpoint(endpointIdentifier).map(m => m.originIdentifier);
    return origins.filter(o => !mappedIdentifiers.includes(o.identifier));
  };

  // Check if there are unsaved changes in settings
  const hasUnsavedChanges = () => {
    if (!selectedEndpoint) return false;
    return (
      formData.name !== (selectedEndpoint.name || '') ||
      formData.timeoutMs !== (selectedEndpoint.timeoutMs || 60000) ||
      formData.loadBalancingMode !== (selectedEndpoint.loadBalancingMode || 'RoundRobin') ||
      formData.blockHttp10 !== (selectedEndpoint.blockHttp10 || false) ||
      formData.maxRequestBodySize !== (selectedEndpoint.maxRequestBodySize || 536870912) ||
      formData.captureRequestBody !== (selectedEndpoint.captureRequestBody || false) ||
      formData.captureResponseBody !== (selectedEndpoint.captureResponseBody || false) ||
      formData.captureRequestHeaders !== (selectedEndpoint.captureRequestHeaders !== false) ||
      formData.captureResponseHeaders !== (selectedEndpoint.captureResponseHeaders !== false) ||
      formData.maxCaptureRequestBodySize !== (selectedEndpoint.maxCaptureRequestBodySize || 65536) ||
      formData.maxCaptureResponseBodySize !== (selectedEndpoint.maxCaptureResponseBodySize || 65536)
    );
  };

  // Handle actions that might lose unsaved changes
  const handleWithUnsavedCheck = (action) => {
    if (hasUnsavedChanges()) {
      setPendingAction(() => action);
      setShowUnsavedWarning(true);
    } else {
      action();
    }
  };

  const handleSelectEndpoint = (endpoint) => {
    const doSelect = () => {
      setSelectedEndpoint(endpoint);
      selectedEndpointGuidRef.current = endpoint?.guid || null;
      setActiveTab('routes');
      setFormData({
        identifier: endpoint.identifier || '',
        name: endpoint.name || '',
        timeoutMs: endpoint.timeoutMs || 60000,
        loadBalancingMode: endpoint.loadBalancingMode || 'RoundRobin',
        blockHttp10: endpoint.blockHttp10 || false,
        maxRequestBodySize: endpoint.maxRequestBodySize || 536870912,
        captureRequestBody: endpoint.captureRequestBody || false,
        captureResponseBody: endpoint.captureResponseBody || false,
        captureRequestHeaders: endpoint.captureRequestHeaders !== false,
        captureResponseHeaders: endpoint.captureResponseHeaders !== false,
        maxCaptureRequestBodySize: endpoint.maxCaptureRequestBodySize || 65536,
        maxCaptureResponseBodySize: endpoint.maxCaptureResponseBodySize || 65536,
      });
    };

    if (selectedEndpoint && endpoint.guid !== selectedEndpoint.guid) {
      handleWithUnsavedCheck(doSelect);
    } else {
      doSelect();
    }
  };

  const handleAddNew = () => {
    setFormData({
      identifier: '',
      name: '',
      timeoutMs: 60000,
      loadBalancingMode: 'RoundRobin',
      blockHttp10: false,
      maxRequestBodySize: 536870912,
      captureRequestBody: false,
      captureResponseBody: false,
      captureRequestHeaders: true,
      captureResponseHeaders: true,
      maxCaptureRequestBodySize: 65536,
      maxCaptureResponseBodySize: 65536,
    });
    setShowAddModal(true);
  };

  const handleCreateEndpoint = async (e) => {
    e.preventDefault();
    try {
      await apiClient.createEndpoint(formData);
      showSuccess('Endpoint created successfully');
      setShowAddModal(false);
      loadData();
    } catch (err) {
      showError('Failed to create endpoint: ' + err.message);
    }
  };

  const handleResetForm = () => {
    if (selectedEndpoint) {
      setFormData({
        identifier: selectedEndpoint.identifier || '',
        name: selectedEndpoint.name || '',
        timeoutMs: selectedEndpoint.timeoutMs || 60000,
        loadBalancingMode: selectedEndpoint.loadBalancingMode || 'RoundRobin',
        blockHttp10: selectedEndpoint.blockHttp10 || false,
        maxRequestBodySize: selectedEndpoint.maxRequestBodySize || 536870912,
        captureRequestBody: selectedEndpoint.captureRequestBody || false,
        captureResponseBody: selectedEndpoint.captureResponseBody || false,
        captureRequestHeaders: selectedEndpoint.captureRequestHeaders !== false,
        captureResponseHeaders: selectedEndpoint.captureResponseHeaders !== false,
        maxCaptureRequestBodySize: selectedEndpoint.maxCaptureRequestBodySize || 65536,
        maxCaptureResponseBodySize: selectedEndpoint.maxCaptureResponseBodySize || 65536,
      });
    }
  };

  const handleSaveEndpoint = async () => {
    try {
      const updatedEndpoint = await apiClient.updateEndpoint(selectedEndpoint.guid, formData);
      showSuccess('Endpoint settings saved');
      // Update the selected endpoint with the response data
      setSelectedEndpoint(updatedEndpoint);
      // Update the endpoints list
      setEndpoints(prev => prev.map(e => e.guid === updatedEndpoint.guid ? updatedEndpoint : e));
      // Update form data to match
      setFormData({
        identifier: updatedEndpoint.identifier || '',
        name: updatedEndpoint.name || '',
        timeoutMs: updatedEndpoint.timeoutMs || 60000,
        loadBalancingMode: updatedEndpoint.loadBalancingMode || 'RoundRobin',
        blockHttp10: updatedEndpoint.blockHttp10 || false,
        maxRequestBodySize: updatedEndpoint.maxRequestBodySize || 536870912,
        captureRequestBody: updatedEndpoint.captureRequestBody || false,
        captureResponseBody: updatedEndpoint.captureResponseBody || false,
        captureRequestHeaders: updatedEndpoint.captureRequestHeaders !== false,
        captureResponseHeaders: updatedEndpoint.captureResponseHeaders !== false,
        maxCaptureRequestBodySize: updatedEndpoint.maxCaptureRequestBodySize || 65536,
        maxCaptureResponseBodySize: updatedEndpoint.maxCaptureResponseBodySize || 65536,
      });
    } catch (err) {
      showError('Failed to save endpoint: ' + err.message);
    }
  };

  const handleClosePanel = () => {
    const doClose = () => {
      setSelectedEndpoint(null);
      selectedEndpointGuidRef.current = null;
    };
    handleWithUnsavedCheck(doClose);
  };

  const handleDiscardChanges = () => {
    setShowUnsavedWarning(false);
    if (pendingAction) {
      pendingAction();
      setPendingAction(null);
    }
  };

  const handleDeleteEndpointClick = () => {
    setDeleteEndpointConfirm({ show: true });
  };

  const handleDeleteEndpointConfirm = async () => {
    setDeleteEndpointConfirm({ show: false });

    try {
      await apiClient.deleteEndpoint(selectedEndpoint.guid);
      showSuccess('Endpoint deleted successfully');
      setSelectedEndpoint(null);
      selectedEndpointGuidRef.current = null;
      loadData();
    } catch (err) {
      showError('Failed to delete endpoint: ' + err.message);
    }
  };

  const handleAddRoute = async () => {
    if (!newRoute.urlPattern || newRoute.urlPattern.trim() === '') {
      showError('URL pattern is required');
      return;
    }

    try {
      const createdRoute = await apiClient.createRoute({
        endpointIdentifier: selectedEndpoint.identifier,
        endpointGuid: selectedEndpoint.guid,
        httpMethod: newRoute.httpMethod,
        urlPattern: newRoute.urlPattern,
        requiresAuthentication: newRoute.requiresAuthentication,
      });
      showSuccess('Route added successfully');
      setNewRoute({ httpMethod: 'GET', urlPattern: '/', requiresAuthentication: false });
      // Add the new route to the routes list without reloading everything
      setRoutes(prev => [...prev, createdRoute]);
    } catch (err) {
      showError('Failed to add route: ' + err.message);
    }
  };

  const handleDeleteRouteClick = (route) => {
    setDeleteRouteConfirm({ show: true, route });
  };

  const handleDeleteRouteConfirm = async () => {
    const route = deleteRouteConfirm.route;
    setDeleteRouteConfirm({ show: false, route: null });

    try {
      await apiClient.deleteRoute(route.id);
      showSuccess('Route deleted successfully');
      loadData();
    } catch (err) {
      showError('Failed to delete route: ' + err.message);
    }
  };

  const handleAddOriginMapping = async (originIdentifier) => {
    const origin = getOriginByIdentifier(originIdentifier);
    if (!origin) return;

    try {
      await apiClient.createMapping({
        endpointIdentifier: selectedEndpoint.identifier,
        endpointGuid: selectedEndpoint.guid,
        originIdentifier: origin.identifier,
        originGuid: origin.guid,
      });
      showSuccess(`Origin "${origin.name || origin.identifier}" added`);
      loadData();
    } catch (err) {
      showError('Failed to add origin: ' + err.message);
    }
  };

  const handleRemoveOriginClick = (mapping) => {
    setRemoveOriginConfirm({ show: true, mapping });
  };

  const handleRemoveOriginConfirm = async () => {
    const mapping = removeOriginConfirm.mapping;
    setRemoveOriginConfirm({ show: false, mapping: null });

    try {
      await apiClient.deleteMapping(mapping.id);
      showSuccess('Origin removed');
      loadData();
    } catch (err) {
      showError('Failed to remove origin: ' + err.message);
    }
  };

  const columns = [
    { key: 'identifier', label: 'Identifier' },
    { key: 'name', label: 'Name' },
    {
      key: 'routes',
      label: 'Routes',
      render: (_, row) => {
        const endpointRoutes = getRoutesForEndpoint(row.identifier);
        if (endpointRoutes.length === 0) {
          return <span className="text-muted">None</span>;
        }
        return <span className="badge badge-info">{endpointRoutes.length}</span>;
      },
    },
    {
      key: 'origins',
      label: 'Origins',
      render: (_, row) => {
        const endpointMappings = getMappingsForEndpoint(row.identifier);
        if (endpointMappings.length === 0) {
          return <span className="text-muted">None</span>;
        }
        return <span className="badge badge-purple">{endpointMappings.length}</span>;
      },
    },
    { key: 'loadBalancingMode', label: 'Load Balancing' },
  ];

  if (loading) {
    return (
      <div className="view-loading">
        <div className="spinner"></div>
        <p>Loading...</p>
      </div>
    );
  }

  const selectedRoutes = selectedEndpoint ? getRoutesForEndpoint(selectedEndpoint.identifier) : [];
  const preAuthRoutes = selectedRoutes.filter(r => !r.requiresAuthentication);
  const authRoutes = selectedRoutes.filter(r => r.requiresAuthentication);
  const selectedMappings = selectedEndpoint ? getMappingsForEndpoint(selectedEndpoint.identifier) : [];
  const unmappedOrigins = selectedEndpoint ? getUnmappedOrigins(selectedEndpoint.identifier) : [];

  return (
    <div className="view master-detail-view">
      {/* Master: Endpoints Table */}
      <div className="master-panel">
        <div className="view-header">
          <h2 className="view-title">API Endpoints</h2>
          <button className="btn btn-primary" onClick={handleAddNew}>
            Add Endpoint
          </button>
        </div>

        <DataTable
          columns={columns}
          data={endpoints}
          emptyMessage="No API endpoints configured"
          onRowClick={handleSelectEndpoint}
          selectedRow={selectedEndpoint}
          rowKey="guid"
        />
      </div>

      {/* Detail: Selected Endpoint */}
      {selectedEndpoint && (
        <div className="detail-panel">
          <div className="detail-panel-header">
            <div className="detail-panel-title">
              <h3>{selectedEndpoint.name || selectedEndpoint.identifier}</h3>
              {hasUnsavedChanges() && <span className="unsaved-indicator">Unsaved changes</span>}
            </div>
            <div className="detail-panel-actions">
              {hasUnsavedChanges() && (
                <>
                  <button className="btn btn-secondary btn-sm" onClick={handleResetForm}>
                    Reset
                  </button>
                  <button className="btn btn-primary btn-sm" onClick={handleSaveEndpoint}>
                    Save
                  </button>
                </>
              )}
              <button className="btn btn-danger btn-sm" onClick={handleDeleteEndpointClick}>
                Delete
              </button>
              <button className="btn btn-ghost btn-sm" onClick={handleClosePanel}>
                Close
              </button>
            </div>
          </div>

          {/* Tab Navigation */}
          <div className="detail-tabs">
            <button
              className={`detail-tab ${activeTab === 'routes' ? 'active' : ''}`}
              onClick={() => setActiveTab('routes')}
            >
              URL Routes
              <span className="badge badge-sm">{selectedRoutes.length}</span>
            </button>
            <button
              className={`detail-tab ${activeTab === 'origins' ? 'active' : ''}`}
              onClick={() => setActiveTab('origins')}
            >
              Origin Servers
              <span className="badge badge-sm">{selectedMappings.length}</span>
            </button>
            <button
              className={`detail-tab ${activeTab === 'settings' ? 'active' : ''}`}
              onClick={() => setActiveTab('settings')}
            >
              Settings
            </button>
          </div>

          <div className="detail-panel-content">
            {/* Routes Tab */}
            {activeTab === 'routes' && (
              <div className="tab-content">
                {/* Add Route Form - Prominent */}
                <div className="add-form-section">
                  <h4>Add New Route</h4>
                  <div className="add-route-form">
                    <select
                      className="form-input route-method-select"
                      value={newRoute.httpMethod}
                      onChange={(e) => setNewRoute({ ...newRoute, httpMethod: e.target.value })}
                    >
                      <option value="GET">GET</option>
                      <option value="POST">POST</option>
                      <option value="PUT">PUT</option>
                      <option value="PATCH">PATCH</option>
                      <option value="DELETE">DELETE</option>
                      <option value="HEAD">HEAD</option>
                      <option value="OPTIONS">OPTIONS</option>
                    </select>
                    <input
                      type="text"
                      className="form-input route-url-input"
                      placeholder="/api/resource/{id}"
                      value={newRoute.urlPattern}
                      onChange={(e) => setNewRoute({ ...newRoute, urlPattern: e.target.value })}
                    />
                    <label className="form-checkbox route-auth-checkbox">
                      <input
                        type="checkbox"
                        checked={newRoute.requiresAuthentication}
                        onChange={(e) => setNewRoute({ ...newRoute, requiresAuthentication: e.target.checked })}
                      />
                      Requires Auth
                    </label>
                    <button type="button" className="btn btn-primary" onClick={handleAddRoute}>
                      Add Route
                    </button>
                  </div>
                </div>

                {/* Routes Lists */}
                <div className="routes-panels">
                  {/* Pre-Auth Routes */}
                  <div className="routes-panel">
                    <div className="routes-panel-header">
                      <h5>Pre-Authentication Routes</h5>
                      <span className="badge badge-info">{preAuthRoutes.length}</span>
                    </div>
                    <div className="routes-panel-content">
                      {preAuthRoutes.length === 0 ? (
                        <p className="text-muted">No pre-auth routes configured</p>
                      ) : (
                        <div className="routes-list-compact">
                          {preAuthRoutes.map((route) => (
                            <div key={route.id} className="route-row">
                              <span className={`badge badge-method badge-${route.httpMethod?.toLowerCase() || 'any'}`}>
                                {route.httpMethod || 'ANY'}
                              </span>
                              <code className="route-url-display">{route.urlPattern}</code>
                              <button
                                className="btn btn-ghost btn-xs route-delete-btn"
                                onClick={() => handleDeleteRouteClick(route)}
                                title="Delete route"
                              >
                                ×
                              </button>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Authenticated Routes */}
                  <div className="routes-panel">
                    <div className="routes-panel-header">
                      <h5>Authenticated Routes</h5>
                      <span className="badge badge-warning">{authRoutes.length}</span>
                    </div>
                    <div className="routes-panel-content">
                      {authRoutes.length === 0 ? (
                        <p className="text-muted">No authenticated routes configured</p>
                      ) : (
                        <div className="routes-list-compact">
                          {authRoutes.map((route) => (
                            <div key={route.id} className="route-row">
                              <span className={`badge badge-method badge-${route.httpMethod?.toLowerCase() || 'any'}`}>
                                {route.httpMethod || 'ANY'}
                              </span>
                              <code className="route-url-display">{route.urlPattern}</code>
                              <button
                                className="btn btn-ghost btn-xs route-delete-btn"
                                onClick={() => handleDeleteRouteClick(route)}
                                title="Delete route"
                              >
                                ×
                              </button>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Origins Tab */}
            {activeTab === 'origins' && (
              <div className="tab-content">
                {/* Add Origin Mapping */}
                <div className="add-form-section">
                  <h4>Add Origin Server</h4>
                  {unmappedOrigins.length === 0 ? (
                    <p className="text-muted">All origin servers are already mapped to this endpoint.</p>
                  ) : (
                    <div className="add-origin-form">
                      <select
                        id="origin-select"
                        className="form-input"
                        defaultValue=""
                        onChange={(e) => {
                          if (e.target.value) {
                            handleAddOriginMapping(e.target.value);
                            e.target.value = '';
                          }
                        }}
                      >
                        <option value="" disabled>Select an origin server to add...</option>
                        {unmappedOrigins.map((origin) => (
                          <option key={origin.guid} value={origin.identifier}>
                            {origin.name || origin.identifier} ({origin.hostname}:{origin.port})
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>

                {/* Mapped Origins List */}
                <div className="origins-section">
                  <h4>Mapped Origin Servers</h4>
                  {selectedMappings.length === 0 ? (
                    <div className="empty-state">
                      <p>No origin servers mapped to this endpoint.</p>
                      <p className="text-muted">Add an origin server above to enable request routing.</p>
                    </div>
                  ) : (
                    <div className="origins-list">
                      {selectedMappings.map((mapping) => {
                        const origin = getOriginByIdentifier(mapping.originIdentifier);
                        return (
                          <div key={mapping.id} className="origin-card">
                            <div className="origin-card-info">
                              <div className="origin-card-name">
                                {origin?.name || mapping.originIdentifier}
                              </div>
                              <div className="origin-card-details">
                                <code>{origin?.hostname || '?'}:{origin?.port || '?'}</code>
                                {origin?.ssl && <span className="badge badge-success">SSL</span>}
                              </div>
                            </div>
                            <button
                              className="btn btn-danger btn-sm"
                              onClick={() => handleRemoveOriginClick(mapping)}
                            >
                              Remove
                            </button>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Settings Tab */}
            {activeTab === 'settings' && (
              <div className="tab-content">
                <div className="settings-edit">
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-label">Identifier</label>
                      <input
                        type="text"
                        className="form-input"
                        value={formData.identifier}
                        disabled
                      />
                    </div>
                    <div className="form-group">
                      <label className="form-label">Name</label>
                      <input
                        type="text"
                        className="form-input"
                        value={formData.name}
                        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                      />
                    </div>
                  </div>
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-label">Load Balancing</label>
                      <select
                        className="form-input"
                        value={formData.loadBalancingMode}
                        onChange={(e) => setFormData({ ...formData, loadBalancingMode: e.target.value })}
                      >
                        <option value="RoundRobin">Round Robin</option>
                        <option value="Random">Random</option>
                      </select>
                    </div>
                    <div className="form-group">
                      <label className="form-label">Timeout (ms)</label>
                      <input
                        type="number"
                        className="form-input"
                        value={formData.timeoutMs}
                        onChange={(e) => setFormData({ ...formData, timeoutMs: parseInt(e.target.value) })}
                      />
                    </div>
                  </div>
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-label">Max Body Size (bytes)</label>
                      <input
                        type="number"
                        className="form-input"
                        value={formData.maxRequestBodySize}
                        onChange={(e) => setFormData({ ...formData, maxRequestBodySize: parseInt(e.target.value) })}
                      />
                    </div>
                    <div className="form-group">
                      <label className="form-checkbox" style={{ marginTop: '28px' }}>
                        <input
                          type="checkbox"
                          checked={formData.blockHttp10}
                          onChange={(e) => setFormData({ ...formData, blockHttp10: e.target.checked })}
                        />
                        Block HTTP/1.0 requests
                      </label>
                    </div>
                  </div>

                  <h4 style={{ marginTop: '24px', marginBottom: '16px' }}>Request History Capture</h4>
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-checkbox">
                        <input
                          type="checkbox"
                          checked={formData.captureRequestHeaders}
                          onChange={(e) => setFormData({ ...formData, captureRequestHeaders: e.target.checked })}
                        />
                        Capture Request Headers
                      </label>
                    </div>
                    <div className="form-group">
                      <label className="form-checkbox">
                        <input
                          type="checkbox"
                          checked={formData.captureResponseHeaders}
                          onChange={(e) => setFormData({ ...formData, captureResponseHeaders: e.target.checked })}
                        />
                        Capture Response Headers
                      </label>
                    </div>
                  </div>
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-checkbox">
                        <input
                          type="checkbox"
                          checked={formData.captureRequestBody}
                          onChange={(e) => setFormData({ ...formData, captureRequestBody: e.target.checked })}
                        />
                        Capture Request Body
                      </label>
                    </div>
                    <div className="form-group">
                      <label className="form-checkbox">
                        <input
                          type="checkbox"
                          checked={formData.captureResponseBody}
                          onChange={(e) => setFormData({ ...formData, captureResponseBody: e.target.checked })}
                        />
                        Capture Response Body
                      </label>
                    </div>
                  </div>
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-label">Max Capture Request Body (bytes)</label>
                      <input
                        type="number"
                        className="form-input"
                        value={formData.maxCaptureRequestBodySize}
                        onChange={(e) => setFormData({ ...formData, maxCaptureRequestBodySize: parseInt(e.target.value) })}
                      />
                    </div>
                    <div className="form-group">
                      <label className="form-label">Max Capture Response Body (bytes)</label>
                      <input
                        type="number"
                        className="form-input"
                        value={formData.maxCaptureResponseBodySize}
                        onChange={(e) => setFormData({ ...formData, maxCaptureResponseBodySize: parseInt(e.target.value) })}
                      />
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Placeholder when nothing selected */}
      {!selectedEndpoint && endpoints.length > 0 && (
        <div className="detail-panel detail-panel-empty">
          <p>Select an endpoint from the table above to view and manage its routes, origin servers, and settings.</p>
        </div>
      )}

      {/* Add Endpoint Modal */}
      {showAddModal && (
        <Modal title="Add Endpoint" onClose={() => setShowAddModal(false)}>
          <form onSubmit={handleCreateEndpoint}>
            <div className="form-group">
              <label className="form-label">Identifier</label>
              <input
                type="text"
                className="form-input"
                value={formData.identifier}
                onChange={(e) => setFormData({ ...formData, identifier: e.target.value })}
                required
                placeholder="my-api"
              />
            </div>
            <div className="form-group">
              <label className="form-label">Name</label>
              <input
                type="text"
                className="form-input"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="My API"
              />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Load Balancing</label>
                <select
                  className="form-input"
                  value={formData.loadBalancingMode}
                  onChange={(e) => setFormData({ ...formData, loadBalancingMode: e.target.value })}
                >
                  <option value="RoundRobin">Round Robin</option>
                  <option value="Random">Random</option>
                </select>
              </div>
              <div className="form-group">
                <label className="form-label">Timeout (ms)</label>
                <input
                  type="number"
                  className="form-input"
                  value={formData.timeoutMs}
                  onChange={(e) => setFormData({ ...formData, timeoutMs: parseInt(e.target.value) })}
                />
              </div>
            </div>
            <div className="form-group">
              <label className="form-checkbox">
                <input
                  type="checkbox"
                  checked={formData.blockHttp10}
                  onChange={(e) => setFormData({ ...formData, blockHttp10: e.target.checked })}
                />
                Block HTTP/1.0 requests
              </label>
            </div>
            <p className="form-hint">You can add URL routes and origin servers after creating the endpoint.</p>
            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={() => setShowAddModal(false)}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary">
                Create Endpoint
              </button>
            </div>
          </form>
        </Modal>
      )}

      {deleteEndpointConfirm.show && (
        <ConfirmModal
          title="Delete Endpoint"
          message="Are you sure you want to delete this endpoint? This will also delete all associated routes and origin mappings."
          entityName={selectedEndpoint?.identifier}
          confirmLabel="Delete"
          onConfirm={handleDeleteEndpointConfirm}
          onClose={() => setDeleteEndpointConfirm({ show: false })}
        />
      )}

      {deleteRouteConfirm.show && (
        <ConfirmModal
          title="Delete Route"
          message="Are you sure you want to delete this route?"
          entityName={`${deleteRouteConfirm.route?.httpMethod} ${deleteRouteConfirm.route?.urlPattern}`}
          confirmLabel="Delete"
          onConfirm={handleDeleteRouteConfirm}
          onClose={() => setDeleteRouteConfirm({ show: false, route: null })}
        />
      )}

      {removeOriginConfirm.show && (
        <ConfirmModal
          title="Remove Origin"
          message="Are you sure you want to remove this origin server from this endpoint?"
          entityName={getOriginByIdentifier(removeOriginConfirm.mapping?.originIdentifier)?.name || removeOriginConfirm.mapping?.originIdentifier}
          confirmLabel="Remove"
          onConfirm={handleRemoveOriginConfirm}
          onClose={() => setRemoveOriginConfirm({ show: false, mapping: null })}
        />
      )}

      {showUnsavedWarning && (
        <ConfirmModal
          title="Unsaved Changes"
          message="You have unsaved changes to the endpoint settings. Are you sure you want to discard them?"
          warningMessage="Your changes will be lost."
          confirmLabel="Discard"
          confirmVariant="warning"
          onConfirm={handleDiscardChanges}
          onClose={() => { setShowUnsavedWarning(false); setPendingAction(null); }}
        />
      )}
    </div>
  );
}

export default EndpointsView;
