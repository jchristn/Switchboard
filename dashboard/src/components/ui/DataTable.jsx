import { useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { ChevronUp, ChevronDown } from './Icons';
import EmptyState from './EmptyState';
import ErrorBanner from './ErrorBanner';
import './DataTable.css';

// Returns true when a row click originated inside an interactive element and should
// therefore NOT trigger onRowClick.
export function shouldIgnoreRowClick(target) {
  if (!target || typeof target.closest !== 'function') return false;
  return Boolean(
    target.closest('input, button, a, select, textarea, label, [role="menu"], [data-row-click-ignore]')
  );
}

function compareValues(a, b) {
  if (a == null && b == null) return 0;
  if (a == null) return -1;
  if (b == null) return 1;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a).localeCompare(String(b), undefined, { numeric: true });
}

export default function DataTable({
  columns = [],
  rows = [],
  rowKey,
  loading = false,
  error = null,
  onRetry,
  onRowClick,
  emptyMessage,
  emptyHint,
  className = '',
}) {
  const { t } = useTranslation();
  const [sort, setSort] = useState({ key: null, dir: 'asc' });
  const [filters, setFilters] = useState({});

  const hasFilterRow = columns.some((c) => c.filterable);

  const toggleSort = (col) => {
    if (!col.sortable) return;
    setSort((prev) => {
      if (prev.key !== col.key) return { key: col.key, dir: 'asc' };
      if (prev.dir === 'asc') return { key: col.key, dir: 'desc' };
      return { key: null, dir: 'asc' };
    });
  };

  const setColumnFilter = (key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
  };

  const sortValue = (row, col) => {
    if (col.sortValue) return col.sortValue(row);
    return row[col.key];
  };

  const filterValue = (row, col) => {
    if (col.filterValue) return col.filterValue(row);
    const raw = row[col.key];
    return raw == null ? '' : String(raw);
  };

  const processed = useMemo(() => {
    let out = rows;
    const activeFilters = Object.entries(filters).filter(([, v]) => v && v.trim() !== '');
    if (activeFilters.length) {
      out = out.filter((row) =>
        activeFilters.every(([key, needle]) => {
          const col = columns.find((c) => c.key === key);
          if (!col) return true;
          return filterValue(row, col).toLowerCase().includes(needle.toLowerCase());
        })
      );
    }
    if (sort.key) {
      const col = columns.find((c) => c.key === sort.key);
      if (col) {
        out = [...out].sort((ra, rb) => {
          const res = compareValues(sortValue(ra, col), sortValue(rb, col));
          return sort.dir === 'asc' ? res : -res;
        });
      }
    }
    return out;
  }, [rows, columns, filters, sort]);

  const colCount = columns.length;

  const handleRowClick = (e, row) => {
    if (!onRowClick) return;
    if (shouldIgnoreRowClick(e.target)) return;
    onRowClick(row);
  };

  const renderBody = () => {
    if (loading) {
      return (
        <tr>
          <td className="sb-dt__state" colSpan={colCount}>
            <div className="sb-dt__loading">{t('common.loading')}</div>
          </td>
        </tr>
      );
    }
    if (processed.length === 0) {
      return (
        <tr>
          <td className="sb-dt__state" colSpan={colCount}>
            <EmptyState
              message={emptyMessage || t('table.empty')}
              hint={emptyHint || t('table.emptyHint')}
            />
          </td>
        </tr>
      );
    }
    return processed.map((row, idx) => {
      const key = rowKey ? rowKey(row) : idx;
      return (
        <tr
          key={key}
          className={onRowClick ? 'sb-dt__row sb-dt__row--clickable' : 'sb-dt__row'}
          onClick={(e) => handleRowClick(e, row)}
        >
          {columns.map((col) => (
            <td
              key={col.key}
              className={`sb-dt__cell ${col.mono ? 'sb-dt__cell--mono' : ''} ${col.isAction ? 'sb-dt__cell--action' : ''}`.trim()}
              style={{ textAlign: col.align || undefined }}
              {...(col.isAction ? { 'data-row-click-ignore': '' } : {})}
            >
              {col.render ? col.render(row) : row[col.key]}
            </td>
          ))}
        </tr>
      );
    });
  };

  return (
    <div className={`sb-dt ${className}`.trim()}>
      {error && (
        <ErrorBanner
          message={typeof error === 'string' ? error : t('table.error')}
          onRetry={onRetry}
        />
      )}
      <div className="sb-dt__scroll">
        <table className="sb-dt__table">
          <thead>
            <tr>
              {columns.map((col) => {
                const isSorted = sort.key === col.key;
                const ariaSort = col.sortable
                  ? isSorted
                    ? sort.dir === 'asc'
                      ? 'ascending'
                      : 'descending'
                    : 'none'
                  : undefined;
                return (
                  <th
                    key={col.key}
                    className={`sb-dt__th ${col.sortable ? 'sb-dt__th--sortable' : ''} ${col.isAction ? 'sb-dt__th--action' : ''}`.trim()}
                    style={{ textAlign: col.align || undefined, width: col.width }}
                    aria-sort={ariaSort}
                    scope="col"
                  >
                    {col.sortable ? (
                      <button
                        type="button"
                        className="sb-dt__sort-btn"
                        onClick={() => toggleSort(col)}
                      >
                        <span>{col.label}</span>
                        {isSorted &&
                          (sort.dir === 'asc' ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
                      </button>
                    ) : (
                      col.label
                    )}
                  </th>
                );
              })}
            </tr>
            {hasFilterRow && (
              <tr className="sb-dt__filter-row">
                {columns.map((col) => (
                  <th key={col.key} className="sb-dt__filter-cell">
                    {col.filterable ? (
                      <input
                        type="text"
                        className="sb-dt__filter-input"
                        value={filters[col.key] || ''}
                        onChange={(e) => setColumnFilter(col.key, e.target.value)}
                        aria-label={t('table.filterColumn', {
                          column: typeof col.label === 'string' ? col.label : col.key,
                        })}
                        placeholder={t('common.filter')}
                        data-row-click-ignore=""
                      />
                    ) : null}
                  </th>
                ))}
              </tr>
            )}
          </thead>
          <tbody>{renderBody()}</tbody>
        </table>
      </div>
    </div>
  );
}

DataTable.propTypes = {
  columns: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string.isRequired,
      label: PropTypes.node,
      align: PropTypes.oneOf(['start', 'center', 'end', 'left', 'right']),
      render: PropTypes.func,
      sortable: PropTypes.bool,
      sortValue: PropTypes.func,
      filterable: PropTypes.bool,
      filterValue: PropTypes.func,
      isAction: PropTypes.bool,
      width: PropTypes.oneOfType([PropTypes.number, PropTypes.string]),
      mono: PropTypes.bool,
    })
  ),
  rows: PropTypes.array,
  rowKey: PropTypes.func,
  loading: PropTypes.bool,
  error: PropTypes.oneOfType([PropTypes.string, PropTypes.object, PropTypes.bool]),
  onRetry: PropTypes.func,
  onRowClick: PropTypes.func,
  emptyMessage: PropTypes.string,
  emptyHint: PropTypes.string,
  className: PropTypes.string,
};
