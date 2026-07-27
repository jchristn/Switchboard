import { useMemo } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import CopyButton from './CopyButton';
import CopyableId from './CopyableId';
import { formatBytes } from '../../i18n/formatters';
import './JsonViewerModal.css';

// Read-only JSON inspector. Accepts an object (pretty-printed) or a pre-formatted
// string. Header subtitle shows an optional id + the payload byte size.
export default function JsonViewerModal({ open = true, onClose, data, id, title }) {
  const { t } = useTranslation();

  const text = useMemo(() => {
    if (data == null) return '';
    if (typeof data === 'string') {
      try {
        return JSON.stringify(JSON.parse(data), null, 2);
      } catch {
        return data;
      }
    }
    try {
      return JSON.stringify(data, null, 2);
    } catch {
      return String(data);
    }
  }, [data]);

  const byteLength = useMemo(() => {
    try {
      return new TextEncoder().encode(text).length;
    } catch {
      return text.length;
    }
  }, [text]);

  const subtitle = (
    <span className="sb-json__subtitle">
      {id != null && id !== '' && <CopyableId value={id} />}
      <span className="sb-json__bytes">{formatBytes(byteLength)}</span>
    </span>
  );

  const footer = (
    <CopyButton value={text} className="sb-json__copy-btn" title={t('common.copy')} />
  );

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="large"
      title={title || t('common.viewJson')}
      subtitle={subtitle}
      footer={footer}
    >
      <pre className="json-view">{text}</pre>
    </Modal>
  );
}

JsonViewerModal.propTypes = {
  open: PropTypes.bool,
  onClose: PropTypes.func,
  data: PropTypes.oneOfType([PropTypes.object, PropTypes.array, PropTypes.string]),
  id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  title: PropTypes.node,
};
