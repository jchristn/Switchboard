// Central registry of the locales Switchboard ships. Adding a locale is a matter of adding an entry
// here and a catalog under ./locales/<code>/ — no application logic changes. Direction metadata drives
// the document `dir` attribute so RTL locales lay out correctly.

export const STORAGE_KEY = 'switchboard_locale';
export const DEFAULT_LOCALE = 'en';
export const FALLBACK_LOCALE = 'en';

export const LOCALES = [
  { code: 'en', dir: 'ltr', native: 'English', english: 'English' },
  { code: 'es', dir: 'ltr', native: 'Español', english: 'Spanish' },
  { code: 'de', dir: 'ltr', native: 'Deutsch', english: 'German' },
  { code: 'fr', dir: 'ltr', native: 'Français', english: 'French' },
  { code: 'pt', dir: 'ltr', native: 'Português', english: 'Portuguese' },
  { code: 'zh', dir: 'ltr', native: '中文（普通话）', english: 'Mandarin Chinese' },
  { code: 'yue', dir: 'ltr', native: '中文（廣東話）', english: 'Cantonese Chinese' },
  { code: 'ja', dir: 'ltr', native: '日本語', english: 'Japanese' },
  { code: 'fa', dir: 'rtl', native: 'فارسی', english: 'Persian' },
];

export const SUPPORTED_CODES = LOCALES.map((l) => l.code);

export function localeMeta(code) {
  return LOCALES.find((l) => l.code === code) || LOCALES[0];
}

export function directionFor(code) {
  return localeMeta(code).dir;
}
