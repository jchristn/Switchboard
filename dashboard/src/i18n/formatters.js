// Locale-aware formatting helpers. Every user-visible number, date, duration, and byte count goes
// through these so output follows the active locale instead of the browser default. i18next's active
// language is the source of truth; pass it from callers via useFormatters() (see hooks/useFormatters).

import i18n from './index';

function activeLocale(explicit) {
  return explicit || i18n.language || 'en';
}

export function formatNumber(value, locale, options) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return new Intl.NumberFormat(activeLocale(locale), options).format(value);
}

export function formatPercent(value, locale, fractionDigits = 1) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return new Intl.NumberFormat(activeLocale(locale), {
    style: 'percent',
    minimumFractionDigits: 0,
    maximumFractionDigits: fractionDigits,
  }).format(value / 100);
}

export function formatDateTime(value, locale, options) {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat(
    activeLocale(locale),
    options || { dateStyle: 'medium', timeStyle: 'medium' }
  ).format(date);
}

export function formatDate(value, locale) {
  return formatDateTime(value, locale, { dateStyle: 'medium' });
}

export function formatTime(value, locale) {
  return formatDateTime(value, locale, { timeStyle: 'medium' });
}

export function formatRelativeTime(value, locale) {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  const rtf = new Intl.RelativeTimeFormat(activeLocale(locale), { numeric: 'auto' });
  const diffMs = date.getTime() - Date.now();
  const abs = Math.abs(diffMs);
  const units = [
    ['year', 31536000000],
    ['month', 2592000000],
    ['day', 86400000],
    ['hour', 3600000],
    ['minute', 60000],
    ['second', 1000],
  ];
  for (const [unit, ms] of units) {
    if (abs >= ms || unit === 'second') {
      return rtf.format(Math.round(diffMs / ms), unit);
    }
  }
  return rtf.format(0, 'second');
}

export function formatDuration(ms, locale) {
  if (ms === null || ms === undefined || Number.isNaN(ms)) return '—';
  if (ms < 1000) return `${formatNumber(Math.round(ms), locale)} ms`;
  if (ms < 60000) return `${formatNumber(ms / 1000, locale, { maximumFractionDigits: 2 })} s`;
  return `${formatNumber(ms / 60000, locale, { maximumFractionDigits: 2 })} min`;
}

export function formatBytes(bytes, locale) {
  if (bytes === null || bytes === undefined || Number.isNaN(bytes)) return '—';
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exp = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  const value = bytes / Math.pow(1024, exp);
  return `${formatNumber(value, locale, { maximumFractionDigits: exp === 0 ? 0 : 2 })} ${units[exp]}`;
}

export function formatList(items, locale, type = 'conjunction') {
  if (!items || items.length === 0) return '';
  return new Intl.ListFormat(activeLocale(locale), { style: 'long', type }).format(
    items.map(String)
  );
}
