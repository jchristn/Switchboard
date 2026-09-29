// Summarize /history/timeseries buckets so KPI tiles can describe exactly the window the activity chart
// shows. Returns { total, success, failure, successRate, avgDurationMs } where successRate is 0-100 (the
// scale fmt.percent expects) and successRate/avgDurationMs are null when the window has no requests.
export function summarizeBuckets(buckets) {
  let total = 0;
  let success = 0;
  let failure = 0;
  let durationWeighted = 0;

  for (const b of Array.isArray(buckets) ? buckets : []) {
    const n = Number(b?.total) || 0;
    total += n;
    success += Number(b?.success) || 0;
    failure += Number(b?.failure) || 0;
    durationWeighted += (Number(b?.avgDurationMs) || 0) * n;
  }

  return {
    total,
    success,
    failure,
    successRate: total > 0 ? (success / total) * 100 : null,
    avgDurationMs: total > 0 ? durationWeighted / total : null,
  };
}
