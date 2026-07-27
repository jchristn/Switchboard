import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { useOnboarding } from '../../context/OnboardingContext';
import './Tour.css';

// Ordered coach-marks. Each targets a `data-tour` attribute the Sidebar renders on
// its nav items. Steps whose target is absent (e.g. admin-only routes) are skipped.
const TOUR_STEPS = [
  { selector: '[data-tour="nav-Gauge"]', titleKey: 'tour.overviewTitle', bodyKey: 'tour.overviewBody' },
  { selector: '[data-tour="nav-Server"]', titleKey: 'tour.originsTitle', bodyKey: 'tour.originsBody' },
  { selector: '[data-tour="nav-Route"]', titleKey: 'tour.endpointsTitle', bodyKey: 'tour.endpointsBody' },
  { selector: '[data-tour="nav-History"]', titleKey: 'tour.historyTitle', bodyKey: 'tour.historyBody' },
  { selector: '[data-tour="nav-Settings"]', titleKey: 'tour.settingsTitle', bodyKey: 'tour.settingsBody' },
  { selector: '[data-tour="nav-Terminal"]', titleKey: 'tour.apiExplorerTitle', bodyKey: 'tour.apiExplorerBody' },
];

const TOOLTIP_WIDTH = 300;
const GAP = 14;
const MARGIN = 8;

// Spotlight product tour. Renders nothing unless the onboarding context has the tour
// active. Portals a darkening spotlight + tooltip to the body, positioned against the
// current step's target element.
export default function Tour() {
  const { t } = useTranslation();
  const { tourActive, endTour } = useOnboarding();

  // Resolve which steps actually have a target present when the tour opens.
  const steps = useMemo(() => {
    if (!tourActive) return [];
    return TOUR_STEPS.filter((s) => document.querySelector(s.selector));
  }, [tourActive]);

  const [index, setIndex] = useState(0);
  const [rect, setRect] = useState(null);
  const tooltipRef = useRef(null);
  const [tooltipSize, setTooltipSize] = useState({ width: TOOLTIP_WIDTH, height: 160 });

  // Reset to the first step each time the tour opens.
  useEffect(() => {
    if (tourActive) setIndex(0);
  }, [tourActive]);

  const finish = useCallback(() => endTour(true), [endTour]);

  const step = steps[index];

  // Track the target element's viewport rect (updates on scroll/resize).
  useEffect(() => {
    if (!tourActive || !step) return undefined;
    const el = document.querySelector(step.selector);
    if (!el) {
      setRect(null);
      return undefined;
    }
    const update = () => setRect(el.getBoundingClientRect());
    update();
    try {
      el.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    } catch {
      /* scrollIntoView options unsupported — ignore */
    }
    window.addEventListener('resize', update);
    window.addEventListener('scroll', update, true);
    return () => {
      window.removeEventListener('resize', update);
      window.removeEventListener('scroll', update, true);
    };
  }, [tourActive, step]);

  // Measure the tooltip so it can be kept inside the viewport.
  useLayoutEffect(() => {
    if (tooltipRef.current) {
      const r = tooltipRef.current.getBoundingClientRect();
      setTooltipSize({ width: r.width, height: r.height });
    }
  }, [index, rect]);

  // Close on Escape.
  useEffect(() => {
    if (!tourActive) return undefined;
    const onKey = (e) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        endTour(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [tourActive, endTour]);

  if (!tourActive || steps.length === 0 || !step || !rect) return null;

  const isFirst = index === 0;
  const isLast = index === steps.length - 1;
  const rtl = typeof document !== 'undefined' && document.documentElement.dir === 'rtl';

  // Prefer placing the tooltip on the inline-end side of the target; flip when it
  // would overflow the viewport.
  const spaceEnd = window.innerWidth - rect.right;
  const spaceStart = rect.left;
  const preferEnd = rtl ? spaceStart < spaceEnd : true;
  let left;
  if (preferEnd && (rtl ? spaceStart : spaceEnd) >= tooltipSize.width + GAP + MARGIN) {
    left = rtl ? rect.left - tooltipSize.width - GAP : rect.right + GAP;
  } else if ((rtl ? spaceEnd : spaceStart) >= tooltipSize.width + GAP + MARGIN) {
    left = rtl ? rect.right + GAP : rect.left - tooltipSize.width - GAP;
  } else {
    // Not enough room on either side — sit below the target.
    left = Math.min(Math.max(MARGIN, rect.left), window.innerWidth - tooltipSize.width - MARGIN);
  }
  left = Math.min(Math.max(MARGIN, left), window.innerWidth - tooltipSize.width - MARGIN);

  let top = rect.top;
  const belowFits = top + tooltipSize.height + MARGIN <= window.innerHeight;
  if (!belowFits) top = window.innerHeight - tooltipSize.height - MARGIN;
  top = Math.max(MARGIN, top);

  const ringStyle = {
    top: rect.top - 4,
    left: rect.left - 4,
    width: rect.width + 8,
    height: rect.height + 8,
  };
  const tooltipStyle = { top, left, width: TOOLTIP_WIDTH };

  return createPortal(
    <div className="tour-root" role="dialog" aria-modal="true" aria-label={t(step.titleKey)}>
      <div className="tour-spotlight" style={ringStyle} aria-hidden="true" />
      <div className="tour-tooltip" style={tooltipStyle} ref={tooltipRef}>
        <div className="tour-tooltip__header">
          <h3 className="tour-tooltip__title">{t(step.titleKey)}</h3>
          <span className="tour-tooltip__count">{t('tour.step', { current: index + 1, total: steps.length })}</span>
        </div>
        <p className="tour-tooltip__body">{t(step.bodyKey)}</p>
        <div className="tour-tooltip__actions">
          <button type="button" className="sb-btn sb-btn--ghost tour-skip" onClick={finish}>
            {t('common.skip')}
          </button>
          <button
            type="button"
            className="sb-btn sb-btn--ghost"
            onClick={() => setIndex((i) => Math.max(0, i - 1))}
            disabled={isFirst}
          >
            {t('common.back')}
          </button>
          {isLast ? (
            <button type="button" className="sb-btn sb-btn--primary" onClick={finish}>
              {t('tour.done')}
            </button>
          ) : (
            <button
              type="button"
              className="sb-btn sb-btn--primary"
              onClick={() => setIndex((i) => Math.min(steps.length - 1, i + 1))}
            >
              {t('common.next')}
            </button>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
