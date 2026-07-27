import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import * as F from '../i18n/formatters';

// Binds the locale-aware formatters to the active i18n language so components call e.g. `fmt.date(x)`.
export function useFormatters() {
  const { i18n } = useTranslation();
  const locale = i18n.language;
  return useMemo(
    () => ({
      number: (v, o) => F.formatNumber(v, locale, o),
      percent: (v, d) => F.formatPercent(v, locale, d),
      date: (v) => F.formatDate(v, locale),
      time: (v) => F.formatTime(v, locale),
      dateTime: (v, o) => F.formatDateTime(v, locale, o),
      relative: (v) => F.formatRelativeTime(v, locale),
      duration: (v) => F.formatDuration(v, locale),
      bytes: (v) => F.formatBytes(v, locale),
      list: (v, t) => F.formatList(v, locale, t),
    }),
    [locale]
  );
}

export default useFormatters;
