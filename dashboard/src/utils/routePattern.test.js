import { describe, it, expect } from 'vitest';
import { validateRoutePattern, isCatchAllPattern, routePatternError } from './routePattern';

describe('validateRoutePattern', () => {
  it('accepts literal, parameter, and trailing catch-all patterns', () => {
    expect(validateRoutePattern('/')).toBeNull();
    expect(validateRoutePattern('/api/users')).toBeNull();
    expect(validateRoutePattern('/api/users/{id}')).toBeNull();
    expect(validateRoutePattern('/api/{*rest}')).toBeNull();
    expect(validateRoutePattern('/{*path}')).toBeNull();
    expect(validateRoutePattern('/{v}/files/{*path}')).toBeNull();
    expect(validateRoutePattern('/api/{*rest}/')).toBeNull();
  });

  it('treats {*}, {}, *, and ** as literals', () => {
    expect(validateRoutePattern('/a/{*}/b')).toBeNull();
    expect(validateRoutePattern('/a/{}/b')).toBeNull();
    expect(validateRoutePattern('/a/*/b')).toBeNull();
    expect(validateRoutePattern('/a/**')).toBeNull();
  });

  it('rejects an empty pattern', () => {
    expect(validateRoutePattern('')).toEqual({ code: 'required', segment: '' });
    expect(validateRoutePattern('   ')).toEqual({ code: 'required', segment: '' });
    expect(validateRoutePattern(null)).toEqual({ code: 'required', segment: '' });
  });

  it('rejects a catch-all that is not the last segment', () => {
    expect(validateRoutePattern('/{*rest}/edit')).toEqual({ code: 'catchAllNotLast', segment: '{*rest}' });
    expect(validateRoutePattern('/api/{*rest}/x/{id}')).toEqual({ code: 'catchAllNotLast', segment: '{*rest}' });
  });

  it('rejects more than one catch-all', () => {
    expect(validateRoutePattern('/{*a}/{*b}')).toEqual({ code: 'catchAllMultiple', segment: '{*b}' });
  });

  it('rejects a catch-all inside a larger segment', () => {
    expect(validateRoutePattern('/files/v{*rest}')).toEqual({ code: 'catchAllPartial', segment: '{*rest}' });
    expect(validateRoutePattern('/files/{*rest}.txt')).toEqual({ code: 'catchAllPartial', segment: '{*rest}' });
  });
});

describe('isCatchAllPattern', () => {
  it('detects valid catch-all patterns only', () => {
    expect(isCatchAllPattern('/api/{*rest}')).toBe(true);
    expect(isCatchAllPattern('/{*path}')).toBe(true);
    expect(isCatchAllPattern('/api/{id}')).toBe(false);
    expect(isCatchAllPattern('/a/{*}')).toBe(false);
    expect(isCatchAllPattern('/{*rest}/edit')).toBe(false);
    expect(isCatchAllPattern('')).toBe(false);
  });
});

describe('routePatternError', () => {
  const t = (key, opts) => (opts ? `${key}:${opts.segment}` : key);

  it('returns null for a valid pattern', () => {
    expect(routePatternError(t, '/api/{*rest}')).toBeNull();
  });

  it('maps each failure to a translation key', () => {
    expect(routePatternError(t, '')).toBe('endpoints.urlPatternRequired');
    expect(routePatternError(t, '/{*a}/b')).toBe('routePattern.catchAllNotLast:{*a}');
    expect(routePatternError(t, '/{*a}/{*b}')).toBe('routePattern.catchAllMultiple:{*b}');
    expect(routePatternError(t, '/x{*a}')).toBe('routePattern.catchAllPartial:{*a}');
  });
});
