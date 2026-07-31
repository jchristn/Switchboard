import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import { HealthBadge } from './Badge';
import './HealthHistogram.css';

// Format a duration in milliseconds as a compact "Nh Nm" / "Nm" string.
export function formatDuration(ms) {
  const hours = Math.floor(ms / 3600000);
  const minutes = Math.floor((ms % 3600000) / 60000);
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

// Bucket a rolling health history into bars. Each bar is green (all success), red (all fail),
// or amber (mixed) — the same semantics used across the sibling projects' dashboards.
function bucketHistory(history, width) {
  const now = new Date();
  const sorted = [...history].sort((a, b) => new Date(a.timestampUtc) - new Date(b.timestampUtc));
  const oldest = new Date(sorted[0].timestampUtc);
  const spanHours = (now - oldest) / (1000 * 60 * 60);

  let buckets = [];
  if (spanHours < 1) {
    buckets = sorted.map((r) => ({ success: r.success ? 1 : 0, fail: r.success ? 0 : 1, time: r.timestampUtc }));
  } else {
    const bucketMs = spanHours <= 6 ? 60000 : 300000;
    const bucketMap = new Map();
    for (const r of sorted) {
      const key = Math.floor(new Date(r.timestampUtc).getTime() / bucketMs);
      if (!bucketMap.has(key)) bucketMap.set(key, { success: 0, fail: 0 });
      const b = bucketMap.get(key);
      if (r.success) b.success += 1;
      else b.fail += 1;
    }
    for (const [key, val] of bucketMap) {
      buckets.push({ ...val, time: new Date(key * bucketMs).toISOString() });
    }
  }

  const maxBars = Math.floor(width / 6);
  if (buckets.length > maxBars) buckets = buckets.slice(-maxBars);
  return buckets;
}

// A compact bar histogram of recent health check results.
export function HealthHistogram({ history, width = 120, height = 24 }) {
  const { t } = useTranslation();
  if (!history || history.length === 0) {
    return <span className="sb-hh-empty">{t('health.noData')}</span>;
  }

  const buckets = bucketHistory(history, width);
  const barWidth = Math.max(4, Math.floor(width / buckets.length) - 2);

  return (
    <div className="sb-hh" style={{ height: `${height}px`, maxWidth: `${width}px` }}>
      {buckets.map((b, i) => {
        let tone = 'ok';
        if (b.fail > 0 && b.success === 0) tone = 'fail';
        else if (b.fail > 0 && b.success > 0) tone = 'mixed';
        const title = `${new Date(b.time).toLocaleTimeString()} — ${b.success} ok, ${b.fail} fail`;
        return (
          <div
            key={i}
            className={`sb-hh-bar sb-hh-bar--${tone}`}
            title={title}
            style={{ width: `${barWidth}px`, height: `${height}px` }}
          />
        );
      })}
    </div>
  );
}

HealthHistogram.propTypes = {
  history: PropTypes.arrayOf(
    PropTypes.shape({ timestampUtc: PropTypes.string, success: PropTypes.bool })
  ),
  width: PropTypes.number,
  height: PropTypes.number,
};

function fmtTime(value) {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toLocaleString();
}

// Detail modal: stat-card row, optional last-error box, wide histogram, and a timestamp grid.
export function HealthDetailModal({ open, onClose, health }) {
  const { t } = useTranslation();
  if (!open || !health) return null;

  const uptimePct = health.uptimePercentage != null ? `${health.uptimePercentage.toFixed(2)}%` : t('health.notAvailable');
  const history = health.history || [];
  let spanStr = t('health.noData');
  if (history.length > 0) {
    const oldest = [...history].sort((a, b) => new Date(a.timestampUtc) - new Date(b.timestampUtc))[0];
    const spanMs = new Date() - new Date(oldest.timestampUtc);
    if (spanMs > 0) spanStr = formatDuration(spanMs);
  }

  const timestamps = [
    { label: t('health.firstCheck'), value: fmtTime(health.firstCheckUtc) },
    { label: t('health.lastCheck'), value: fmtTime(health.lastCheckUtc) },
    { label: t('health.lastHealthy'), value: fmtTime(health.lastHealthyUtc) },
    { label: t('health.lastUnhealthy'), value: fmtTime(health.lastUnhealthyUtc) },
  ];

  return (
    <Modal open={open} onClose={onClose} size="large" title={t('health.titleFor', { name: health.name || health.identifier })}>
      <div className="sb-health-modal">
        <div className="sb-health-stats">
          <div className="sb-health-stat">
            <div className="sb-health-stat__label">{t('health.status')}</div>
            <div className="sb-health-stat__value">
              <HealthBadge healthy={!!health.isHealthy} />
            </div>
          </div>
          <div className="sb-health-stat">
            <div className="sb-health-stat__label">{t('health.uptime')}</div>
            <div className="sb-health-stat__value">{uptimePct}</div>
          </div>
          <div className="sb-health-stat">
            <div className="sb-health-stat__label">{t('health.historySpan')}</div>
            <div className="sb-health-stat__value">{spanStr}</div>
          </div>
          <div className="sb-health-stat">
            <div className="sb-health-stat__label">{t('health.consecutiveOk')}</div>
            <div className="sb-health-stat__value sb-health-stat__value--ok">{health.consecutiveSuccesses ?? 0}</div>
          </div>
          <div className="sb-health-stat">
            <div className="sb-health-stat__label">{t('health.consecutiveFail')}</div>
            <div className="sb-health-stat__value sb-health-stat__value--fail">{health.consecutiveFailures ?? 0}</div>
          </div>
        </div>

        {health.lastError && (
          <div className="sb-health-error">
            <div className="sb-health-error__label">{t('health.lastError')}</div>
            <div className="sb-health-error__message">{health.lastError}</div>
          </div>
        )}

        <div className="sb-health-section">
          <div className="sb-health-section__label">{t('health.healthHistory')}</div>
          <div className="sb-health-histogram-box">
            <HealthHistogram history={history} width={760} height={36} />
          </div>
        </div>

        <div className="sb-health-timestamps">
          {timestamps.map((ts) => (
            <div key={ts.label} className="sb-health-timestamp">
              <span className="sb-health-timestamp__label">{ts.label}</span>
              <span className="sb-health-timestamp__value">{ts.value || t('health.notAvailable')}</span>
            </div>
          ))}
        </div>
      </div>
    </Modal>
  );
}

HealthDetailModal.propTypes = {
  open: PropTypes.bool,
  onClose: PropTypes.func,
  health: PropTypes.object,
};
