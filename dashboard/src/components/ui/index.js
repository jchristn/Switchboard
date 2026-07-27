// Switchboard reusable UI component library.
// Self-contained: depends only on react, prop-types, react-dom, react-i18next,
// the i18n formatters, and sibling components in this folder.

export * as Icons from './Icons';

export { default as Badge, StatusBadge, MethodBadge, HealthBadge } from './Badge';

export { default as CopyButton } from './CopyButton';
export { default as CopyableId } from './CopyableId';

export { default as Modal } from './Modal';
export { default as ConfirmModal } from './ConfirmModal';
export { default as JsonViewerModal } from './JsonViewerModal';

export { default as ActionMenu } from './ActionMenu';
export { entityActions } from './entityActions';

export { default as DataTable, shouldIgnoreRowClick } from './DataTable';
export { default as TablePagination } from './TablePagination';

export { default as Metric } from './Metric';
export { default as PageHeader } from './PageHeader';

export {
  default as ActivityChart,
  TIME_RANGES,
  generateBuckets,
  mergeBuckets,
} from './ActivityChart';

export { default as EmptyState } from './EmptyState';
export { default as ErrorBanner } from './ErrorBanner';
export { default as Collapsible } from './Collapsible';
export { default as Segmented } from './Segmented';

export { FilterBar, Field, FilterGrid, FilterActions } from './FilterBar';
