import { describe, it, expect } from 'vitest';
import { TIME_RANGES, rangeWindow, generateBuckets, mergeBuckets } from './ActivityChart';

// 2026-09-29T14:52:37.123Z: deliberately not on any bucket boundary.
const NOW = Date.UTC(2026, 8, 29, 14, 52, 37, 123);

describe('rangeWindow', () => {
  it('starts on the first chart bucket and ends at now, for every range', () => {
    for (const id of Object.keys(TIME_RANGES)) {
      const win = rangeWindow(id, NOW);
      const skeleton = generateBuckets(id, NOW);
      expect(win.startMs).toBe(skeleton[0].startMs);
      expect(win.endMs).toBe(NOW);
      expect(win.startMs % TIME_RANGES[id].bucketMs).toBe(0);
      expect(win.intervalMinutes).toBe(TIME_RANGES[id].bucketMs / 60000);
      expect(Date.parse(win.start)).toBe(win.startMs);
      expect(Date.parse(win.end)).toBe(NOW);
    }
  });

  it('maps server buckets built on the window 1:1 onto the chart with nothing dropped', () => {
    for (const id of Object.keys(TIME_RANGES)) {
      const { bucketMs, buckets } = TIME_RANGES[id];
      const win = rangeWindow(id, NOW);
      // What the server returns: buckets of intervalMinutes starting at win.start.
      const server = Array.from({ length: buckets }, (_, i) => ({
        bucketStartUtc: new Date(win.startMs + i * bucketMs).toISOString(),
        total: i + 1,
        success: i,
        failure: 1,
      }));
      const merged = mergeBuckets(generateBuckets(id, NOW), server);
      const drawn = merged.reduce((sum, b) => sum + b.total, 0);
      const sent = server.reduce((sum, b) => sum + b.total, 0);
      expect(drawn).toBe(sent);
      merged.forEach((b, i) => expect(b.total).toBe(i + 1));
    }
  });

  it('an unaligned "now minus the window" start would shift or drop buckets (the old Overview bug)', () => {
    const { bucketMs, buckets, windowMs } = TIME_RANGES.hour;
    const unalignedStart = NOW - windowMs;
    const server = Array.from({ length: buckets }, (_, i) => ({
      bucketStartUtc: new Date(unalignedStart + i * bucketMs).toISOString(),
      total: 1,
      success: 1,
      failure: 0,
    }));
    const merged = mergeBuckets(generateBuckets('hour', NOW), server);
    expect(merged.reduce((sum, b) => sum + b.total, 0)).toBeLessThan(buckets);
  });
});
