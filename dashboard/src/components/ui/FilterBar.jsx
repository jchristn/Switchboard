import { useId } from 'react';
import PropTypes from 'prop-types';
import './FilterBar.css';

// Container for a set of filter controls.
export function FilterBar({ children, className = '' }) {
  return <div className={`sb-filterbar ${className}`.trim()}>{children}</div>;
}

FilterBar.propTypes = {
  children: PropTypes.node,
  className: PropTypes.string,
};

// A responsive grid to lay out Field elements.
export function FilterGrid({ children, className = '' }) {
  return <div className={`sb-filterbar__grid ${className}`.trim()}>{children}</div>;
}

FilterGrid.propTypes = {
  children: PropTypes.node,
  className: PropTypes.string,
};

// A labelled control. Associates the label with its input via a generated id when
// `htmlFor` is not supplied by the caller.
export function Field({ label, htmlFor, children, hint, className = '' }) {
  const generatedId = useId();
  const controlId = htmlFor || generatedId;
  return (
    <div className={`sb-field ${className}`.trim()}>
      {label != null && (
        <label className="sb-field__label" htmlFor={controlId}>
          {label}
        </label>
      )}
      <div className="sb-field__control">{children}</div>
      {hint && <div className="sb-field__hint">{hint}</div>}
    </div>
  );
}

Field.propTypes = {
  label: PropTypes.node,
  htmlFor: PropTypes.string,
  children: PropTypes.node,
  hint: PropTypes.node,
  className: PropTypes.string,
};

// Right-aligned action cluster (apply/clear buttons).
export function FilterActions({ children, className = '' }) {
  return <div className={`sb-filterbar__actions ${className}`.trim()}>{children}</div>;
}

FilterActions.propTypes = {
  children: PropTypes.node,
  className: PropTypes.string,
};

export default FilterBar;
