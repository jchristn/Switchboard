import { useCallback, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { Close } from './Icons';
import './Modal.css';

const SIZES = ['small', 'medium', 'large', 'xl', 'full'];

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

// Portalled dialog. Backdrop + dialog escape any clipping ancestor. Closes on
// Escape and backdrop click, traps Tab focus, and restores focus on unmount.
export default function Modal({
  open = true,
  onClose,
  title,
  subtitle,
  size = 'medium',
  children,
  footer,
  closeOnBackdrop = true,
  className = '',
}) {
  const { t } = useTranslation();
  const dialogRef = useRef(null);
  const restoreRef = useRef(null);
  const safeSize = SIZES.includes(size) ? size : 'medium';

  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onClose?.();
        return;
      }
      if (e.key === 'Tab') {
        const nodes = dialogRef.current?.querySelectorAll(FOCUSABLE);
        if (!nodes || nodes.length === 0) {
          e.preventDefault();
          return;
        }
        const list = Array.from(nodes).filter((n) => n.offsetParent !== null || n === document.activeElement);
        if (list.length === 0) return;
        const first = list[0];
        const last = list[list.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    },
    [onClose]
  );

  useEffect(() => {
    if (!open) return undefined;
    restoreRef.current = document.activeElement;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    // Focus the first focusable element (or the dialog itself).
    const node = dialogRef.current;
    const focusable = node?.querySelector(FOCUSABLE);
    (focusable || node)?.focus();
    return () => {
      document.body.style.overflow = prevOverflow;
      const restore = restoreRef.current;
      if (restore && typeof restore.focus === 'function') restore.focus();
    };
  }, [open]);

  if (!open) return null;

  const handleBackdrop = (e) => {
    if (closeOnBackdrop && e.target === e.currentTarget) onClose?.();
  };

  return createPortal(
    <div className="sb-modal-backdrop" onMouseDown={handleBackdrop}>
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label={typeof title === 'string' ? title : undefined}
        className={`sb-modal sb-modal--${safeSize} ${className}`.trim()}
        tabIndex={-1}
        onKeyDown={handleKeyDown}
      >
        <header className="sb-modal__header">
          <div className="sb-modal__titles">
            {title != null && <h2 className="sb-modal__title">{title}</h2>}
            {subtitle != null && <div className="sb-modal__subtitle">{subtitle}</div>}
          </div>
          <button
            type="button"
            className="sb-modal__close"
            onClick={onClose}
            aria-label={t('common.close')}
            title={t('common.close')}
          >
            <Close size={18} />
          </button>
        </header>
        <div className="sb-modal__body">{children}</div>
        {footer != null && <footer className="sb-modal__footer">{footer}</footer>}
      </div>
    </div>,
    document.body
  );
}

Modal.propTypes = {
  open: PropTypes.bool,
  onClose: PropTypes.func,
  title: PropTypes.node,
  subtitle: PropTypes.node,
  size: PropTypes.oneOf(SIZES),
  children: PropTypes.node,
  footer: PropTypes.node,
  closeOnBackdrop: PropTypes.bool,
  className: PropTypes.string,
};
