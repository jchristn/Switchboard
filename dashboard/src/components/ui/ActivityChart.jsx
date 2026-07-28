import { useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Segmented from './Segmented';
import { Refresh } from './Icons';
import { formatNumber, formatDateTime } from '../../i18n/formatters';
import './ActivityChart.css';

export const TIME_RANGES = {
  hour: { labelKey: 'chart.rangeHour', windowMs: 3600000, bucketMs: 60000, buckets: 60 },
  day: { labelKey: 'chart.rangeDay', windowMs: 86400000, bucketMs: 900000, buckets: 96 },
  week: { labelKey: 'chart.rangeWeek', windowMs: 604800000, bucketMs: 7200000, buckets: 84 },
  month: { labelKey: 'chart.rangeMonth', windowMs: 2592000000, bucketMs: 21600000, buckets: 120 },
};

// A zero-filled skeleton of buckets aligned to the range's grid, ending at nowMs.
export function generateBuckets(rangeId, nowMs = Date.now()) {
  const range = TIME_RANGES[rangeId] || TIME_RANGES.hour;
  const { bucketMs, buckets } = range;
  const endStart = Math.floor(nowMs / bucketMs) * bucketMs;
  const startStart = endStart - (buckets - 1) * bucketMs;
  const out = [];
  for (let i = 0; i < buckets; i += 1) {
    const startMs = startStart + i * bucketMs;
    out.push({
      bucketStartUtc: new Date(startMs).toISOString(),
      startMs,
      total: 0,
      success: 0,
      failure: 0,
    });
  }
  return out;
}

// Overlay server buckets onto a skeleton by flooring each server bucket's start to
// the skeleton's grid and accumulating counts.
export function mergeBuckets(skeleton, serverBuckets) {
  if (!Array.isArray(skeleton) || skeleton.length === 0) return skeleton || [];
  const list = Array.isArray(serverBuckets) ? serverBuckets : [];
  const firstMs = Date.parse(skeleton[0].bucketStartUtc);
  const bucketMs =
    skeleton.length >= 2 ? Date.parse(skeleton[1].bucketStartUtc) - firstMs : 60000;

  const merged = skeleton.map((b) => ({
    ...b,
    startMs: b.startMs != null ? b.startMs : Date.parse(b.bucketStartUtc),
    total: b.total || 0,
    success: b.success || 0,
    failure: b.failure || 0,
  }));

  const indexByMs = new Map();
  merged.forEach((b, i) => indexByMs.set(b.startMs, i));

  for (const sb of list) {
    if (!sb || sb.bucketStartUtc == null) continue;
    const raw = Date.parse(sb.bucketStartUtc);
    if (Number.isNaN(raw)) continue;
    const floored = Math.floor(raw / bucketMs) * bucketMs;
    const idx = indexByMs.get(floored);
    if (idx == null) continue;
    const success = sb.success != null ? sb.success : (sb.total || 0) - (sb.failure || 0);
    const failure = sb.failure != null ? sb.failure : 0;
    const total = sb.total != null ? sb.total : success + failure;
    merged[idx].success += Math.max(0, success);
    merged[idx].failure += Math.max(0, failure);
    merged[idx].total += Math.max(0, total);
  }

  return merged;
}

const VIEW_W = 1000;
const VIEW_H = 220;
const PAD_LEFT = 44;
const PAD_RIGHT = 12;
const PAD_TOP = 12;
const PAD_BOTTOM = 28;
const PLOT_W = VIEW_W - PAD_LEFT - PAD_RIGHT;
const PLOT_H = VIEW_H - PAD_TOP - PAD_BOTTOM;
const Y_GRID_LINES = 4;

function labelFormatFor(rangeId) {
  if (rangeId === 'hour') return { hour: '2-digit', minute: '2-digit' };
  if (rangeId === 'day') return { hour: '2-digit', minute: '2-digit' };
  return { month: 'short', day: 'numeric' };
}

// Hand-rolled stacked success/failure bar chart (no charting library).
export default function ActivityChart({
  data,
  rangeId = 'hour',
  onRangeChange,
  onBucketClick,
  onRefresh,
  loading = false,
  autoRefresh = false,
  onToggleAutoRefresh,
  title,
  nowMs,
}) {
  const { t } = useTranslation();
  const [tooltip, setTooltip] = useState(null);
  const svgRef = useRef(null);

  const buckets = useMemo(() => {
    const serverBuckets = Array.isArray(data)
      ? data
      : (data && (data.buckets || data.series)) || [];
    const skeleton = generateBuckets(rangeId, nowMs || Date.now());
    return mergeBuckets(skeleton, serverBuckets);
  }, [data, rangeId, nowMs]);

  const maxTotal = useMemo(
    () => Math.max(1, ...buckets.map((b) => b.total || 0)),
    [buckets]
  );

  // Whole-number Y-axis ticks. With a fractional step an empty chart rendered 0,0,1,1,1; a "nice"
  // integer step keeps labels distinct (e.g. an empty chart shows just 0 and 1).
  const yTicks = useMemo(() => {
    const raw = maxTotal / Math.max(1, Y_GRID_LINES);
    let step;
    if (raw <= 1) {
      step = 1;
    } else {
      const pow = Math.pow(10, Math.floor(Math.log10(raw)));
      const norm = raw / pow;
      const nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
      step = Math.max(1, Math.round(nice * pow));
    }
    const ticks = [];
    for (let v = 0; v <= maxTotal + 1e-9; v += step) ticks.push(Math.round(v));
    const uniq = [...new Set(ticks)];
    if (uniq[uniq.length - 1] < maxTotal) uniq.push(Math.round(maxTotal));
    return uniq;
  }, [maxTotal]);

  const allZero = useMemo(() => buckets.every((b) => (b.total || 0) === 0), [buckets]);

  const rangeOptions = Object.entries(TIME_RANGES).map(([id, cfg]) => ({
    value: id,
    label: t(cfg.labelKey),
  }));

  const barSlot = PLOT_W / buckets.length;
  const barWidth = Math.max(1, barSlot * 0.7);
  const labelEvery = Math.max(1, Math.ceil(buckets.length / 8));

  const yFor = (value) => PAD_TOP + PLOT_H - (value / maxTotal) * PLOT_H;

  const showTooltip = (e, bucket) => {
    setTooltip({
      x: e.clientX,
      y: e.clientY,
      bucket,
    });
  };

  const labelFmt = labelFormatFor(rangeId);

  return (
    <div className="sb-chart">
      <div className="sb-chart__header">
        <div className="sb-chart__title">{title || t('chart.title')}</div>
        <div className="sb-chart__controls">
          <Segmented
            size="sm"
            options={rangeOptions}
            value={rangeId}
            onChange={onRangeChange}
            ariaLabel={t('chart.title')}
          />
          {onToggleAutoRefresh && (
            <button
              type="button"
              className={`sb-chart__toggle ${autoRefresh ? 'sb-chart__toggle--on' : ''}`.trim()}
              role="switch"
              aria-checked={autoRefresh}
              onClick={() => onToggleAutoRefresh(!autoRefresh)}
              title={t('chart.autoRefresh')}
            >
              <span className="sb-chart__toggle-track" aria-hidden="true">
                <span className="sb-chart__toggle-thumb" />
              </span>
              <span className="sb-chart__toggle-label">{t('chart.autoRefresh')}</span>
            </button>
          )}
          {onRefresh && (
            <button
              type="button"
              className={`sb-chart__refresh ${loading ? 'sb-chart__refresh--spin' : ''}`.trim()}
              onClick={onRefresh}
              aria-label={t('common.refresh')}
              title={t('common.refresh')}
              disabled={loading}
            >
              <Refresh size={16} />
            </button>
          )}
        </div>
      </div>

      <div className="sb-chart__plot">
        <svg
          ref={svgRef}
          viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
          width="100%"
          className="sb-chart__svg"
          role="img"
          aria-label={title || t('chart.title')}
        >
          {/* y grid lines + labels */}
          {yTicks.map((value) => {
            const y = yFor(value);
            return (
              <g key={`grid-${value}`}>
                <line
                  x1={PAD_LEFT}
                  x2={VIEW_W - PAD_RIGHT}
                  y1={y}
                  y2={y}
                  stroke="var(--color-chart-grid)"
                  strokeWidth="1"
                />
                <text
                  x={PAD_LEFT - 6}
                  y={y + 3}
                  textAnchor="end"
                  className="sb-chart__axis-text"
                >
                  {formatNumber(Math.round(value))}
                </text>
              </g>
            );
          })}

          {/* bars */}
          {buckets.map((b, i) => {
            const x = PAD_LEFT + i * barSlot + (barSlot - barWidth) / 2;
            const failure = b.failure || 0;
            const success = b.success || 0;
            const failH = (failure / maxTotal) * PLOT_H;
            const succH = (success / maxTotal) * PLOT_H;
            const baseY = PAD_TOP + PLOT_H;
            const failY = baseY - failH;
            const succY = failY - succH;
            const total = b.total || 0;
            return (
              <g
                key={b.bucketStartUtc}
                className="sb-chart__bar-group"
                onMouseEnter={(e) => showTooltip(e, b)}
                onMouseMove={(e) => showTooltip(e, b)}
                onMouseLeave={() => setTooltip(null)}
                onClick={() => onBucketClick?.(b)}
                style={{ cursor: onBucketClick ? 'pointer' : 'default' }}
              >
                {/* invisible hit area covering the full column height */}
                <rect
                  x={PAD_LEFT + i * barSlot}
                  y={PAD_TOP}
                  width={barSlot}
                  height={PLOT_H}
                  fill="transparent"
                />
                {failure > 0 && (
                  <rect
                    x={x}
                    y={failY}
                    width={barWidth}
                    height={Math.max(0, failH)}
                    fill="var(--color-danger)"
                    rx="1"
                  />
                )}
                {success > 0 && (
                  <rect
                    x={x}
                    y={succY}
                    width={barWidth}
                    height={Math.max(0, succH)}
                    fill="var(--color-success)"
                    rx="1"
                  />
                )}
                {total === 0 && (
                  <rect
                    x={x}
                    y={baseY - 1}
                    width={barWidth}
                    height={1}
                    fill="var(--color-chart-grid)"
                  />
                )}
              </g>
            );
          })}

          {/* x axis labels (thinned) */}
          {buckets.map((b, i) => {
            if (i % labelEvery !== 0) return null;
            const x = PAD_LEFT + i * barSlot + barSlot / 2;
            return (
              <text
                key={`xlabel-${b.bucketStartUtc}`}
                x={x}
                y={VIEW_H - 8}
                textAnchor="middle"
                className="sb-chart__axis-text"
              >
                {formatDateTime(b.bucketStartUtc, undefined, labelFmt)}
              </text>
            );
          })}
        </svg>

        {allZero && !loading && (
          <div className="sb-chart__nodata">{t('chart.noData')}</div>
        )}
      </div>

      <div className="sb-chart__legend">
        <span className="sb-chart__legend-item">
          <span className="sb-chart__swatch" style={{ background: 'var(--color-success)' }} aria-hidden="true" />
          {t('chart.success')}
        </span>
        <span className="sb-chart__legend-item">
          <span className="sb-chart__swatch" style={{ background: 'var(--color-danger)' }} aria-hidden="true" />
          {t('chart.failure')}
        </span>
      </div>

      {tooltip &&
        createPortal(
          <div
            className="sb-chart__tooltip"
            style={{ top: tooltip.y + 14, left: tooltip.x + 14 }}
            role="tooltip"
          >
            {t('chart.bucketTooltip', {
              time: formatDateTime(tooltip.bucket.bucketStartUtc),
              total: formatNumber(tooltip.bucket.total || 0),
              failure: formatNumber(tooltip.bucket.failure || 0),
            })}
          </div>,
          document.body
        )}
    </div>
  );
}

ActivityChart.propTypes = {
  data: PropTypes.oneOfType([PropTypes.array, PropTypes.object]),
  rangeId: PropTypes.oneOf(Object.keys(TIME_RANGES)),
  onRangeChange: PropTypes.func,
  onBucketClick: PropTypes.func,
  onRefresh: PropTypes.func,
  loading: PropTypes.bool,
  autoRefresh: PropTypes.bool,
  onToggleAutoRefresh: PropTypes.func,
  title: PropTypes.node,
  nowMs: PropTypes.number,
};
