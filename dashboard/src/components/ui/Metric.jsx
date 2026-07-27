import PropTypes from 'prop-types';
import './Metric.css';

const TONES = ['neutral', 'success', 'warning', 'danger', 'info'];

// KPI stat tile. When `onClick` is supplied it renders as a button with hover
// affordance; otherwise it is a static card.
export default function Metric({
  label,
  value,
  note,
  tone = 'neutral',
  icon,
  onClick,
  className = '',
}) {
  const safeTone = TONES.includes(tone) ? tone : 'neutral';
  const interactive = typeof onClick === 'function';
  const Tag = interactive ? 'button' : 'div';

  return (
    <Tag
      type={interactive ? 'button' : undefined}
      className={`sb-metric sb-metric--${safeTone} ${interactive ? 'sb-metric--interactive' : ''} ${className}`.trim()}
      onClick={onClick}
    >
      <div className="sb-metric__head">
        <span className="sb-metric__label">{label}</span>
        {icon && <span className="sb-metric__icon" aria-hidden="true">{icon}</span>}
      </div>
      <div className="sb-metric__value">{value}</div>
      {note != null && <div className="sb-metric__note">{note}</div>}
    </Tag>
  );
}

Metric.propTypes = {
  label: PropTypes.node,
  value: PropTypes.node,
  note: PropTypes.node,
  tone: PropTypes.oneOf(TONES),
  icon: PropTypes.node,
  onClick: PropTypes.func,
  className: PropTypes.string,
};
