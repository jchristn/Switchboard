import PropTypes from 'prop-types';
import CopyButton from './CopyButton';
import './CopyButton.css';

// A monospace, truncated identifier with a copy affordance.
export default function CopyableId({ value, label, className = '', ...props }) {
  if (value == null || value === '') return null;
  const text = String(value);
  return (
    <span className={`sb-copyable-id ${className}`.trim()} {...props}>
      <span className="sb-copyable-id__value" title={text}>
        {label != null ? label : text}
      </span>
      <CopyButton value={text} />
    </span>
  );
}

CopyableId.propTypes = {
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  label: PropTypes.node,
  className: PropTypes.string,
};
