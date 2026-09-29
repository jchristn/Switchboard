import { useCallback, useState } from 'react';

const KEY_PREFIX = 'switchboard_page_size_';
const MAX_PAGE_SIZE = 1000;

// Storage can be unavailable or throw (private windows, blocked site data), so every access is
// guarded and the table falls back to its default.
function readSize(key, fallback) {
  try {
    const raw = localStorage.getItem(KEY_PREFIX + key);
    const parsed = raw === null ? NaN : Number.parseInt(raw, 10);
    if (Number.isInteger(parsed) && parsed > 0 && parsed <= MAX_PAGE_SIZE) return parsed;
  } catch {
    // ignore and use the default
  }
  return fallback;
}

// Rows-per-page for a table, remembered per table across page visits and reloads.
export function usePersistentPageSize(key, defaultSize = 25) {
  const [pageSize, setPageSizeState] = useState(() => readSize(key, defaultSize));

  const setPageSize = useCallback(
    (size) => {
      setPageSizeState(size);
      try {
        localStorage.setItem(KEY_PREFIX + key, String(size));
      } catch {
        // ignore; the choice still applies for this visit
      }
    },
    [key]
  );

  return [pageSize, setPageSize];
}
