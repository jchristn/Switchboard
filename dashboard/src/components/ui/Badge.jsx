import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import './Badge.css';

const TONES = ['neutral', 'success', 'warning', 'danger', 'info', 'accent'];

// Pill label. Background is the tone color at low opacity, text is the tone color.
// Always renders text so color is never the only signal.
export default function Badge({ tone = 'neutral', children, className = '', ...props }) {
  const safeTone = TONES.includes(tone) ? tone : 'neutral';
  return (
    <span className={`sb-badge sb-badge--${safeTone} ${className}`.trim()} {...props}>
      {children}
    </span>
  );
}

Badge.propTypes = {
  tone: PropTypes.oneOf(TONES),
  children: PropTypes.node,
  className: PropTypes.string,
};

// Maps a status to a tone. Numeric HTTP status codes color by class (2xx/3xx ok, 4xx warn, 5xx danger);
// free-form status strings map by keyword.
function statusToTone(status) {
  const num = typeof status === 'number' ? status : /^\d{3}$/.test(String(status)) ? Number(status) : NaN;
  if (!Number.isNaN(num)) {
    if (num >= 500) return 'danger';
    if (num >= 400) return 'warning';
    if (num >= 200 && num < 400) return 'success';
    return 'neutral';
  }
  const s = String(status || '').toLowerCase();
  if (['healthy', 'active', 'success', 'ok', 'up', 'enabled', 'online', 'ready', 'valid'].includes(s)) {
    return 'success';
  }
  if (['pending', 'warning', 'degraded', 'checking', 'partial', 'restarting'].includes(s)) {
    return 'warning';
  }
  if (['unhealthy', 'inactive', 'failure', 'failed', 'error', 'down', 'disabled', 'offline', 'invalid'].includes(s)) {
    return 'danger';
  }
  if (['info', 'new', 'draft'].includes(s)) {
    return 'info';
  }
  return 'neutral';
}

export function StatusBadge({ status, label, ...props }) {
  return (
    <Badge tone={statusToTone(status)} {...props}>
      {label != null ? label : status}
    </Badge>
  );
}

StatusBadge.propTypes = {
  status: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  label: PropTypes.node,
};

const METHOD_TONE = {
  GET: 'info',
  HEAD: 'info',
  OPTIONS: 'neutral',
  POST: 'success',
  PUT: 'warning',
  PATCH: 'warning',
  DELETE: 'danger',
};

export function MethodBadge({ method, ...props }) {
  const m = String(method || '').toUpperCase();
  return (
    <Badge tone={METHOD_TONE[m] || 'neutral'} className="sb-badge--method" {...props}>
      {m || '—'}
    </Badge>
  );
}

MethodBadge.propTypes = {
  method: PropTypes.string,
};

export function HealthBadge({ healthy, healthyLabel, unhealthyLabel, ...props }) {
  const { t } = useTranslation();
  const tone = healthy ? 'success' : 'danger';
  const text = healthy
    ? healthyLabel || t('status.healthy')
    : unhealthyLabel || t('status.unhealthy');
  return (
    <Badge tone={tone} {...props}>
      <span className="sb-badge__dot" aria-hidden="true" />
      {text}
    </Badge>
  );
}

HealthBadge.propTypes = {
  healthy: PropTypes.bool,
  healthyLabel: PropTypes.string,
  unhealthyLabel: PropTypes.string,
};
