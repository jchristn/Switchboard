import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import { Warning } from './Icons';
import './Modal.css';

const VARIANTS = ['danger', 'warning'];

// Confirm/cancel dialog built on Modal. The confirm button is tinted with the
// variant's tone color.
export default function ConfirmModal({
  open = true,
  onCancel,
  onConfirm,
  title,
  message,
  variant = 'danger',
  confirmLabel,
  cancelLabel,
  busy = false,
}) {
  const { t } = useTranslation();
  const safeVariant = VARIANTS.includes(variant) ? variant : 'danger';

  const footer = (
    <>
      <button type="button" className="sb-btn sb-btn--ghost" onClick={onCancel} disabled={busy}>
        {cancelLabel || t('common.cancel')}
      </button>
      <button
        type="button"
        className={`sb-btn sb-btn--${safeVariant}`}
        onClick={onConfirm}
        disabled={busy}
      >
        {confirmLabel || t('common.confirm')}
      </button>
    </>
  );

  return (
    <Modal open={open} onClose={onCancel} title={title} size="small" footer={footer}>
      <div className={`sb-confirm sb-confirm--${safeVariant}`}>
        <span className="sb-confirm__icon" aria-hidden="true">
          <Warning size={22} />
        </span>
        <div className="sb-confirm__message">{message}</div>
      </div>
    </Modal>
  );
}

ConfirmModal.propTypes = {
  open: PropTypes.bool,
  onCancel: PropTypes.func,
  onConfirm: PropTypes.func,
  title: PropTypes.node,
  message: PropTypes.node,
  variant: PropTypes.oneOf(VARIANTS),
  confirmLabel: PropTypes.string,
  cancelLabel: PropTypes.string,
  busy: PropTypes.bool,
};
