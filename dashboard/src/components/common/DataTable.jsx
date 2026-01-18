import { useState, useMemo } from 'react';
import PropTypes from 'prop-types';
import './DataTable.css';

function DataTable({
  columns,
  data,
  emptyMessage = 'No data available',
  onRowClick,
  selectedRow,
  rowKey = 'id'
}) {
  const [filters, setFilters] = useState({});
  const [sortConfig, setSortConfig] = useState({ key: null, direction: null });

  // Handle filter change
  const handleFilterChange = (columnKey, value) => {
    setFilters(prev => ({
      ...prev,
      [columnKey]: value
    }));
  };

  // Handle sort click
  const handleSortClick = (columnKey) => {
    setSortConfig(prev => {
      if (prev.key !== columnKey) {
        return { key: columnKey, direction: 'asc' };
      }
      if (prev.direction === 'asc') {
        return { key: columnKey, direction: 'desc' };
      }
      if (prev.direction === 'desc') {
        return { key: null, direction: null };
      }
      return { key: columnKey, direction: 'asc' };
    });
  };

  // Get sort icon for column
  const getSortIcon = (columnKey) => {
    if (sortConfig.key !== columnKey) {
      return (
        <svg className="sort-icon sort-icon-inactive" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M7 15l5 5 5-5"></path>
          <path d="M7 9l5-5 5 5"></path>
        </svg>
      );
    }
    if (sortConfig.direction === 'asc') {
      return (
        <svg className="sort-icon sort-icon-active" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M7 15l5 5 5-5"></path>
        </svg>
      );
    }
    return (
      <svg className="sort-icon sort-icon-active" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M7 9l5-5 5 5"></path>
      </svg>
    );
  };

  // Filter and sort data
  const processedData = useMemo(() => {
    let result = [...(data || [])];

    // Apply filters
    Object.entries(filters).forEach(([columnKey, filterValue]) => {
      if (filterValue && filterValue.trim()) {
        const lowerFilter = filterValue.toLowerCase();
        result = result.filter(row => {
          const cellValue = row[columnKey];
          if (cellValue === null || cellValue === undefined) return false;
          return String(cellValue).toLowerCase().includes(lowerFilter);
        });
      }
    });

    // Apply sorting
    if (sortConfig.key && sortConfig.direction) {
      result.sort((a, b) => {
        const aVal = a[sortConfig.key];
        const bVal = b[sortConfig.key];

        // Handle null/undefined
        if (aVal === null || aVal === undefined) return sortConfig.direction === 'asc' ? 1 : -1;
        if (bVal === null || bVal === undefined) return sortConfig.direction === 'asc' ? -1 : 1;

        // Compare values
        let comparison = 0;
        if (typeof aVal === 'number' && typeof bVal === 'number') {
          comparison = aVal - bVal;
        } else if (aVal instanceof Date && bVal instanceof Date) {
          comparison = aVal.getTime() - bVal.getTime();
        } else {
          comparison = String(aVal).localeCompare(String(bVal));
        }

        return sortConfig.direction === 'asc' ? comparison : -comparison;
      });
    }

    return result;
  }, [data, filters, sortConfig]);

  const isRowSelected = (row) => {
    if (!selectedRow || !rowKey) return false;
    return row[rowKey] === selectedRow[rowKey];
  };

  // Check if any filters are active
  const hasActiveFilters = Object.values(filters).some(f => f && f.trim());

  if (!data || data.length === 0) {
    return (
      <div className="data-table-empty">
        <p>{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="data-table-container">
      <table className="data-table">
        <thead>
          <tr className="data-table-header-row">
            {columns.map((column) => (
              <th key={column.key}>
                <div
                  className="data-table-header-cell"
                  onClick={() => column.sortable !== false && handleSortClick(column.key)}
                >
                  <span className="data-table-header-label">{column.label}</span>
                  {column.sortable !== false && getSortIcon(column.key)}
                </div>
              </th>
            ))}
          </tr>
          <tr className="data-table-filter-row">
            {columns.map((column) => (
              <th key={`filter-${column.key}`}>
                {column.filterable !== false && (
                  <input
                    type="text"
                    className="data-table-filter"
                    placeholder="Filter..."
                    value={filters[column.key] || ''}
                    onChange={(e) => handleFilterChange(column.key, e.target.value)}
                    onClick={(e) => e.stopPropagation()}
                  />
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {processedData.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="data-table-no-results">
                {hasActiveFilters ? 'No matching results' : emptyMessage}
              </td>
            </tr>
          ) : (
            processedData.map((row, rowIndex) => (
              <tr
                key={row.id || row.guid || row[rowKey] || rowIndex}
                onClick={() => onRowClick && onRowClick(row)}
                className={`${onRowClick ? 'clickable' : ''} ${isRowSelected(row) ? 'selected' : ''}`}
              >
                {columns.map((column) => (
                  <td key={column.key}>
                    {column.render
                      ? column.render(row[column.key], row)
                      : row[column.key] ?? '-'}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

DataTable.propTypes = {
  columns: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string.isRequired,
      label: PropTypes.string.isRequired,
      render: PropTypes.func,
    })
  ).isRequired,
  data: PropTypes.array.isRequired,
  emptyMessage: PropTypes.string,
  onRowClick: PropTypes.func,
  selectedRow: PropTypes.object,
  rowKey: PropTypes.string,
};

export default DataTable;
