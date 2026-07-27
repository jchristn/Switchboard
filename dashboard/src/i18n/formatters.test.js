import { describe, it, expect } from 'vitest';
import { formatNumber, formatPercent, formatBytes, formatDuration } from './formatters';

// Explicit locales so results are deterministic regardless of the active i18n language.
describe('formatters', () => {
  it('formats numbers per locale', () => {
    expect(formatNumber(1234.5, 'en')).toBe('1,234.5');
    expect(formatNumber(1234.5, 'de')).toBe('1.234,5');
  });

  it('formats percent (input is 0-100)', () => {
    expect(formatPercent(99.5, 'en')).toContain('99.5');
    expect(formatPercent(100, 'en')).toContain('100');
  });

  it('formats bytes', () => {
    expect(formatBytes(0, 'en')).toBe('0 B');
    expect(formatBytes(1536, 'en')).toBe('1.5 KB');
  });

  it('formats durations', () => {
    expect(formatDuration(500, 'en')).toContain('ms');
    expect(formatDuration(1500, 'en')).toContain('s');
    expect(formatDuration(null, 'en')).toBe('—');
  });
});
