import { createElement } from 'react';
import { Eye, Code, Pencil, Trash } from './Icons';

// Builds a standard entity action list for <ActionMenu items={...} />.
// Pass the i18n `t` function so labels are resolved by the caller.
//
//   entityActions(t, { onView, onViewJson, onEdit, onDelete, canEdit, canDelete, extra })
//
// Order: View, ...extra, View JSON, Edit (hidden when !canEdit), divider, Delete
// (danger, hidden when !canDelete).
export function entityActions(
  t,
  { onView, onViewJson, onEdit, onDelete, canEdit = true, canDelete = true, extra = [] } = {}
) {
  const items = [];

  if (onView) {
    items.push({
      key: 'view',
      label: t('common.view'),
      icon: createElement(Eye, { size: 16 }),
      onClick: onView,
    });
  }

  for (const item of extra) {
    if (item) items.push(item);
  }

  if (onViewJson) {
    items.push({
      key: 'viewJson',
      label: t('common.viewJson'),
      icon: createElement(Code, { size: 16 }),
      onClick: onViewJson,
    });
  }

  if (onEdit) {
    items.push({
      key: 'edit',
      label: t('common.edit'),
      icon: createElement(Pencil, { size: 16 }),
      onClick: onEdit,
      hidden: !canEdit,
    });
  }

  if (onDelete) {
    items.push({ key: 'delete-divider', divider: true, hidden: !canDelete });
    items.push({
      key: 'delete',
      label: t('common.delete'),
      icon: createElement(Trash, { size: 16 }),
      onClick: onDelete,
      danger: true,
      hidden: !canDelete,
    });
  }

  return items;
}

export default entityActions;
