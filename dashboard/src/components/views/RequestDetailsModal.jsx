import { useMemo } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useFormatters } from '../../hooks/useFormatters';
import {
  Modal,
  Collapsible,
  CopyButton,
  CopyableId,
  MethodBadge,
  StatusBadge,
  Badge,
} from '../ui';
import './RequestDetailsModal.css';

// Pretty-prints a value that may be a JSON string, a plain string, or an object.
// Returns { text, isJson } so callers can style JSON differently if desired.
function prettify(value) {
  if (value == null || value === '') return { text: '', isJson: false };
  if (typeof value === 'object') {
    try {
      return { text: JSON.stringify(value, null, 2), isJson: true };
    } catch {
      return { text: String(value), isJson: false };
    }
  }
  const str = String(value);
  const trimmed = str.trim();
  if (
    (trimmed.startsWith('{') && trimmed.endsWith('}')) ||
    (trimmed.startsWith('[') && trimmed.endsWith(']'))
  ) {
    try {
      return { text: JSON.stringify(JSON.parse(str), null, 2), isJson: true };
    } catch {
      /* not valid JSON, fall through */
    }
  }
  return { text: str, isJson: false };
}

// Heuristic: a string with control characters (other than tab/newline/carriage
// return) is very likely binary content that should not be rendered inline.
function looksBinary(value) {
  if (typeof value !== 'string' || value.length === 0) return false;
  const sample = value.slice(0, 2048);
  for (let i = 0; i < sample.length; i += 1) {
    const c = sample.charCodeAt(i);
    if (c === 9 || c === 10 || c === 13) continue;
    if (c < 32) return true;
  }
  return false;
}

// A single stat tile for the hero row.
function StatTile({ label, value, tone = 'neutral' }) {
  return (
    <div className={`rd-stat rd-stat--${tone}`}>
      <span className="rd-stat__label">{label}</span>
      <span className="rd-stat__value">{value}</span>
    </div>
  );
}

StatTile.propTypes = {
  label: PropTypes.node,
  value: PropTypes.node,
  tone: PropTypes.string,
};

// One header/body block with a copy affordance and optional truncated/binary pill.
function ContentBlock({ title, value, declaredSize, t }) {
  const binary = looksBinary(typeof value === 'string' ? value : '');
  const { text, isJson } = binary ? { text: '', isJson: false } : prettify(value);
  const truncated =
    typeof value === 'string' &&
    typeof declaredSize === 'number' &&
    declaredSize > 0 &&
    value.length < declaredSize;

  const right = (
    <span className="rd-block__pills">
      {binary && <Badge tone="warning">{t('history.binary')}</Badge>}
      {truncated && <Badge tone="info">{t('history.truncated')}</Badge>}
      {!binary && text ? <CopyButton value={text} /> : null}
    </span>
  );

  return (
    <Collapsible title={title} right={right} defaultOpen>
      {binary ? (
        <div className="rd-block__empty">{t('history.binary')}</div>
      ) : text ? (
        <pre className={`rd-block__pre ${isJson ? 'rd-block__pre--json' : ''}`.trim()}>{text}</pre>
      ) : (
        <div className="rd-block__empty">{t('history.emptyBody')}</div>
      )}
    </Collapsible>
  );
}

ContentBlock.propTypes = {
  title: PropTypes.node,
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.object]),
  declaredSize: PropTypes.number,
  t: PropTypes.func.isRequired,
};

