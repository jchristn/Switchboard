import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight, Refresh } from './Icons';
import { formatNumber } from '../../i18n/formatters';
import './TablePagination.css';

const PAGE_SIZES = [10, 25, 50, 100, 250, 500, 1000];

// Above-table toolbar: left slot for filters/add controls, right side for paging.
export default function TablePagination({
  total = 0,
  pageNumber = 1,
  pageSize = 25,
  onPageChange,
  onPageSizeChange,
  onRefresh,
  pageSizeOptions = PAGE_SIZES,
  children,
}) {
  const { t } = useTranslation();
  const [jumpValue, setJumpValue] = useState('');

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(Math.max(1, pageNumber), totalPages);
  const from = total === 0 ? 0 : (current - 1) * pageSize + 1;
  const to = Math.min(current * pageSize, total);

  const goTo = (page) => {
    const clamped = Math.min(Math.max(1, page), totalPages);
    if (clamped !== current) onPageChange?.(clamped);
  };

  const submitJump = (e) => {
    e.preventDefault();
    const n = parseInt(jumpValue, 10);
    if (!Number.isNaN(n)) goTo(n);
    setJumpValue('');
  };

  return (
    <div className="sb-pagination">
      <div className="sb-pagination__left">{children}</div>
      <div className="sb-pagination__right">
        <span className="sb-pagination__showing">
          {t('table.showing', {
            from: formatNumber(from),
            to: formatNumber(to),
            total: formatNumber(total),
          })}
        </span>

        <label className="sb-pagination__pagesize">
          <span className="sb-pagination__pagesize-label">{t('table.pageSize')}</span>
          <select
            className="sb-pagination__select"
            value={pageSize}
            onChange={(e) => onPageSizeChange?.(Number(e.target.value))}
            aria-label={t('table.pageSize')}
            title={t('table.pageSizeTip')}
          >
            {pageSizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>

        <div className="sb-pagination__pager">
          <button
            type="button"
            className="sb-pagination__btn"
            onClick={() => goTo(1)}
            disabled={current <= 1}
            aria-label={t('table.first')}
            title={t('table.first')}
          >
            <ChevronLeft size={14} />
            <ChevronLeft size={14} className="sb-pagination__btn-stack" />
          </button>
          <button
            type="button"
            className="sb-pagination__btn"
            onClick={() => goTo(current - 1)}
            disabled={current <= 1}
            aria-label={t('table.prev')}
            title={t('table.prev')}
          >
            <ChevronLeft size={16} />
          </button>

          <form className="sb-pagination__jump" onSubmit={submitJump}>
            <input
              type="text"
              inputMode="numeric"
              className="sb-pagination__jump-input"
              value={jumpValue}
              onChange={(e) => setJumpValue(e.target.value.replace(/[^0-9]/g, ''))}
              placeholder={String(current)}
              aria-label={t('table.jump')}
              title={t('table.jump')}
            />
            <span className="sb-pagination__page-of">
              {t('table.page', { page: current, pages: totalPages })}
            </span>
          </form>

          <button
            type="button"
            className="sb-pagination__btn"
            onClick={() => goTo(current + 1)}
            disabled={current >= totalPages}
            aria-label={t('table.nextPage')}
            title={t('table.nextPage')}
          >
            <ChevronRight size={16} />
          </button>
          <button
            type="button"
            className="sb-pagination__btn"
            onClick={() => goTo(totalPages)}
            disabled={current >= totalPages}
            aria-label={t('table.last')}
            title={t('table.last')}
          >
            <ChevronRight size={14} />
            <ChevronRight size={14} className="sb-pagination__btn-stack" />
          </button>
        </div>

        {onRefresh && (
          <button
            type="button"
            className="sb-pagination__btn sb-pagination__refresh"
            onClick={onRefresh}
            aria-label={t('common.refresh')}
            title={t('common.refresh')}
          >
            <Refresh size={16} />
          </button>
        )}
      </div>
    </div>
  );
}

TablePagination.propTypes = {
  total: PropTypes.number,
  pageNumber: PropTypes.number,
  pageSize: PropTypes.number,
  onPageChange: PropTypes.func,
  onPageSizeChange: PropTypes.func,
  onRefresh: PropTypes.func,
  pageSizeOptions: PropTypes.arrayOf(PropTypes.number),
  children: PropTypes.node,
};
