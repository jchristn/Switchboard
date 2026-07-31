import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { LOCALES } from '../../i18n/localeRegistry';
import { setActiveLocale } from '../../i18n';

// Locale picker shown in the topbar and on the login screen. Autonyms are always displayed so a
// speaker of the target language recognizes their own language regardless of the current UI locale.
export function LanguageSelector({ compact = false }) {
  const { t, i18n } = useTranslation();
  return (
    <label
      className={`language-selector${compact ? ' is-compact' : ''}`}
      title={t('topbar.languageTip')}
    >
      <span className="sr-only">{t('topbar.language')}</span>
      <select
        aria-label={t('topbar.language')}
        title={t('topbar.languageTip')}
        value={i18n.language?.split('-')[0] || 'en'}
        onChange={(e) => setActiveLocale(e.target.value)}
      >
        {LOCALES.map((l) => (
          <option key={l.code} value={l.code}>
            {l.native}
          </option>
        ))}
      </select>
    </label>
  );
}

LanguageSelector.propTypes = {
  compact: PropTypes.bool,
};

export default LanguageSelector;
