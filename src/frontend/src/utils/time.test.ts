import { describe, it, expect } from 'vitest';
import { formatDuration } from './time';

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
