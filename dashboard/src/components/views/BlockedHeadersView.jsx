import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useApp } from '../../context/AppContext';
import {
  PageHeader,
  DataTable,
  TablePagination,
  ActionMenu,
  entityActions,
  ConfirmModal,
  JsonViewerModal,
  Icons,
} from '../ui';
import './ResourceViews.css';

export default function BlockedHeadersView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { showSuccess, showError } = useApp();

  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const [headerName, setHeaderName] = useState('');
  const [adding, setAdding] = useState(false);

  const [jsonRow, setJsonRow] = useState(null);
  const [deleteRow, setDeleteRow] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await apiClient.getBlockedHeaders();
      setRows(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || t('blockedHeaders.loadError'));
    } finally {
      setLoading(false);
    }
  }, [apiClient, t]);

  useEffect(() => {
    load();
  }, [load]);

  const addHeader = async (e) => {
    e.preventDefault();
    const name = headerName.trim();
    if (!name) return;
    setAdding(true);
    try {
      await apiClient.createBlockedHeader({ headerName: name });
      showSuccess(t('blockedHeaders.added'));
      setHeaderName('');
      await load();
    } catch (err) {
      showError(`${t('blockedHeaders.addError')}: ${err.message}`);
    } finally {
      setAdding(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteRow) return;
    setDeleting(true);
    try {
      await apiClient.deleteBlockedHeader(deleteRow.id);
      showSuccess(t('blockedHeaders.deleted'));
      setDeleteRow(null);
      await load();
    } catch (err) {
      showError(`${t('blockedHeaders.deleteError')}: ${err.message}`);
    } finally {
      setDeleting(false);
    }
  };

  const columns = [
    { key: 'headerName', label: t('blockedHeaders.headerName'), sortable: true, filterable: true, mono: true },
    {
      key: 'actions',
      label: '',
      isAction: true,
      align: 'end',
      width: '56px',
      render: (row) => (
        <ActionMenu
          items={entityActions(t, {
            onViewJson: () => setJsonRow(row),
            onDelete: () => setDeleteRow(row),
          })}
        />
      ),
    },
  ];

  const start = (pageNumber - 1) * pageSize;
  const paged = rows.slice(start, start + pageSize);

  return (
    <div className="rv-view">
      <PageHeader title={t('blockedHeaders.title')} subtitle={t('blockedHeaders.subtitle')} />

      <form className="rv-inline-form" onSubmit={addHeader}>
        <div className="form-group">
          <label className="form-label" htmlFor="bh-name" title={t('blockedHeaders.headerNameTip')}>{t('blockedHeaders.headerName')}</label>
          <input
            id="bh-name"
            className="form-input"
            title={t('blockedHeaders.headerNameTip')}
            value={headerName}
            onChange={(e) => setHeaderName(e.target.value)}
            placeholder={t('blockedHeaders.headerPlaceholder')}
          />
        </div>
        <button type="submit" className="btn btn-primary" disabled={adding || !headerName.trim()}>
          <Icons.Plus size={16} />
          <span>{t('blockedHeaders.add')}</span>
        </button>
      </form>

      <TablePagination
        total={rows.length}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageNumber(1);
        }}
        onRefresh={load}
      />

      <DataTable
        columns={columns}
        rows={paged}
        rowKey={(r) => r.id}
        loading={loading}
        error={error}
        onRetry={load}
        emptyMessage={t('blockedHeaders.empty')}
        emptyHint={t('blockedHeaders.emptyHint')}
      />

      <JsonViewerModal
        open={!!jsonRow}
        onClose={() => setJsonRow(null)}
        data={jsonRow}
        id={jsonRow?.id}
        title={t('blockedHeaders.viewTitle')}
      />

      <ConfirmModal
        open={!!deleteRow}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        variant="danger"
        title={t('blockedHeaders.deleteTitle')}
        message={t('blockedHeaders.deleteMessage', { name: deleteRow?.headerName })}
        confirmLabel={t('common.delete')}
        busy={deleting}
      />
    </div>
  );
}
