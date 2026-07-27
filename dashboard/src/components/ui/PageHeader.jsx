import PropTypes from 'prop-types';
import './PageHeader.css';

// Page title block with an optional subtitle and a right-aligned actions slot.
export default function PageHeader({ title, subtitle, actions, className = '' }) {
  return (
    <div className={`sb-page-header ${className}`.trim()}>
      <div className="sb-page-header__titles">
        <h1 className="sb-page-header__title">{title}</h1>
        {subtitle != null && <p className="sb-page-header__subtitle">{subtitle}</p>}
      </div>
      {actions != null && <div className="sb-page-header__actions">{actions}</div>}
    </div>
  );
}

PageHeader.propTypes = {
  title: PropTypes.node,
  subtitle: PropTypes.node,
  actions: PropTypes.node,
  className: PropTypes.string,
};
