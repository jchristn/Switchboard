import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { Copy, Check } from './Icons';
import './CopyButton.css';

// Copies text to the clipboard. Prefers the async clipboard API and falls back to
// a hidden textarea + execCommand('copy') for non-secure contexts (http, file://).
async function copyText(value) {
  const text = String(value ?? '');
  if (navigator.clipboard && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      /* fall through to legacy path */
    }
  }
  try {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.position = 'fixed';
    ta.style.insetInlineStart = '-9999px';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(ta);
    return ok;
  } catch {
    return false;
  }
}

export default function CopyButton({ value, size = 16, className = '', title, ...props }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const timerRef = useRef(null);

  useEffect(() => () => clearTimeout(timerRef.current), []);

  const handleClick = useCallback(
    async (e) => {
      e.stopPropagation();
      const ok = await copyText(value);
      if (ok) {
        setCopied(true);
        clearTimeout(timerRef.current);
        timerRef.current = setTimeout(() => setCopied(false), 1200);
      }
    },
    [value]
  );

  const label = copied ? t('common.copied') : title || t('common.copy');

  return (
    <button
      type="button"
      className={`sb-copy-btn ${copied ? 'sb-copy-btn--copied' : ''} ${className}`.trim()}
      onClick={handleClick}
      aria-label={label}
      title={label}
      data-row-click-ignore=""
      {...props}
    >
      {copied ? <Check size={size} /> : <Copy size={size} />}
    </button>
  );
}

CopyButton.propTypes = {
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  size: PropTypes.number,
  className: PropTypes.string,
  title: PropTypes.string,
};
