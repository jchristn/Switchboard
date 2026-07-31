import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import { useFormatters } from '../../hooks/useFormatters';
import {
  PageHeader,
  DataTable,
  TablePagination,
  ActionMenu,
  entityActions,
  JsonViewerModal,
  ConfirmModal,
  ActivityChart,
  TIME_RANGES,
  Metric,
  MethodBadge,
  StatusBadge,
  FilterBar,
  Field,
  FilterGrid,
  FilterActions,
} from '../ui';
import RequestDetailsModal from './RequestDetailsModal';
import './HistoryView.css';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

const EMPTY_FILTERS = {
  method: '',
  status: '',
  path: '',
  from: '',
  to: '',
  failedOnly: false,
};

// Converts a millisecond timestamp into the value format a datetime-local input
// expects ("YYYY-MM-DDTHH:mm") in the viewer's local time.
function toLocalInputValue(ms) {
  const d = new Date(ms);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(
    d.getMinutes()
  )}`;
}

// Converts a datetime-local value (local time) into an ISO8601 UTC string for the API.
function localInputToIso(value) {
  if (!value) return undefined;
  const ms = Date.parse(value);
  if (Number.isNaN(ms)) return undefined;
  return new Date(ms).toISOString();
}

function HistoryView() {
  const { t } = useTranslation();
  const fmt = useFormatters();
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();
  const [searchParams] = useSearchParams();

  // Chart state.
  const [rangeId, setRangeId] = useState('hour');
  const [timeseries, setTimeseries] = useState([]);
  const [chartLoading, setChartLoading] = useState(false);

  // KPI stats.
  const [stats, setStats] = useState(null);

  // Table state.
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  // Filters — `?failed=1` presets the failed-only toggle.
  const [filters, setFilters] = useState(() => ({
    ...EMPTY_FILTERS,
    failedOnly: searchParams.get('failed') === '1',
  }));

  // Modals.
  const [viewRecord, setViewRecord] = useState(null);
  const [jsonRecord, setJsonRecord] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const filtersActive = useMemo(
    () =>
      Boolean(
        filters.method ||
          filters.status ||
          filters.path ||
          filters.from ||
          filters.to ||
          filters.failedOnly
      ),
    [filters]
  );

  const updateFilter = (patch) => {
    setFilters((prev) => ({ ...prev, ...patch }));
    setPageNumber(1);
  };

  const clearFilters = () => {
    setFilters({ ...EMPTY_FILTERS });
    setPageNumber(1);
  };

  // ---- Stats ----
  const loadStats = useCallback(async () => {
    try {
      const data = await apiClient.getHistoryStats();
      setStats(data);
    } catch {
      setStats(null);
    }
  }, [apiClient]);

  // ---- Timeseries (chart) ----
  const loadTimeseries = useCallback(async () => {
    const range = TIME_RANGES[rangeId] || TIME_RANGES.hour;
    // Align the requested window to the same bucket grid the chart renders on. Sending a raw "now"
    // (with seconds/millis) would make the server's buckets start on a fractional-bucket offset, and
    // the client would then floor them back onto its clean grid — shifting every bar by up to one
    // bucket relative to the request times shown in the table. Flooring the end to the bucket grid
    // and stepping back a whole number of buckets keeps server and chart buckets aligned 1:1.
    const endMs = Date.now();
    const endStart = Math.floor(endMs / range.bucketMs) * range.bucketMs;
    const startMs = endStart - (range.buckets - 1) * range.bucketMs;
    const end = new Date(endMs);
    const start = new Date(startMs);
    setChartLoading(true);
    try {
      const data = await apiClient.getHistoryTimeseries({
        start: start.toISOString(),
        end: end.toISOString(),
        intervalMinutes: Math.max(1, Math.round(range.bucketMs / 60000)),
      });
      // The endpoint returns { startUtc, endUtc, intervalMinutes, buckets: [...] }; the chart wants the buckets.
      setTimeseries(Array.isArray(data?.buckets) ? data.buckets : Array.isArray(data) ? data : []);
    } catch {
      // New endpoint may not exist yet (404) — render the chart empty rather than error.
      setTimeseries([]);
    } finally {
      setChartLoading(false);
    }
  }, [apiClient, rangeId]);

  // ---- Table rows ----
  const loadRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    const skip = (pageNumber - 1) * pageSize;
    const take = pageSize;
    const start = localInputToIso(filters.from);
    const end = localInputToIso(filters.to);
    // getFailedHistory is the server-side path for failed-only when no date window is
    // applied; otherwise we fetch the window via getHistory and drop successes below.
    const useFailedEndpoint = filters.failedOnly && !start && !end;
    try {
      let data = useFailedEndpoint
        ? await apiClient.getFailedHistory({ skip, take })
        : await apiClient.getHistory({ skip, take, start, end });
      data = Array.isArray(data) ? data : [];

      // Client-side fallback: the backend does not filter by method/status/path (nor
      // by failed when a date window forced us onto getHistory), so apply those here
      // over the fetched page.
      if (filters.failedOnly && !useFailedEndpoint) {
        data = data.filter((r) => r.success === false || (r.statusCode >= 400));
      }
      if (filters.method) {
        const m = filters.method.toUpperCase();
        data = data.filter((r) => String(r.httpMethod || '').toUpperCase() === m);
      }
      if (filters.status) {
        const needle = filters.status.trim().toLowerCase();
        data = data.filter((r) => String(r.statusCode ?? '').toLowerCase().includes(needle));
      }
      if (filters.path) {
        const needle = filters.path.trim().toLowerCase();
        data = data.filter((r) => String(r.requestPath || '').toLowerCase().includes(needle));
      }

      setRows(data);
    } catch (err) {
      setError(err?.message || t('history.loadError'));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [apiClient, pageNumber, pageSize, filters, t]);

  useEffect(() => {
    loadRows();
  }, [loadRows]);

  useEffect(() => {
    loadStats();
  }, [loadStats]);

  useEffect(() => {
    loadTimeseries();
  }, [loadTimeseries]);

  const refreshAll = () => {
    loadRows();
    loadStats();
    loadTimeseries();
  };

  // Clicking a chart bucket scopes the from/to filters to that bucket's window.
  const handleBucketClick = (bucket) => {
    if (!bucket) return;
    const range = TIME_RANGES[rangeId] || TIME_RANGES.hour;
    const startMs = bucket.startMs != null ? bucket.startMs : Date.parse(bucket.bucketStartUtc);
    if (Number.isNaN(startMs)) return;
    updateFilter({
      from: toLocalInputValue(startMs),
      to: toLocalInputValue(startMs + range.bucketMs),
    });
  };

  // ---- Row actions ----
  const openDetail = async (row, mode) => {
    try {
      // Request history is keyed by RequestId (a GUID); the numeric Id is not populated, so it must
      // not take precedence or every lookup would resolve to 0.
      const id = row.requestId ?? row.id;
      const full = await apiClient.getHistoryDetail(id);
      const record = full || row;
      if (mode === 'json') setJsonRecord(record);
      else setViewRecord(record);
    } catch (err) {
      showError(err?.message || t('history.detailLoadError'));
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await apiClient.deleteHistory(deleteTarget.requestId ?? deleteTarget.id);
      showSuccess(t('history.deleted'));
      setDeleteTarget(null);
      refreshAll();
    } catch (err) {
      showError(err?.message || t('history.loadError'));
    } finally {
      setDeleting(false);
    }
  };

  // ---- KPIs ----
  const avgDurationMs = useMemo(() => {
    if (!rows.length) return null;
    const withDuration = rows.filter((r) => typeof r.durationMs === 'number');
    if (!withDuration.length) return null;
    return withDuration.reduce((sum, r) => sum + r.durationMs, 0) / withDuration.length;
  }, [rows]);

  const successRateText = useMemo(() => {
    if (!stats || stats.successRate == null) return '—';
    const rate = stats.successRate > 1 ? stats.successRate / 100 : stats.successRate;
    return fmt.percent(rate, 1);
  }, [stats, fmt]);

  const total = useMemo(() => {
    if (stats) {
      return filters.failedOnly ? stats.failedRequests ?? rows.length : stats.totalRequests ?? rows.length;
    }
    return rows.length;
  }, [stats, filters.failedOnly, rows.length]);

  // ---- Table columns ----
  const columns = useMemo(
    () => [
      {
        key: 'timestampUtc',
        label: t('history.colWhen'),
        sortable: true,
        sortValue: (r) => Date.parse(r.timestampUtc) || 0,
        render: (r) => (
          <span title={`${fmt.dateTime(r.timestampUtc)} · ${fmt.relative(r.timestampUtc)}`}>
            {fmt.dateTime(r.timestampUtc)}
          </span>
        ),
      },
      {
        key: 'httpMethod',
        label: t('history.colMethod'),
        render: (r) => <MethodBadge method={r.httpMethod} />,
      },
      {
        key: 'requestPath',
        label: t('history.colPath'),
        mono: true,
        render: (r) => (
          <span className="history-path" title={`${r.requestPath || ''}${r.queryString || ''}`}>
            {r.requestPath}
            {r.queryString ? <span className="history-path__query">{r.queryString}</span> : null}
          </span>
        ),
      },
      {
        key: 'statusCode',
        label: t('history.colStatus'),
        align: 'center',
        sortable: true,
        sortValue: (r) => r.statusCode || 0,
        render: (r) => <StatusBadge status={r.statusCode} />,
      },
      {
        key: 'durationMs',
        label: t('history.colDuration'),
        align: 'end',
        sortable: true,
        sortValue: (r) => r.durationMs || 0,
        render: (r) => fmt.duration(r.durationMs),
      },
      {
        key: 'clientIp',
        label: t('history.colClient'),
        mono: true,
        render: (r) => r.clientIp || '—',
      },
      {
        key: 'actions',
        label: t('common.actions'),
        align: 'end',
        isAction: true,
        width: 56,
        render: (r) => (
          <ActionMenu
            items={entityActions(t, {
              onView: () => openDetail(r, 'view'),
              onViewJson: () => openDetail(r, 'json'),
              onDelete: () => setDeleteTarget(r),
            })}
          />
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, fmt]
  );

  const emptyMessage = filtersActive
    ? t('history.emptyNoMatch')
    : t('history.emptyNoTraffic');

  return (
    <div className="view history-view">
      <PageHeader title={t('history.title')} subtitle={t('history.subtitle')} />

      <ActivityChart
        data={timeseries}
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onBucketClick={handleBucketClick}
        onRefresh={loadTimeseries}
        loading={chartLoading}
      />

      <div className="history-kpis">
        <Metric label={t('history.kpiRetained')} value={fmt.number(stats?.totalRequests ?? rows.length)} />
        <Metric
          label={t('history.kpiFailures')}
          value={fmt.number(stats?.failedRequests ?? 0)}
          tone={stats?.failedRequests ? 'danger' : 'neutral'}
        />
        <Metric label={t('history.kpiSuccessRate')} value={successRateText} tone="success" />
        <Metric
          label={t('history.kpiAvgDuration')}
          value={avgDurationMs != null ? fmt.duration(avgDurationMs) : '—'}
        />
      </div>

      <FilterBar>
          <FilterGrid>
            <Field label={<span title={t('history.filterMethodTip')}>{t('history.filterMethod')}</span>}>
              <select
                className="sb-input"
                title={t('history.filterMethodTip')}
                value={filters.method}
                onChange={(e) => updateFilter({ method: e.target.value })}
              >
                <option value="">{t('history.methodAny')}</option>
                {HTTP_METHODS.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </Field>
            <Field label={<span title={t('history.filterStatusTip')}>{t('history.filterStatus')}</span>}>
              <input
                type="text"
                className="sb-input"
                title={t('history.filterStatusTip')}
                inputMode="numeric"
                placeholder={t('history.statusAny')}
                value={filters.status}
                onChange={(e) => updateFilter({ status: e.target.value })}
              />
            </Field>
            <Field label={<span title={t('history.filterPathTip')}>{t('history.filterPath')}</span>}>
              <input
                type="text"
                className="sb-input"
                title={t('history.filterPathTip')}
                value={filters.path}
                onChange={(e) => updateFilter({ path: e.target.value })}
              />
            </Field>
            <Field label={<span title={t('history.filterFromTip')}>{t('history.filterFrom')}</span>}>
              <input
                type="datetime-local"
                className="sb-input"
                title={t('history.filterFromTip')}
                value={filters.from}
                onChange={(e) => updateFilter({ from: e.target.value })}
              />
            </Field>
            <Field label={<span title={t('history.filterToTip')}>{t('history.filterTo')}</span>}>
              <input
                type="datetime-local"
                className="sb-input"
                title={t('history.filterToTip')}
                value={filters.to}
                onChange={(e) => updateFilter({ to: e.target.value })}
              />
            </Field>
            <Field label="">
              <label className="history-toggle" title={t('history.failedOnlyTip')}>
                <input
                  type="checkbox"
                  title={t('history.failedOnlyTip')}
                  checked={filters.failedOnly}
                  onChange={(e) => updateFilter({ failedOnly: e.target.checked })}
                />
                <span>{t('history.failedOnly')}</span>
              </label>
            </Field>
          </FilterGrid>
          {filtersActive && (
            <FilterActions>
              <button type="button" className="sb-btn sb-btn--ghost" onClick={clearFilters}>
                {t('common.clear')}
              </button>
            </FilterActions>
          )}
      </FilterBar>

      <TablePagination
        total={total}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPageNumber(1);
        }}
        onRefresh={refreshAll}
      />

      <DataTable
        columns={columns}
        rows={rows}
        rowKey={(r) => r.requestId ?? r.id}
        loading={loading}
        error={error}
        onRetry={loadRows}
        onRowClick={(r) => openDetail(r, 'view')}
        emptyMessage={emptyMessage}
        emptyHint={filtersActive ? t('common.clear') : t('table.emptyHint')}
      />

      <RequestDetailsModal
        open={Boolean(viewRecord)}
        record={viewRecord}
        onClose={() => setViewRecord(null)}
        onViewJson={() => {
          setJsonRecord(viewRecord);
          setViewRecord(null);
        }}
      />

      <JsonViewerModal
        open={Boolean(jsonRecord)}
        onClose={() => setJsonRecord(null)}
        data={jsonRecord}
        id={jsonRecord?.requestId}
        title={t('history.detailTitle')}
      />

      <ConfirmModal
        open={Boolean(deleteTarget)}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('history.deleteConfirmTitle')}
        message={t('history.deleteConfirmMessage')}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />
    </div>
  );
}

export default HistoryView;
