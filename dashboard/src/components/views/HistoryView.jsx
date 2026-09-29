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
  rangeWindow,
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
import { usePersistentPageSize } from '../../hooks/usePersistentPageSize';
import { parseStatusFilter, NON_2XX_FILTER } from '../../utils/statusFilter';
import { summarizeBuckets } from '../../utils/timeseries';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'];

// The history API cannot filter by method, status, or path, so while one of those filters is active
// the dashboard fetches this many of the most recent requests, filters them, and pages the result.
const CLIENT_FILTER_WINDOW = 1000;

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
  const [chartLoading, setChartLoading] = useState(true);
  // The "now" the loaded series was fetched for; the chart draws exactly that window.
  const [seriesNowMs, setSeriesNowMs] = useState(null);

  // KPI stats.
  const [stats, setStats] = useState(null);

  // Table state.
  const [rows, setRows] = useState([]);
  // Filtered total when filtering in the browser (null when the server-side count applies), and
  // whether the fetched window was full (older requests exist that the filter did not see).
  const [clientTotal, setClientTotal] = useState(null);
  const [clientWindowFull, setClientWindowFull] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = usePersistentPageSize('history', 50);

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

  // "Failures" KPI card: every status except 2xx, with all other filters cleared.
  const showNon2xx = () => {
    setFilters({ ...EMPTY_FILTERS, status: NON_2XX_FILTER });
    setPageNumber(1);
  };

  const statusFilter = useMemo(() => parseStatusFilter(filters.status), [filters.status]);

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
    // Same aligned window as Overview (rangeWindow), so both charts and the KPI tiles agree.
    const win = rangeWindow(rangeId);
    setChartLoading(true);
    try {
      const data = await apiClient.getHistoryTimeseries({
        start: win.start,
        end: win.end,
        intervalMinutes: win.intervalMinutes,
      });
      // The endpoint returns { startUtc, endUtc, intervalMinutes, buckets: [...] }; the chart wants the buckets.
      setTimeseries(Array.isArray(data?.buckets) ? data.buckets : Array.isArray(data) ? data : []);
      setSeriesNowMs(win.endMs);
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
    const clientFiltering = Boolean(filters.method || filters.path || (filters.status && statusFilter.valid));
    const fetchSkip = clientFiltering ? 0 : skip;
    const fetchTake = clientFiltering ? CLIENT_FILTER_WINDOW : take;
    try {
      let data = useFailedEndpoint
        ? await apiClient.getFailedHistory({ skip: fetchSkip, take: fetchTake })
        : await apiClient.getHistory({ skip: fetchSkip, take: fetchTake, start, end });
      data = Array.isArray(data) ? data : [];
      const windowFull = clientFiltering && data.length >= CLIENT_FILTER_WINDOW;

      // Client-side fallback: the backend does not filter by method/status/path (nor
      // by failed when a date window forced us onto getHistory), so apply those here.
      if (filters.failedOnly && !useFailedEndpoint) {
        data = data.filter((r) => r.success === false || (r.statusCode >= 400));
      }
      if (filters.method) {
        const m = filters.method.toUpperCase();
        data = data.filter((r) => String(r.httpMethod || '').toUpperCase() === m);
      }
      if (filters.status && statusFilter.valid) {
        data = data.filter((r) => statusFilter.matches(r.statusCode));
      }
      if (filters.path) {
        const needle = filters.path.trim().toLowerCase();
        data = data.filter((r) => String(r.requestPath || '').toLowerCase().includes(needle));
      }

      if (clientFiltering) {
        setClientTotal(data.length);
        setClientWindowFull(windowFull);
        setRows(data.slice(skip, skip + take));
      } else {
        setClientTotal(null);
        setClientWindowFull(false);
        setRows(data);
      }
    } catch (err) {
      setError(err?.message || t('history.loadError'));
      setClientTotal(null);
      setClientWindowFull(false);
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [apiClient, pageNumber, pageSize, filters, statusFilter, t]);

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
  // The tiles describe the same window as the activity chart above them (the selected range), so the
  // chart and the numbers always agree. All-time totals from /history/stats are shown as notes.
  const rangeSummary = useMemo(() => summarizeBuckets(timeseries), [timeseries]);
  const rangeReady = !chartLoading || timeseries.length > 0;

  const total = useMemo(() => {
    if (clientTotal != null) return clientTotal;
    if (stats) {
      return filters.failedOnly ? stats.failedRequests ?? rows.length : stats.totalRequests ?? rows.length;
    }
    return rows.length;
  }, [clientTotal, stats, filters.failedOnly, rows.length]);

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
        nowMs={seriesNowMs || undefined}
      />

      <div className="history-kpis">
        <Metric
          label={<span title={t('history.kpiAllTip')}>{t('history.kpiRetained')}</span>}
          value={rangeReady ? fmt.number(rangeSummary.total) : '—'}
          note={stats ? t('kpi.allTimeCount', { count: fmt.number(stats.totalRequests ?? 0) }) : null}
          onClick={clearFilters}
        />
        <Metric
          label={<span title={t('history.kpiFailuresTip')}>{t('history.kpiFailures')}</span>}
          value={rangeReady ? fmt.number(rangeSummary.failure) : '—'}
          note={stats ? t('kpi.allTimeCount', { count: fmt.number(stats.failedRequests ?? 0) }) : null}
          tone={rangeSummary.failure > 0 ? 'danger' : 'neutral'}
          onClick={showNon2xx}
        />
        <Metric
          label={t('history.kpiSuccessRate')}
          value={rangeReady && rangeSummary.successRate != null ? fmt.percent(rangeSummary.successRate, 1) : '—'}
          note={stats?.successRate != null ? t('kpi.allTimeRate', { rate: fmt.percent(stats.successRate, 1) }) : null}
          tone="success"
        />
        <Metric
          label={t('history.kpiAvgDuration')}
          value={rangeReady && rangeSummary.avgDurationMs != null ? fmt.duration(rangeSummary.avgDurationMs) : '—'}
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
            <Field
              className="history-status-field"
              label={<span title={t('history.filterStatusTip')}>{t('history.filterStatus')}</span>}
              hint={
                statusFilter.valid ? null : (
                  <span className="history-status-error" role="alert">
                    {t('history.statusInvalid', { term: statusFilter.invalidTerm })}
                  </span>
                )
              }
            >
              <input
                type="text"
                className="sb-input"
                title={t('history.filterStatusTip')}
                aria-invalid={statusFilter.valid ? undefined : true}
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

      {clientWindowFull && (
        <p className="history-window-note" role="note">
          {t('history.clientFilterWindow', { count: fmt.number(CLIENT_FILTER_WINDOW) })}
        </p>
      )}

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