export default function RequestDetailsModal({ open, onClose, record, onViewJson }) {
  const { t } = useTranslation();
  const fmt = useFormatters();

  const url = useMemo(() => {
    if (!record) return '';
    return `${record.requestPath || ''}${record.queryString || ''}`;
  }, [record]);

  if (!open || !record) return null;

  const statusTone =
    record.statusCode >= 200 && record.statusCode < 400 ? 'success' : 'danger';

  const footer = (
    <>
      <button type="button" className="sb-btn sb-btn--ghost" onClick={onViewJson}>
        {t('common.viewJson')}
      </button>
      <button type="button" className="sb-btn sb-btn--primary" disabled title={t('history.replayInExplorer')}>
        {t('history.replayInExplorer')}
      </button>
    </>
  );

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="full"
      title={t('history.detailTitle')}
      footer={footer}
    >
      <div className="rd">
        {/* Hero */}
        <div className="rd-hero">
          <div className="rd-hero__line">
            <MethodBadge method={record.httpMethod} />
            <StatusBadge status={record.statusCode} />
            <span className="rd-hero__url" title={url}>
              {url}
            </span>
            <CopyButton value={url} title={t('common.copy')} />
          </div>
          {record.errorMessage && (
            <div className="rd-hero__error">{record.errorMessage}</div>
          )}
        </div>

        {/* Stat tiles */}
        <div className="rd-stats">
          <StatTile label={t('history.duration')} value={fmt.duration(record.durationMs)} />
          <StatTile label={t('history.status')} value={record.statusCode ?? '—'} tone={statusTone} />
          <StatTile label={t('history.requestSize')} value={fmt.bytes(record.requestBodySize || 0)} />
          <StatTile label={t('history.responseSize')} value={fmt.bytes(record.responseBodySize || 0)} />
          <StatTile label={t('history.when')} value={fmt.dateTime(record.timestampUtc)} />
        </div>

        {/* Identifiers */}
        <section className="rd-section">
          <h3 className="rd-section__title">{t('history.identifiers')}</h3>
          <div className="rd-idgrid">
            <div className="rd-idgrid__row">
              <span className="rd-idgrid__label">{t('history.requestId')}</span>
              <CopyableId value={record.requestId} />
            </div>
            <div className="rd-idgrid__row">
              <span className="rd-idgrid__label">{t('history.endpoint')}</span>
              {record.endpointIdentifier ? (
                <CopyableId value={record.endpointIdentifier} />
              ) : (
                <span className="rd-idgrid__muted">{t('common.none')}</span>
              )}
            </div>
            <div className="rd-idgrid__row">
              <span className="rd-idgrid__label">{t('history.origin')}</span>
              {record.originIdentifier ? (
                <CopyableId value={record.originIdentifier} />
              ) : (
                <span className="rd-idgrid__muted">{t('common.none')}</span>
              )}
            </div>
            <div className="rd-idgrid__row">
              <span className="rd-idgrid__label">{t('history.clientIp')}</span>
              {record.clientIp ? (
                <CopyableId value={record.clientIp} />
              ) : (
                <span className="rd-idgrid__muted">{t('common.none')}</span>
              )}
            </div>
            <div className="rd-idgrid__row">
              <span className="rd-idgrid__label">{t('history.authenticated')}</span>
              <span className="rd-idgrid__muted">
                {record.wasAuthenticated ? t('common.yes') : t('common.no')}
              </span>
            </div>
          </div>
        </section>

        {/* Request / Response panels */}
        <div className="rd-panels">
          <section className="rd-panel">
            <h3 className="rd-panel__title">{t('history.request')}</h3>
            <ContentBlock
              title={t('history.requestHeaders')}
              value={record.requestHeaders}
              t={t}
            />
            <ContentBlock
              title={t('history.requestBody')}
              value={record.requestBody}
              declaredSize={record.requestBodySize}
              t={t}
            />
          </section>

          <section className="rd-panel">
            <h3 className="rd-panel__title">{t('history.response')}</h3>
            <ContentBlock
              title={t('history.responseHeaders')}
              value={record.responseHeaders}
              t={t}
            />
            <ContentBlock
              title={t('history.responseBody')}
              value={record.responseBody}
              declaredSize={record.responseBodySize}
              t={t}
            />
          </section>
        </div>
      </div>
    </Modal>
  );
}

RequestDetailsModal.propTypes = {
  open: PropTypes.bool,
  onClose: PropTypes.func,
  record: PropTypes.object,
  onViewJson: PropTypes.func,
};
