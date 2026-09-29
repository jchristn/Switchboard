import { describe, it, expect } from 'vitest';
import { summarizeBuckets } from './timeseries';

describe('summarizeBuckets', () => {
  it('sums counts and computes a 0-100 success rate', () => {
    const s = summarizeBuckets([
      { total: 10, success: 8, failure: 2, avgDurationMs: 100 },
      { total: 0, success: 0, failure: 0, avgDurationMs: 0 },
      { total: 30, success: 27, failure: 3, avgDurationMs: 20 },
    ]);
    expect(s.total).toBe(40);
    expect(s.success).toBe(35);
    expect(s.failure).toBe(5);
    expect(s.successRate).toBeCloseTo(87.5);
  });

  it('weights average duration by request count', () => {
    const s = summarizeBuckets([
      { total: 1, success: 1, failure: 0, avgDurationMs: 1000 },
      { total: 9, success: 9, failure: 0, avgDurationMs: 0 },
    ]);
    expect(s.avgDurationMs).toBeCloseTo(100);
  });

  it('reports no rate or duration for an empty window', () => {
    for (const input of [[], null, undefined, [{ total: 0, success: 0, failure: 0 }]]) {
      const s = summarizeBuckets(input);
      expect(s.total).toBe(0);
      expect(s.successRate).toBeNull();
      expect(s.avgDurationMs).toBeNull();
    }
  });

  it('matches a window with a single successful request (100%, no failures)', () => {
    const s = summarizeBuckets([{ total: 1, success: 1, failure: 0, avgDurationMs: 32 }]);
    expect(s.successRate).toBe(100);
    expect(s.failure).toBe(0);
    expect(s.avgDurationMs).toBe(32);
  });

  it('tolerates missing or non-numeric fields', () => {
    const s = summarizeBuckets([{ total: '4', success: '3', failure: null }, {}]);
    expect(s.total).toBe(4);
    expect(s.success).toBe(3);
    expect(s.failure).toBe(0);
  });
});
