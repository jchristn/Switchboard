import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import DataTable from '../common/DataTable';
import ConfirmModal from '../common/ConfirmModal';
import './Views.css';

// Format timestamp with microsecond precision: yyyy-MM-dd HH:mm:ss.ffffffZ
function formatPreciseTimestamp(timestamp) {
  if (!timestamp) return 'N/A';
  const date = new Date(timestamp);
  const year = date.getUTCFullYear();
  const month = String(date.getUTCMonth() + 1).padStart(2, '0');
  const day = String(date.getUTCDate()).padStart(2, '0');
  const hours = String(date.getUTCHours()).padStart(2, '0');
  const minutes = String(date.getUTCMinutes()).padStart(2, '0');
  const seconds = String(date.getUTCSeconds()).padStart(2, '0');
  const ms = String(date.getUTCMilliseconds()).padStart(3, '0');
  // JavaScript Date only has millisecond precision, pad with 000 for microseconds
  return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}.${ms}000Z`;
}

// Copyable value with inline copy button
function CopyableValue({ value, link, linkState, mono = false }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  if (!value) return <span className="detail-value">N/A</span>;

  const content = link ? (
    <Link to={link} state={linkState} className={`detail-link ${mono ? 'mono' : ''}`}>{value}</Link>
  ) : (
    <span className={mono ? 'mono' : ''}>{value}</span>
  );

  return (
    <span className={`detail-value copyable-value ${mono ? 'detail-value-mono' : ''}`}>
      {content}
      <button
        className={`inline-copy-btn ${copied ? 'copied' : ''}`}
        onClick={handleCopy}
        title={copied ? 'Copied!' : 'Copy to clipboard'}
      >
        {copied ? (
          <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
        ) : (
          <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
          </svg>
        )}
      </button>
    </span>
  );
}

// Check if string is valid JSON
function isJson(str) {
  if (!str || typeof str !== 'string') return false;
  const trimmed = str.trim();
  if ((!trimmed.startsWith('{') && !trimmed.startsWith('[')) ||
      (!trimmed.endsWith('}') && !trimmed.endsWith(']'))) {
    return false;
  }
  try {
    JSON.parse(str);
    return true;
  } catch {
    return false;
  }
}

// Format content for display
function formatContent(content, prettyPrint) {
  if (!content || !prettyPrint) return content;

  if (isJson(content)) {
    try {
      return JSON.stringify(JSON.parse(content), null, 2);
    } catch {
      return content;
    }
  }
  return content;
}

// Copyable code block component with collapse/expand
function CopyableCodeBlock({ content, label, defaultExpanded = true }) {
  const [copied, setCopied] = useState(false);
  const [prettyPrint, setPrettyPrint] = useState(true);
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [fullHeight, setFullHeight] = useState(false);

  const handleCopy = async () => {
    try {
      // Always copy the original content, not the formatted version
      await navigator.clipboard.writeText(content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  if (!content) return null;

  const canPrettyPrint = isJson(content);
  const displayContent = formatContent(content, prettyPrint);

  return (
    <div className={`copyable-code-block ${expanded ? 'expanded' : 'collapsed'}`}>
      <div className="copyable-code-header" onClick={() => setExpanded(!expanded)}>
        <div className="copyable-code-title">
          <button
            className="expand-btn"
            title={expanded ? 'Collapse' : 'Expand'}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points={expanded ? "6 9 12 15 18 9" : "9 18 15 12 9 6"}></polyline>
            </svg>
          </button>
          <span className="copyable-code-label">{label}</span>
        </div>
        <div className="copyable-code-actions" onClick={(e) => e.stopPropagation()}>
          {expanded && (
            <button
              className={`height-btn ${fullHeight ? 'active' : ''}`}
              onClick={() => setFullHeight(!fullHeight)}
              title={fullHeight ? 'Constrain height' : 'Expand to full height'}
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                {fullHeight ? (
                  <>
                    <polyline points="4 14 10 14 10 20"></polyline>
                    <polyline points="20 10 14 10 14 4"></polyline>
                    <line x1="14" y1="10" x2="21" y2="3"></line>
                    <line x1="3" y1="21" x2="10" y2="14"></line>
                  </>
                ) : (
                  <>
                    <polyline points="15 3 21 3 21 9"></polyline>
                    <polyline points="9 21 3 21 3 15"></polyline>
                    <line x1="21" y1="3" x2="14" y2="10"></line>
                    <line x1="3" y1="21" x2="10" y2="14"></line>
                  </>
                )}
              </svg>
            </button>
          )}
          {canPrettyPrint && (
            <button
              className={`format-btn ${prettyPrint ? 'active' : ''}`}
              onClick={() => setPrettyPrint(!prettyPrint)}
              title={prettyPrint ? 'Show raw' : 'Pretty print'}
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="4 7 4 4 20 4 20 7"></polyline>
                <line x1="9" y1="20" x2="15" y2="20"></line>
                <line x1="12" y1="4" x2="12" y2="20"></line>
              </svg>
            </button>
          )}
          <button
            className={`copy-btn ${copied ? 'copied' : ''}`}
            onClick={handleCopy}
            title={copied ? 'Copied!' : 'Copy to clipboard'}
          >
            {copied ? (
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12"></polyline>
              </svg>
            ) : (
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
              </svg>
            )}
          </button>
        </div>
      </div>
      {expanded && (
        <pre className={`copyable-code-content ${fullHeight ? 'full-height' : ''}`}>{displayContent}</pre>
      )}
    </div>
  );
}

function HistoryView() {
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedRequest, setSelectedRequest] = useState(null);
  const [filters, setFilters] = useState({
    take: 50,
  });
  const [showCleanupConfirm, setShowCleanupConfirm] = useState(false);

  useEffect(() => {
    loadHistory();
  }, [filters]);

  const loadHistory = async () => {
    setLoading(true);
    try {
      const data = await apiClient.getRecentHistory(filters.take);
      setHistory(data);
    } catch (err) {
      showError('Failed to load history: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleViewDetails = async (record) => {
    try {
      const details = await apiClient.getHistoryDetail(record.requestId);
      setSelectedRequest(details);
    } catch (err) {
      showError('Failed to load request details: ' + err.message);
    }
  };

  const handleCloseDetails = () => {
    setSelectedRequest(null);
  };

  const handleCleanupClick = () => {
    setShowCleanupConfirm(true);
  };

  const handleCleanupConfirm = async () => {
    setShowCleanupConfirm(false);

    try {
      const result = await apiClient.runHistoryCleanup(7);
      showSuccess(`Cleanup complete: ${result.deletedRecords} records deleted`);
      loadHistory();
    } catch (err) {
      showError('Failed to run cleanup: ' + err.message);
    }
  };

  const formatTimestamp = (timestamp) => {
    return new Date(timestamp).toLocaleString();
  };

  const getStatusBadge = (statusCode) => {
    if (statusCode >= 200 && statusCode < 300) {
      return 'badge-success';
    } else if (statusCode >= 300 && statusCode < 400) {
      return 'badge-info';
    } else if (statusCode >= 400 && statusCode < 500) {
      return 'badge-warning';
    } else {
      return 'badge-danger';
    }
  };

  const columns = [
    {
      key: 'timestampUtc',
      label: 'Timestamp',
      render: (value) => formatTimestamp(value),
    },
    { key: 'httpMethod', label: 'Method' },
    {
      key: 'requestPath',
      label: 'Path',
      render: (value) => (
        <span className="path-cell" title={value}>
          {value.length > 50 ? value.substring(0, 50) + '...' : value}
        </span>
      ),
    },
    {
      key: 'statusCode',
      label: 'Status',
      render: (value) => (
        <span className={`badge ${getStatusBadge(value)}`}>{value}</span>
      ),
    },
    {
      key: 'durationMs',
      label: 'Duration',
      render: (value) => `${value}ms`,
    },
  ];

  return (
    <div className="view master-detail-view history-view">
      {/* Master: History Table */}
      <div className="master-panel">
        <div className="view-header">
          <h2 className="view-title">Request History</h2>
          <div className="view-actions">
            <select
              className="form-input"
              style={{ width: 'auto' }}
              value={filters.take}
              onChange={(e) => setFilters({ ...filters, take: parseInt(e.target.value) })}
            >
              <option value="25">Last 25</option>
              <option value="50">Last 50</option>
              <option value="100">Last 100</option>
              <option value="250">Last 250</option>
            </select>
            <button className="btn btn-secondary" onClick={loadHistory}>
              Refresh
            </button>
            <button className="btn btn-danger" onClick={handleCleanupClick}>
              Cleanup
            </button>
          </div>
        </div>

        {loading ? (
          <div className="view-loading">
            <div className="spinner"></div>
            <p>Loading...</p>
          </div>
        ) : (
          <DataTable
            columns={columns}
            data={history}
            emptyMessage="No request history"
            onRowClick={handleViewDetails}
            selectedRow={selectedRequest}
            rowKey="requestId"
          />
        )}
      </div>

      {/* Detail: Request Details Panel */}
      {selectedRequest && (
        <div className="detail-panel history-detail-panel">
          <div className="detail-panel-header">
            <div className="detail-panel-title">
              <h3>
                <span className={`badge badge-method badge-${selectedRequest.httpMethod?.toLowerCase() || 'any'}`}>
                  {selectedRequest.httpMethod}
                </span>
                <span className="request-path-title">{selectedRequest.requestPath}</span>
              </h3>
            </div>
            <div className="detail-panel-actions">
              <button className="btn btn-ghost btn-sm" onClick={handleCloseDetails}>
                Close
              </button>
            </div>
          </div>

          <div className="detail-panel-content history-detail-content">
            {/* Metadata Section - Two Columns */}
            <div className="history-metadata-grid">
              {/* Request Metadata */}
              <div className="history-metadata-section">
                <h4>Request</h4>
                <div className="detail-row">
                  <span className="detail-label">Request ID:</span>
                  <CopyableValue value={selectedRequest.requestId} mono />
                </div>
                <div className="detail-row">
                  <span className="detail-label">Time:</span>
                  <span className="detail-value detail-value-mono">{formatPreciseTimestamp(selectedRequest.timestampUtc)}</span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Method:</span>
                  <span className="detail-value">{selectedRequest.httpMethod}</span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Path:</span>
                  <span className="detail-value">{selectedRequest.requestPath}</span>
                </div>
                {selectedRequest.queryString && (
                  <div className="detail-row">
                    <span className="detail-label">Query:</span>
                    <span className="detail-value">{selectedRequest.queryString}</span>
                  </div>
                )}
                <div className="detail-row">
                  <span className="detail-label">Client IP:</span>
                  <span className="detail-value">{selectedRequest.clientIp || 'N/A'}</span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Endpoint:</span>
                  <CopyableValue
                    value={selectedRequest.endpointIdentifier}
                    link={selectedRequest.endpointIdentifier ? "/endpoints" : null}
                    linkState={selectedRequest.endpointIdentifier ? { selectIdentifier: selectedRequest.endpointIdentifier } : null}
                  />
                </div>
                <div className="detail-row">
                  <span className="detail-label">Origin:</span>
                  <CopyableValue
                    value={selectedRequest.originIdentifier}
                    link={selectedRequest.originIdentifier ? "/origins" : null}
                    linkState={selectedRequest.originIdentifier ? { selectIdentifier: selectedRequest.originIdentifier } : null}
                  />
                </div>
              </div>

              {/* Response Metadata */}
              <div className="history-metadata-section">
                <h4>Response</h4>
                <div className="detail-row">
                  <span className="detail-label">Status:</span>
                  <span className={`badge ${getStatusBadge(selectedRequest.statusCode)}`}>
                    {selectedRequest.statusCode}
                  </span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Duration:</span>
                  <span className="detail-value">{selectedRequest.durationMs}ms</span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Size:</span>
                  <span className="detail-value">{selectedRequest.responseBodySize} bytes</span>
                </div>
                {selectedRequest.errorMessage && (
                  <div className="detail-row">
                    <span className="detail-label">Error:</span>
                    <span className="detail-value text-danger">{selectedRequest.errorMessage}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Headers Section - Horizontally Aligned */}
            <div className="history-paired-section">
              <div className="history-paired-column">
                <CopyableCodeBlock
                  content={selectedRequest.requestHeaders}
                  label="Request Headers"
                />
              </div>
              <div className="history-paired-column">
                <CopyableCodeBlock
                  content={selectedRequest.responseHeaders}
                  label="Response Headers"
                />
              </div>
            </div>

            {/* Bodies Section - Horizontally Aligned */}
            <div className="history-paired-section">
              <div className="history-paired-column">
                <CopyableCodeBlock
                  content={selectedRequest.requestBody}
                  label="Request Body"
                />
              </div>
              <div className="history-paired-column">
                <CopyableCodeBlock
                  content={selectedRequest.responseBody}
                  label="Response Body"
                />
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Placeholder when nothing selected */}
      {!selectedRequest && history.length > 0 && (
        <div className="detail-panel detail-panel-empty">
          <p>Select a request from the table above to view details.</p>
        </div>
      )}

      {showCleanupConfirm && (
        <ConfirmModal
          title="Cleanup History"
          message="This will delete request history records older than 7 days."
          warningMessage="This action cannot be undone."
          confirmLabel="Run Cleanup"
          confirmVariant="warning"
          onConfirm={handleCleanupConfirm}
          onClose={() => setShowCleanupConfirm(false)}
        />
      )}
    </div>
  );
}

export default HistoryView;
