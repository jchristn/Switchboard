import { useId, useState } from 'react';
import PropTypes from 'prop-types';
import { ChevronDown } from './Icons';
import './Collapsible.css';

// Header toggles the body. Controlled when `open`/`onToggle` are supplied, otherwise
// manages its own state (seeded by `defaultOpen`).
export default function Collapsible({
  title,
  children,
  open,
  onToggle,
  defaultOpen = false,
  right,
  className = '',
}) {
  const isControlled = open !== undefined;
  const [internalOpen, setInternalOpen] = useState(defaultOpen);
  const isOpen = isControlled ? open : internalOpen;
  const bodyId = useId();

  const toggle = () => {
    if (isControlled) onToggle?.(!isOpen);
    else setInternalOpen((v) => !v);
  };

  return (
    <div className={`sb-collapsible ${isOpen ? 'sb-collapsible--open' : ''} ${className}`.trim()}>
      <div className="sb-collapsible__header">
        <button
          type="button"
          className="sb-collapsible__toggle"
          onClick={toggle}
          aria-expanded={isOpen}
          aria-controls={bodyId}
        >
          <span className="sb-collapsible__chevron" aria-hidden="true">
            <ChevronDown size={16} />
          </span>
          <span className="sb-collapsible__title">{title}</span>
        </button>
        {right && <div className="sb-collapsible__right">{right}</div>}
      </div>
      {isOpen && (
        <div id={bodyId} className="sb-collapsible__body">
          {children}
        </div>
      )}
    </div>
  );
}

Collapsible.propTypes = {
  title: PropTypes.node,
  children: PropTypes.node,
  open: PropTypes.bool,
  onToggle: PropTypes.func,
  defaultOpen: PropTypes.bool,
  right: PropTypes.node,
  className: PropTypes.string,
};
