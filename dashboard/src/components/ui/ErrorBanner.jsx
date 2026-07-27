import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { Warning, Refresh } from './Icons';
import CopyableId from './CopyableId';
import './ErrorBanner.css';

// Inline error surface with an optional request id and retry action.
export default function ErrorBanner({ message, requestId, onRetry, className = '' }) {
  const { t } = useTranslation();
  return (
    <div className={`sb-error-banner ${className}`.trim()} role="alert">
      <span className="sb-error-banner__icon" aria-hidden="true">
        <Warning size={18} />
      </span>
      <div className="sb-error-banner__body">
        <div className="sb-error-banner__message">{message || t('table.error')}</div>
        {requestId && (
          <div className="sb-error-banner__meta">
            <CopyableId value={requestId} />
          </div>
        )}
      </div>
      {onRetry && (
        <button type="button" className="sb-error-banner__retry" onClick={onRetry}>
          <Refresh size={15} />
          <span>{t('table.retry')}</span>
        </button>
      )}
    </div>
  );
}

ErrorBanner.propTypes = {
  message: PropTypes.node,
  requestId: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  onRetry: PropTypes.func,
  className: PropTypes.string,
};
