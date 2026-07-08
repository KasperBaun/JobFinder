import { describe, it, expect } from 'vitest';
import type { JobSearch, JobSearchEvent } from '../api/types';
import { phaseDurations } from './progress';

function ev(phase: JobSearch['phase'], iso: string): JobSearchEvent {
  return { timestamp: iso, level: 'info', phase, message: phase };
}

describe('phaseDurations', () => {
  it('spans each phase to the start of the next, closing the last with finishedAt', () => {
    const timeline = [
      ev('fetching', '2026-07-08T10:00:00.000Z'),
      ev('deduping', '2026-07-08T10:00:03.000Z'),
      ev('ranking', '2026-07-08T10:00:04.000Z'),
      ev('llmJudging', '2026-07-08T10:00:16.000Z'),
      ev('done', '2026-07-08T10:00:20.000Z'),
    ];
    const d = phaseDurations(timeline, '2026-07-08T10:00:21.000Z');
    expect(d.fetching).toBe(3_000);
    expect(d.deduping).toBe(1_000);
    expect(d.ranking).toBe(12_000);
    expect(d.llmJudging).toBe(4_000);
    expect(d.done).toBe(1_000);
  });

  it('uses the first timestamp per phase when a phase emits many events', () => {
    const timeline = [
      ev('fetching', '2026-07-08T10:00:00.000Z'),
      ev('fetching', '2026-07-08T10:00:01.000Z'),
      ev('fetching', '2026-07-08T10:00:02.000Z'),
      ev('deduping', '2026-07-08T10:00:05.000Z'),
    ];
    const d = phaseDurations(timeline, '2026-07-08T10:00:06.000Z');
    expect(d.fetching).toBe(5_000);
    expect(d.deduping).toBe(1_000);
  });

  it('handles AI disabled (no llmJudging phase)', () => {
    const timeline = [
      ev('fetching', '2026-07-08T10:00:00.000Z'),
      ev('deduping', '2026-07-08T10:00:03.000Z'),
      ev('ranking', '2026-07-08T10:00:04.000Z'),
    ];
    const d = phaseDurations(timeline, '2026-07-08T10:00:10.000Z');
    expect(d.llmJudging).toBeUndefined();
    expect(d.ranking).toBe(6_000);
  });

  it('leaves the last phase unmeasured when finishedAt is absent', () => {
    const timeline = [
      ev('fetching', '2026-07-08T10:00:00.000Z'),
      ev('deduping', '2026-07-08T10:00:03.000Z'),
    ];
    const d = phaseDurations(timeline);
    expect(d.fetching).toBe(3_000);
    expect(d.deduping).toBeUndefined();
  });

  it('ignores pending events and unparseable timestamps', () => {
    const timeline = [
      ev('pending', '2026-07-08T09:59:00.000Z'),
      ev('fetching', 'not-a-date'),
      ev('deduping', '2026-07-08T10:00:03.000Z'),
    ];
    const d = phaseDurations(timeline, '2026-07-08T10:00:05.000Z');
    expect(d.pending).toBeUndefined();
    expect(d.fetching).toBeUndefined();
    expect(d.deduping).toBe(2_000);
  });

  it('returns an empty map for an empty timeline', () => {
    expect(phaseDurations([])).toEqual({});
  });
});
