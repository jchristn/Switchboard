import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { usePersistentPageSize } from './usePersistentPageSize';

describe('usePersistentPageSize', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  it('uses the default when nothing is stored', () => {
    const { result } = renderHook(() => usePersistentPageSize('origins', 25));
    expect(result.current[0]).toBe(25);
  });

  it('persists a change and restores it on the next visit', () => {
    const first = renderHook(() => usePersistentPageSize('origins', 25));
    act(() => first.result.current[1](100));
    expect(first.result.current[0]).toBe(100);
    first.unmount();

    const second = renderHook(() => usePersistentPageSize('origins', 25));
    expect(second.result.current[0]).toBe(100);
  });

  it('keeps each table separate', () => {
    const origins = renderHook(() => usePersistentPageSize('origins', 25));
    act(() => origins.result.current[1](50));
    const history = renderHook(() => usePersistentPageSize('history', 50));
    expect(history.result.current[0]).toBe(50);
    act(() => history.result.current[1](250));
    expect(renderHook(() => usePersistentPageSize('origins', 25)).result.current[0]).toBe(50);
  });

  it('ignores invalid stored values', () => {
    for (const bad of ['abc', '0', '-5', '99999']) {
      localStorage.setItem('switchboard_page_size_users', bad);
      expect(renderHook(() => usePersistentPageSize('users', 25)).result.current[0]).toBe(25);
    }
  });

  it('still works when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const { result } = renderHook(() => usePersistentPageSize('users', 25));
    expect(result.current[0]).toBe(25);
    act(() => result.current[1](10));
    expect(result.current[0]).toBe(10);
  });
});
