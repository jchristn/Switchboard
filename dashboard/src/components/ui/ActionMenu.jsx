import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { Kebab } from './Icons';
import './ActionMenu.css';

const MENU_WIDTH = 200;
const MENU_MARGIN = 6;

// Kebab-triggered menu. The panel is portalled to <body> with position:fixed so it
// escapes table/overflow clipping, and flips above the trigger near the viewport
// bottom. Closes on outside click, scroll (capture), resize and Escape.
export default function ActionMenu({ items = [], label, buttonClassName = '', align = 'end' }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [coords, setCoords] = useState({ top: 0, left: 0 });
  const triggerRef = useRef(null);
  const menuRef = useRef(null);
  const activeIndexRef = useRef(-1);

  const visibleItems = items.filter((it) => it && !it.hidden);

  const position = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    const menuHeight = menuRef.current?.offsetHeight || 0;
    const spaceBelow = window.innerHeight - rect.bottom;
    const flipUp = spaceBelow < menuHeight + MENU_MARGIN && rect.top > spaceBelow;

    let left =
      align === 'start'
        ? rect.left
        : rect.right - MENU_WIDTH;
    left = Math.max(MENU_MARGIN, Math.min(left, window.innerWidth - MENU_WIDTH - MENU_MARGIN));

    const top = flipUp
      ? Math.max(MENU_MARGIN, rect.top - menuHeight - MENU_MARGIN)
      : rect.bottom + MENU_MARGIN;

    setCoords({ top, left });
  }, [align]);

  useLayoutEffect(() => {
    if (open) position();
  }, [open, position]);

  useEffect(() => {
    if (!open) return undefined;

    const onDocPointer = (e) => {
      if (
        !menuRef.current?.contains(e.target) &&
        !triggerRef.current?.contains(e.target)
      ) {
        setOpen(false);
      }
    };
    const onScroll = () => setOpen(false);
    const onResize = () => setOpen(false);
    const onKey = (e) => {
      if (e.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };

    document.addEventListener('mousedown', onDocPointer);
    document.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', onResize);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDocPointer);
      document.removeEventListener('scroll', onScroll, true);
      window.removeEventListener('resize', onResize);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const focusItem = (index) => {
    const nodes = menuRef.current?.querySelectorAll('[role="menuitem"]:not([disabled])');
    if (!nodes || nodes.length === 0) return;
    const clamped = (index + nodes.length) % nodes.length;
    activeIndexRef.current = clamped;
    nodes[clamped]?.focus();
  };

  useEffect(() => {
    if (open) {
      activeIndexRef.current = -1;
      // focus first item after paint
      const id = requestAnimationFrame(() => focusItem(0));
      return () => cancelAnimationFrame(id);
    }
    return undefined;
  }, [open]);

  const onMenuKeyDown = (e) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      focusItem(activeIndexRef.current + 1);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      focusItem(activeIndexRef.current - 1);
    } else if (e.key === 'Home') {
      e.preventDefault();
      focusItem(0);
    } else if (e.key === 'End') {
      e.preventDefault();
      focusItem(-1);
    }
  };

  const runItem = (item) => {
    if (item.disabled) return;
    setOpen(false);
    item.onClick?.();
  };

  const triggerLabel = label || t('common.actions');

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className={`sb-action-menu__trigger ${buttonClassName}`.trim()}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={triggerLabel}
        title={triggerLabel}
        data-row-click-ignore=""
        onClick={(e) => {
          e.stopPropagation();
          setOpen((v) => !v);
        }}
      >
        <Kebab size={18} />
      </button>
      {open &&
        createPortal(
          <div
            ref={menuRef}
            role="menu"
            aria-label={triggerLabel}
            className="sb-action-menu"
            style={{ top: coords.top, left: coords.left, width: MENU_WIDTH }}
            data-row-click-ignore=""
            onKeyDown={onMenuKeyDown}
            onClick={(e) => e.stopPropagation()}
          >
            {visibleItems.map((item, i) =>
              item.divider ? (
                <div key={item.key || `divider-${i}`} className="sb-action-menu__divider" role="separator" />
              ) : (
                <button
                  key={item.key}
                  type="button"
                  role="menuitem"
                  className={`sb-action-menu__item ${item.danger ? 'sb-action-menu__item--danger' : ''}`.trim()}
                  disabled={item.disabled}
                  onClick={() => runItem(item)}
                >
                  {item.icon && <span className="sb-action-menu__icon">{item.icon}</span>}
                  <span className="sb-action-menu__label">{item.label}</span>
                </button>
              )
            )}
          </div>,
          document.body
        )}
    </>
  );
}

ActionMenu.propTypes = {
  items: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string,
      label: PropTypes.node,
      icon: PropTypes.node,
      onClick: PropTypes.func,
      danger: PropTypes.bool,
      disabled: PropTypes.bool,
      divider: PropTypes.bool,
      hidden: PropTypes.bool,
    })
  ),
  label: PropTypes.string,
  buttonClassName: PropTypes.string,
  align: PropTypes.oneOf(['start', 'end']),
};
