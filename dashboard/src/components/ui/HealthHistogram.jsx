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

const BAR_GAP = 2;
const MIN_BAR = 3;

// A compact bar histogram of recent health checks. One bar per check attempt — green for success,
// red for failure — rendered as a FIFO window: oldest on the left, newest on the right. Only the most
// recent attempts that fit in the given width are shown; older ones fall off the left edge.
export function HealthHistogram({ history, width = 120, height = 24 }) {
  const { t } = useTranslation();
  if (!history || history.length === 0) {
    return <span className="sb-hh-empty">{t('health.noData')}</span>;
  }

  const sorted = [...history].sort((a, b) => new Date(a.timestampUtc) - new Date(b.timestampUtc));
  const maxBars = Math.max(1, Math.floor((width + BAR_GAP) / (MIN_BAR + BAR_GAP)));
  const shown = sorted.slice(-maxBars);
  const barWidth = Math.max(
    MIN_BAR,
    Math.floor((width - BAR_GAP * (shown.length - 1)) / shown.length)
  );

  return (
    <div className="sb-hh" style={{ height: `${height}px`, maxWidth: `${width}px` }}>
      {shown.map((r, i) => {
        const tone = r.success ? 'ok' : 'fail';
        const title = `${new Date(r.timestampUtc).toLocaleTimeString()} — ${r.success ? 'ok' : 'fail'}`;
        return (
          <div
            key={`${r.timestampUtc}-${i}`}
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
