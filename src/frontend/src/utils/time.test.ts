import { describe, it, expect, afterEach } from 'vitest';
import { setActiveLocale } from '../i18n/active';
import { formatAbsolute, formatDuration, formatRelative, formatStepDuration } from './time';

afterEach(() => setActiveLocale('en'));

describe('formatDuration', () => {
  it('formats sub-minute as m:ss', () => {
    expect(formatDuration(0)).toBe('0:00');
    expect(formatDuration(5_000)).toBe('0:05');
    expect(formatDuration(42_000)).toBe('0:42');
  });

  it('formats minutes as m:ss', () => {
    expect(formatDuration(60_000)).toBe('1:00');
    expect(formatDuration(125_000)).toBe('2:05');
  });

  it('formats past an hour as h:mm:ss', () => {
    expect(formatDuration(3_600_000)).toBe('1:00:00');
    expect(formatDuration(3_725_000)).toBe('1:02:05');
  });

  it('clamps invalid / negative input to 0:00', () => {
    expect(formatDuration(-1_000)).toBe('0:00');
    expect(formatDuration(NaN)).toBe('0:00');
  });
});

describe('formatStepDuration', () => {
  it('formats sub-second as ms', () => {
    expect(formatStepDuration(340)).toBe('340ms');
    expect(formatStepDuration(999)).toBe('999ms');
  });

  it('formats sub-minute as one decimal of seconds', () => {
    expect(formatStepDuration(1_000)).toBe('1.0s');
    expect(formatStepDuration(12_345)).toBe('12.3s');
  });

  it('formats a minute or more as Xm SSs', () => {
    expect(formatStepDuration(64_000)).toBe('1m 04s');
    expect(formatStepDuration(125_000)).toBe('2m 05s');
  });

  it('returns empty string for invalid / negative input', () => {
    expect(formatStepDuration(-1)).toBe('');
    expect(formatStepDuration(NaN)).toBe('');
  });
});

describe('locale-aware formatting', () => {
  it('uses the Danish decimal separator for step durations', () => {
    setActiveLocale('da');
    expect(formatStepDuration(12_345)).toBe('12,3s');
    expect(formatStepDuration(340)).toBe('340ms');
  });

  it('renders relative time in the active language', () => {
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60_000).toISOString();
    setActiveLocale('en');
    expect(formatRelative(fiveMinutesAgo)).toBe('5 minutes ago');
    setActiveLocale('da');
    expect(formatRelative(fiveMinutesAgo)).toBe('for 5 minutter siden');
  });

  it('renders "just now" from the catalog', () => {
    const now = new Date().toISOString();
    setActiveLocale('en');
    expect(formatRelative(now)).toBe('just now');
    setActiveLocale('da');
    expect(formatRelative(now)).toBe('lige nu');
  });

  it('falls back to the em dash for a missing timestamp', () => {
    expect(formatRelative(undefined)).toBe('—');
    expect(formatAbsolute(undefined)).toBe('—');
  });

  it('returns the raw value for an unparseable timestamp', () => {
    expect(formatRelative('not-a-date')).toBe('not-a-date');
    expect(formatAbsolute('not-a-date')).toBe('not-a-date');
  });
});
