import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import { resources } from './resources';
import {
  STORAGE_KEY,
  DEFAULT_LOCALE,
  FALLBACK_LOCALE,
  SUPPORTED_CODES,
  directionFor,
} from './localeRegistry';

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: FALLBACK_LOCALE,
    supportedLngs: [...SUPPORTED_CODES, 'cimode'],
    nonExplicitSupportedLngs: true,
    detection: {
      order: ['querystring', 'localStorage', 'navigator'],
      lookupQuerystring: 'lang',
      lookupLocalStorage: STORAGE_KEY,
      caches: ['localStorage'],
    },
    interpolation: { escapeValue: false },
    returnEmptyString: false,
  });

function applyDirection(lng) {
  const code = lng || DEFAULT_LOCALE;
  document.documentElement.setAttribute('lang', code);
  document.documentElement.setAttribute('dir', directionFor(code));
}

applyDirection(i18n.language);
i18n.on('languageChanged', applyDirection);

export function setActiveLocale(code) {
  return i18n.changeLanguage(code);
}

export default i18n;
