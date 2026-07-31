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

// A compact bar histogram of recent health checks. One bar per check attempt — green for success,
// red for failure — rendered as a fixed-size FIFO window of `slots` cells: oldest on the left, newest
// on the right. The window always renders exactly `slots` cells so every origin shows the same number
// of bars; when there are fewer samples than slots, the oldest cells render empty. Bars flex to fill
// the container (or the optional `width`).
export function HealthHistogram({ history, slots = 10, width, height = 24 }) {
  const { t } = useTranslation();

  const sorted = history ? [...history].sort((a, b) => new Date(a.timestampUtc) - new Date(b.timestampUtc)) : [];
  const recent = sorted.slice(-slots);
  const emptyCount = Math.max(0, slots - recent.length);

  const cells = [];
  for (let i = 0; i < emptyCount; i += 1) cells.push(null);
  for (let i = 0; i < recent.length; i += 1) cells.push(recent[i]);

  const style = { height: `${height}px`, gap: `${BAR_GAP}px` };
  if (width) style.width = `${width}px`;

  return (
    <div className="sb-hh" style={style} role="img" aria-label={t('health.healthHistory')}>
      {cells.map((r, i) => {
        const tone = r == null ? 'empty' : r.success ? 'ok' : 'fail';
        const title = r == null
          ? t('health.noData')
          : `${new Date(r.timestampUtc).toLocaleTimeString()} — ${r.success ? 'ok' : 'fail'}`;
        return <span key={i} className={`sb-hh-bar sb-hh-bar--${tone}`} title={title} />;
      })}
    </div>
  );
}

HealthHistogram.propTypes = {
  history: PropTypes.arrayOf(
    PropTypes.shape({ timestampUtc: PropTypes.string, success: PropTypes.bool })
  ),
  slots: PropTypes.number,
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
            <HealthHistogram history={history} slots={40} height={36} />
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
