import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import './EmptyState.css';

// Centered placeholder for empty collections. `message` is the headline, `hint` a
// muted secondary line, and `action` an optional call-to-action node.
export default function EmptyState({ icon, message, hint, action, className = '' }) {
  const { t } = useTranslation();
  return (
    <div className={`sb-empty ${className}`.trim()}>
      {icon && <div className="sb-empty__icon" aria-hidden="true">{icon}</div>}
      <div className="sb-empty__message">{message || t('table.empty')}</div>
      {hint && <div className="sb-empty__hint">{hint}</div>}
      {action && <div className="sb-empty__action">{action}</div>}
    </div>
  );
}

EmptyState.propTypes = {
  icon: PropTypes.node,
  message: PropTypes.node,
  hint: PropTypes.node,
  action: PropTypes.node,
  className: PropTypes.string,
};
