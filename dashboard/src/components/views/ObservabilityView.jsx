import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import {
  PageHeader,
  Metric,
  Badge,
  CopyableId,
  ErrorBanner,
  Icons,
} from '../ui';
import './ObservabilityView.css';

// External UI URLs are deployment-specific; the compose stack publishes Grafana on 3001 and
// Prometheus on 9090. Allow a build-time override without requiring a backend endpoint.
const GRAFANA_URL = import.meta.env.VITE_GRAFANA_URL || 'http://localhost:3001';
const PROMETHEUS_URL = import.meta.env.VITE_PROMETHEUS_URL || 'http://localhost:9090';

function signalTone(enabled) {
  return enabled ? 'success' : 'neutral';
}

function ObservabilityView() {
  const { apiClient } = useAuth();
  const { t } = useTranslation();

  const [telemetry, setTelemetry] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

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
      if (mounted.current) setTelemetry(data?.telemetry ?? null);
    } catch (err) {
      if (mounted.current) setError(err.message || t('observability.loadError'));
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return (
      <div className="obs">
        <PageHeader title={t('observability.title')} subtitle={t('observability.subtitle')} />
        <div className="obs-loading" role="status" aria-live="polite">
          <span className="obs-spinner" aria-hidden="true" />
          <span>{t('common.loading')}</span>
        </div>
      </div>
    );
  }

  const enabled = Boolean(telemetry?.enable);
  const metrics = telemetry?.metrics ?? {};
  const traces = telemetry?.traces ?? {};
  const logs = telemetry?.logs ?? {};
  const otlp = telemetry?.otlp ?? {};

  const links = (
    <div className="obs-links">
      <a
        className="sb-btn sb-btn--ghost"
        href={GRAFANA_URL}
        target="_blank"
        rel="noreferrer noopener"
        title={t('observability.openGrafanaTip')}
      >
        <Icons.ExternalLink aria-hidden="true" />
        <span>{t('observability.openGrafana')}</span>
      </a>
      <a
        className="sb-btn sb-btn--ghost"
        href={PROMETHEUS_URL}
        target="_blank"
        rel="noreferrer noopener"
        title={t('observability.openPrometheusTip')}
      >
        <Icons.ExternalLink aria-hidden="true" />
        <span>{t('observability.openPrometheus')}</span>
      </a>
    </div>
  );

  return (
    <div className="obs">
      <PageHeader title={t('observability.title')} subtitle={t('observability.subtitle')} actions={links} />

      {error && <ErrorBanner message={error} onRetry={load} />}

      <div className="obs-kpis">
        <Metric
          label={t('observability.status')}
          value={
            <Badge tone={enabled ? 'success' : 'neutral'}>
              {enabled ? t('observability.enabled') : t('observability.disabled')}
            </Badge>
          }
          tone={enabled ? 'success' : 'neutral'}
        />
        <Metric
          label={t('observability.signals')}
          value={
            <span className="obs-signals" title={t('observability.signalsTip')}>
              <Badge tone={signalTone(enabled && metrics.enable)}>{t('observability.metrics')}</Badge>
              <Badge tone={signalTone(enabled && traces.enable)}>{t('observability.traces')}</Badge>
              <Badge tone={signalTone(enabled && logs.enable)}>{t('observability.logs')}</Badge>
            </span>
          }
        />
        <Metric
          label={t('observability.samplingRatio')}
          value={traces.samplingRatio != null ? Number(traces.samplingRatio).toFixed(2) : '—'}
        />
        <Metric
          label={t('observability.exportInterval')}
          value={metrics.exportIntervalMs != null ? `${metrics.exportIntervalMs} ms` : '—'}
        />
      </div>

      <section className="obs-panel" aria-label={t('observability.exportConfig')}>
        <h2 className="obs-panel__title">{t('observability.exportConfig')}</h2>
        <dl className="obs-detail">
          <div className="obs-detail__row">
            <dt title={t('observability.serviceNameTip')}>{t('observability.serviceName')}</dt>
            <dd><CopyableId value={telemetry?.serviceName || 'switchboard'} /></dd>
          </div>
          <div className="obs-detail__row">
            <dt title={t('observability.otlpEndpointTip')}>{t('observability.otlpEndpoint')}</dt>
            <dd><CopyableId value={otlp.endpoint || '—'} /></dd>
          </div>
          <div className="obs-detail__row">
            <dt title={t('observability.otlpProtocolTip')}>{t('observability.otlpProtocol')}</dt>
            <dd><Badge tone="info">{otlp.protocol || 'grpc'}</Badge></dd>
          </div>
          <div className="obs-detail__row">
            <dt title={t('observability.propagateTip')}>{t('observability.propagate')}</dt>
            <dd>
              <Badge tone={traces.propagateToOrigin ? 'success' : 'neutral'}>
                {traces.propagateToOrigin ? t('observability.on') : t('observability.off')}
              </Badge>
            </dd>
          </div>
        </dl>
        <p className="obs-note">{t('observability.note')}</p>
      </section>

      {!enabled && (
        <div className="obs-hint" role="note">
          {t('observability.disabledHint')}
        </div>
      )}
    </div>
  );
}

export default ObservabilityView;
