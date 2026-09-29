import { describe, it, expect } from 'vitest';
import { parseStatusFilter, NON_2XX_FILTER } from './statusFilter';

const codes = [100, 200, 201, 204, 301, 304, 400, 401, 403, 404, 422, 429, 500, 502, 503];
const run = (expr) => codes.filter((c) => parseStatusFilter(expr).matches(c));

describe('parseStatusFilter', () => {
  it('matches everything when empty', () => {
    expect(run('')).toEqual(codes);
    expect(run('   ')).toEqual(codes);
    expect(parseStatusFilter(undefined).matches(500)).toBe(true);
  });

  it('matches an exact code', () => {
    expect(run('200')).toEqual([200]);
  });

  it('matches a list of codes', () => {
    expect(run('200,201')).toEqual([200, 201]);
    expect(run(' 404 , 500 ')).toEqual([404, 500]);
  });

  it('combines comparisons into one range', () => {
    expect(run('>=200,<=299')).toEqual([200, 201, 204]);
    expect(run('>400,<500')).toEqual([401, 403, 404, 422, 429]);
    expect(run('>=500')).toEqual([500, 502, 503]);
  });

  it('mixes codes and ranges', () => {
    expect(run('200,400,401,403-429')).toEqual([200, 400, 401, 403, 404, 422, 429]);
    expect(run('429-403')).toEqual([403, 404, 422, 429]);
  });

  it('matches status classes', () => {
    expect(run('5xx')).toEqual([500, 502, 503]);
    expect(run('2XX,3xx')).toEqual([200, 201, 204, 301, 304]);
  });

  it('mixes codes with a comparison range', () => {
    expect(run('404,>=500')).toEqual([404, 500, 502, 503]);
  });

  it('supports exclusions', () => {
    expect(run(NON_2XX_FILTER)).toEqual([100, 301, 304, 400, 401, 403, 404, 422, 429, 500, 502, 503]);
    expect(run('4xx,!404')).toEqual([400, 401, 403, 422, 429]);
    expect(run('!200-399,!5xx')).toEqual([100, 400, 401, 403, 404, 422, 429]);
  });

  it('reports the first invalid term', () => {
    for (const bad of ['abc', '20a', '200-', '>=', '6xx', '!foo', '1000', '200,,x']) {
      const r = parseStatusFilter(bad);
      expect(r.valid).toBe(false);
      expect(r.invalidTerm).toBeTruthy();
      expect(r.matches(500)).toBe(true);
    }
  });

  it('does not match missing or non-numeric codes when a filter is set', () => {
    expect(parseStatusFilter('200').matches(null)).toBe(false);
    expect(parseStatusFilter('!2xx').matches(undefined)).toBe(false);
  });
});
