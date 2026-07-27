import PropTypes from 'prop-types';
import './Segmented.css';

// Accessible segmented control (radio-group semantics). Arrow keys move selection.
export default function Segmented({ options = [], value, onChange, ariaLabel, className = '', size = 'md' }) {
  const currentIndex = options.findIndex((o) => o.value === value);

  const move = (delta) => {
    if (options.length === 0) return;
    const base = currentIndex < 0 ? 0 : currentIndex;
    const next = (base + delta + options.length) % options.length;
    onChange?.(options[next].value);
  };

  const onKeyDown = (e) => {
    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
      e.preventDefault();
      move(1);
    } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
      e.preventDefault();
      move(-1);
    }
  };

  return (
    <div
      className={`sb-segmented sb-segmented--${size} ${className}`.trim()}
      role="radiogroup"
      aria-label={ariaLabel}
      onKeyDown={onKeyDown}
    >
      {options.map((opt) => {
        const selected = opt.value === value;
        return (
          <button
            key={opt.value}
            type="button"
            role="radio"
            aria-checked={selected}
            tabIndex={selected || (currentIndex < 0 && opt === options[0]) ? 0 : -1}
            className={`sb-segmented__option ${selected ? 'sb-segmented__option--selected' : ''}`.trim()}
            onClick={() => onChange?.(opt.value)}
          >
            {opt.label}
          </button>
        );
      })}
    </div>
  );
}

Segmented.propTypes = {
  options: PropTypes.arrayOf(
    PropTypes.shape({
      value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
      label: PropTypes.node,
    })
  ),
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  onChange: PropTypes.func,
  ariaLabel: PropTypes.string,
  className: PropTypes.string,
  size: PropTypes.oneOf(['sm', 'md']),
};
