import Modal from './Modal';
import './ConfirmModal.css';

function ConfirmModal({
  title = 'Confirm',
  message,
  entityName,
  warningMessage = 'This action cannot be undone.',
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmVariant = 'danger',
  onConfirm,
  onClose,
}) {
  return (
    <Modal title={title} onClose={onClose} size="small">
      <div className="confirm-modal">
        <div className={`confirm-icon confirm-icon-${confirmVariant}`}>
          {confirmVariant === 'danger' ? (
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
          ) : (
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10"></circle>
              <path d="M12 16v-4"></path>
              <path d="M12 8h.01"></path>
            </svg>
          )}
        </div>
        <p className="confirm-message">{message}</p>
        {entityName && (
          <p className="confirm-entity-name">{entityName}</p>
        )}
        {warningMessage && (
          <p className="confirm-warning">{warningMessage}</p>
        )}
        <div className="confirm-actions">
          <button className="btn btn-secondary" onClick={onClose}>
            {cancelLabel}
          </button>
          <button
            className={`btn ${confirmVariant === 'danger' ? 'btn-danger' : 'btn-primary'}`}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
}

export default ConfirmModal;
