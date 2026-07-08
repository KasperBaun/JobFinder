import { describe, it, expect } from 'vitest';
import { formatDuration, formatStepDuration } from './time';

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
