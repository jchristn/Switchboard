import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useFormatters } from '../../hooks/useFormatters';
import {
  PageHeader,
  Metric,
  ActivityChart,
  TIME_RANGES,
  EmptyState,
  Icons,
} from '../ui';
import './OverviewView.css';

const POLL_MS = 30000;

// Pull the value out of a Promise.allSettled result, or null when it rejected.
function settled(result) {
  return result && result.status === 'fulfilled' ? result.value : null;
}

// Success-rate → tone. successRate is a 0-100 percentage.
function rateTone(rate) {
  if (rate == null) return 'neutral';
  if (rate >= 99) return 'success';
  if (rate >= 90) return 'warning';
  return 'danger';
}

function OverviewView() {
  const { apiClient } = useAuth();
  const { t } = useTranslation();
  const fmt = useFormatters();
  const navigate = useNavigate();

  const [rangeId, setRangeId] = useState('hour');
  const [loading, setLoading] = useState(true);
  const [chartLoading, setChartLoading] = useState(false);
  const [lastLoaded, setLastLoaded] = useState(null);
  const [timeseries, setTimeseries] = useState([]);
  const [data, setData] = useState({
    origins: null,
    endpoints: null,
    stats: null,
    credentials: null,
    blockedHeaders: null,
  });

  // Keep a mounted flag so async completions after unmount don't set state.
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const loadData = useCallback(
    async (range) => {
      setChartLoading(true);
      const rangeCfg = TIME_RANGES[range] || TIME_RANGES.hour;
      const now = Date.now();
      const timeseriesArgs = {
        start: new Date(now - rangeCfg.windowMs).toISOString(),
        end: new Date(now).toISOString(),
        intervalMinutes: Math.max(1, Math.round(rangeCfg.bucketMs / 60000)),
      };

      const results = await Promise.allSettled([
        apiClient.getOrigins(),
        apiClient.getEndpoints(),
        apiClient.getHistoryStats(),
        apiClient.getCredentials(),
        apiClient.getBlockedHeaders(),
        apiClient.getHistoryTimeseries(timeseriesArgs),
      ]);

      if (!mountedRef.current) return;

      const [origins, endpoints, stats, credentials, blockedHeaders, series] = results;

      setData((prev) => ({
        origins: settled(origins) ?? prev.origins,
        endpoints: settled(endpoints) ?? prev.endpoints,
        stats: settled(stats) ?? prev.stats,
        credentials: settled(credentials) ?? prev.credentials,
        blockedHeaders: settled(blockedHeaders) ?? prev.blockedHeaders,
      }));

      // Timeseries endpoint is new; if it 404s (rejected) show the chart empty
      // rather than surfacing an error on the page.
      // The endpoint returns { startUtc, endUtc, intervalMinutes, buckets: [...] }; the chart wants the buckets.
      const seriesValue = settled(series);
      setTimeseries(
        Array.isArray(seriesValue?.buckets)
          ? seriesValue.buckets
          : Array.isArray(seriesValue)
            ? seriesValue
            : []
      );

      setLastLoaded(new Date());
      setLoading(false);
      setChartLoading(false);
    },
    [apiClient]
  );

  // Load on mount and whenever the range changes; poll every 30s. Changing the
  // range resets the interval and triggers an immediate reload for that window.
  useEffect(() => {
    loadData(rangeId);
    const interval = setInterval(() => loadData(rangeId), POLL_MS);
    return () => clearInterval(interval);
  }, [rangeId, loadData]);

  const {
    origins,
    endpoints,
    stats,
    credentials,
    blockedHeaders,
  } = data;

  const originCount = origins ? origins.length : null;
  const healthyOrigins = origins ? origins.filter((o) => o.healthy === true).length : 0;
  const unhealthyOrigins = useMemo(
    () => (origins ? origins.filter((o) => o.healthy === false) : []),
    [origins]
  );
  const endpointCount = endpoints ? endpoints.length : null;
  const totalRequests = stats ? stats.totalRequests ?? 0 : null;
  const failedRequests = stats ? stats.failedRequests ?? 0 : null;
  const successRate = stats ? stats.successRate ?? null : null;
  const credentialCount = credentials ? credentials.length : null;
  const blockedHeaderCount = blockedHeaders ? blockedHeaders.length : null;

  const num = (v) => (v == null ? '—' : fmt.number(v));

  // Attention alerts: known-unhealthy origins and non-zero failures.
  const alerts = useMemo(() => {
    const list = [];
    unhealthyOrigins.forEach((o) => {
      list.push({
        id: `origin-${o.identifier}`,
        message: t('overview.attnUnhealthyOrigin', { name: o.name || o.identifier }),
        to: '/dashboard/origins',
      });
    });
    if (failedRequests > 0) {
      list.push({
        id: 'failures',
        message: t('overview.attnFailures', { count: failedRequests }),
        to: '/dashboard/history?failed=1',
      });
    }
    return list;
  }, [unhealthyOrigins, failedRequests, t]);

  const quickActions = [
    {
      id: 'add-origin',
      icon: <Icons.Server size={20} />,
      title: t('overview.qaAddOrigin'),
      hint: t('overview.qaAddOriginHint'),
      to: '/dashboard/origins',
    },
    {
      id: 'add-endpoint',
      icon: <Icons.Route size={20} />,
      title: t('overview.qaAddEndpoint'),
      hint: t('overview.qaAddEndpointHint'),
      to: '/dashboard/endpoints',
    },
    {
      id: 'api-explorer',
      icon: <Icons.Terminal size={20} />,
      title: t('overview.qaApiExplorer'),
      hint: t('overview.qaApiExplorerHint'),
      to: '/dashboard/api-explorer',
    },
    {
      id: 'view-failures',
      icon: <Icons.Warning size={20} />,
      title: t('overview.qaViewFailures'),
      hint: t('overview.qaViewFailuresHint'),
      to: '/dashboard/history?failed=1',
    },
  ];

  if (loading) {
    return (
      <div className="ov">
        <PageHeader title={t('overview.title')} subtitle={t('overview.subtitle')} />
        <div className="ov-loading" role="status" aria-live="polite">
          <span className="ov-spinner" aria-hidden="true" />
          <span>{t('common.loading')}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="ov">
      <PageHeader title={t('overview.title')} subtitle={t('overview.subtitle')} />

      {/* KPI tiles */}
      <div className="ov-kpis">
        <Metric
          label={t('overview.kpiOrigins')}
          value={num(originCount)}
          note={
            originCount != null
              ? t('overview.kpiOriginsNote', { healthy: healthyOrigins, total: originCount })
              : null
          }
          tone={unhealthyOrigins.length > 0 ? 'danger' : 'neutral'}
          icon={<Icons.Server size={18} />}
          onClick={() => navigate('/dashboard/origins')}
        />
        <Metric
          label={t('overview.kpiEndpoints')}
          value={num(endpointCount)}
          icon={<Icons.Route size={18} />}
          onClick={() => navigate('/dashboard/endpoints')}
        />
        <Metric
          label={t('overview.kpiRequests')}
          value={num(totalRequests)}
          icon={<Icons.History size={18} />}
          onClick={() => navigate('/dashboard/history')}
        />
        <Metric
          label={t('overview.kpiFailures')}
          value={num(failedRequests)}
          tone={failedRequests > 0 ? 'danger' : 'neutral'}
          icon={<Icons.Warning size={18} />}
          onClick={() => navigate('/dashboard/history?failed=1')}
        />
        <Metric
          label={t('overview.kpiSuccessRate')}
          value={successRate != null ? fmt.percent(successRate) : '—'}
          tone={rateTone(successRate)}
          icon={<Icons.Gauge size={18} />}
        />
        <Metric
          label={t('overview.kpiCredentials')}
          value={num(credentialCount)}
          icon={<Icons.Key size={18} />}
          onClick={() => navigate('/dashboard/credentials')}
        />
        <Metric
          label={t('overview.kpiBlockedHeaders')}
          value={num(blockedHeaderCount)}
          icon={<Icons.Shield size={18} />}
          onClick={() => navigate('/dashboard/blocked-headers')}
        />
      </div>

      <div className="ov-columns">
        {/* Quick actions */}
        <section className="ov-panel" aria-label={t('overview.quickActions')}>
          <h2 className="ov-panel__title">{t('overview.quickActions')}</h2>
          <div className="ov-actions">
            {quickActions.map((qa) => (
              <button
                key={qa.id}
                type="button"
                className="ov-action"
                onClick={() => navigate(qa.to)}
              >
                <span className="ov-action__icon" aria-hidden="true">{qa.icon}</span>
                <span className="ov-action__body">
                  <span className="ov-action__title">{qa.title}</span>
                  <span className="ov-action__hint">{qa.hint}</span>
                </span>
                <span className="ov-action__arrow" aria-hidden="true">
                  <Icons.ArrowRight size={16} />
                </span>
              </button>
            ))}
          </div>
        </section>

        {/* Attention */}
        <section className="ov-panel" aria-label={t('overview.attention')}>
          <h2 className="ov-panel__title">{t('overview.attention')}</h2>
          {alerts.length === 0 ? (
            <EmptyState
              icon={<Icons.Check size={28} />}
              message={t('overview.attentionNone')}
            />
          ) : (
            <ul className="ov-alerts">
              {alerts.map((alert) => (
                <li key={alert.id}>
                  <button
                    type="button"
                    className="ov-alert"
                    onClick={() => navigate(alert.to)}
                  >
                    <span className="ov-alert__icon" aria-hidden="true">
                      <Icons.Warning size={18} />
                    </span>
                    <span className="ov-alert__message">{alert.message}</span>
                    <span className="ov-alert__arrow" aria-hidden="true">
                      <Icons.ArrowRight size={16} />
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {/* Request activity chart */}
      <section className="ov-panel" aria-label={t('chart.title')}>
        <ActivityChart
          data={timeseries}
          rangeId={rangeId}
          onRangeChange={setRangeId}
          onBucketClick={() => navigate('/dashboard/history')}
          onRefresh={() => loadData(rangeId)}
          loading={chartLoading}
          title={t('chart.title')}
        />
        {lastLoaded && (
          <div className="ov-updated">
            {t('overview.lastUpdated', { time: fmt.relative(lastLoaded) })}
          </div>
        )}
      </section>
    </div>
  );
}

export default OverviewView;
