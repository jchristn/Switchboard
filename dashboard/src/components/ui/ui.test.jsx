import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { StatusBadge, MethodBadge } from './Badge';
import { entityActions } from './entityActions';

const t = (k) => k;

describe('StatusBadge', () => {
  it('colors numeric HTTP status by class', () => {
    expect(render(<StatusBadge status={200} />).container.querySelector('.sb-badge--success')).toBeTruthy();
    expect(render(<StatusBadge status={404} />).container.querySelector('.sb-badge--warning')).toBeTruthy();
    expect(render(<StatusBadge status={500} />).container.querySelector('.sb-badge--danger')).toBeTruthy();
  });

  it('colors known status strings', () => {
    expect(render(<StatusBadge status="healthy" />).container.querySelector('.sb-badge--success')).toBeTruthy();
    expect(render(<StatusBadge status="failed" />).container.querySelector('.sb-badge--danger')).toBeTruthy();
  });
});

describe('MethodBadge', () => {
  it('renders the method and a tone', () => {
    const { container, getByText } = render(<MethodBadge method="get" />);
    expect(getByText('GET')).toBeTruthy();
    expect(container.querySelector('.sb-badge--info')).toBeTruthy();
  });
});

describe('entityActions', () => {
  it('includes only actions whose handler is provided, in order', () => {
    const items = entityActions(t, { onView: () => {}, onViewJson: () => {}, onDelete: () => {} });
    const keys = items.filter((i) => !i.divider).map((i) => i.key);
    expect(keys).toEqual(['view', 'viewJson', 'delete']);
    expect(items.find((i) => i.key === 'edit')).toBeUndefined();
    expect(items.find((i) => i.key === 'delete').danger).toBe(true);
  });

  it('supports extra actions and edit gating', () => {
    const items = entityActions(t, {
      onEdit: () => {},
      onDelete: () => {},
      canDelete: false,
      extra: [{ key: 'regen', label: 'Regenerate', onClick: () => {} }],
    });
    expect(items.find((i) => i.key === 'regen')).toBeTruthy();
    expect(items.find((i) => i.key === 'delete').hidden).toBe(true);
  });
});
