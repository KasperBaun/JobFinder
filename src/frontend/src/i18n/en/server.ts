import { dec, n } from '../format'
import { list, num, str } from '../serverText'
import type { ServerArgs, ServerRenderer } from '../serverText'

// Keys mirror what the backend emits and are persisted in run history — additive only.
// Backend sources: Jobs/JobSearch.cs, Jobmatch.Api/Jobs/SearchJob.Events.cs,
// Ranking/Ranker.Notes.cs, Search/SearchService.Ranking.cs.
export const server = {
  remoteMode: {
    remote: 'remote',
    hybrid: 'hybrid',
    onsite: 'onsite',
    unknown: 'unknown',
  } as Record<string, string>,

  timeline: {
    queued: () => 'Search queued',
    started: () => 'Search started',
    resumed: (a: ServerArgs) => `Search resumed (attempt ${num(a, 'attempt')})`,
    complete: (a: ServerArgs) => `Search complete — ${num(a, 'count')} top jobs`,
    failed: (a: ServerArgs) => `Search failed: ${str(a, 'error')}`,
    cancelled: () => 'Search cancelled',

    fetchingFrom: (a: ServerArgs) => `Fetching listings from ${num(a, 'total')} sources`,
    providerRunning: (a: ServerArgs) => `Fetching ${str(a, 'provider')} (${num(a, 'index')}/${num(a, 'total')})`,
    providerDone: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} jobs · ${dec(num(a, 'seconds'), 0)}s`,
    providerDoneCapped: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} jobs · ${dec(num(a, 'seconds'), 0)}s · hit page cap, may be more`,
    providerDoneLimited: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} jobs · ${dec(num(a, 'seconds'), 0)}s · at configured limit, may be more`,
    providerFailed: (a: ServerArgs) =>
      `${str(a, 'provider')} failed: ${str(a, 'error')} · ${dec(num(a, 'seconds'), 0)}s`,

    deduped: (a: ServerArgs) => `${n(num(a, 'count'))} unique jobs after removing duplicates`,
    llmJudging: (a: ServerArgs) => `AI reviewing top ${num(a, 'count')} jobs…`,
    ranked: (a: ServerArgs) => `${n(num(a, 'count'))} jobs rated · best ${dec(num(a, 'topScore'), 2)}`,
    writing: () => 'Writing results',
  } satisfies Record<string, ServerRenderer>,

  reasoning: {
    disqualified: (a: ServerArgs) => `Removed because of: ${list(a, 'hits').join(', ')}.`,
    primaryHits: (a: ServerArgs) => `Must-have skill match: ${list(a, 'skills').join(', ')}.`,
    noPrimaryHits: () => 'None of your must-have skills mentioned.',
    secondaryHits: (a: ServerArgs) => `Also matches: ${list(a, 'skills').join(', ')}.`,
    domainHits: (a: ServerArgs) => `Industry: ${list(a, 'domains').join(', ')}.`,
    seniorityClose: () => 'Experience level close fit.',
    seniorityMatches: () => 'Experience level matches.',
    seniorityMismatch: () => 'Experience level doesn’t match.',
    seniorityUnknown: () => 'Experience level not stated.',
    titleNotDeveloper: () => 'Title doesn’t look like a developer role — rating reduced.',
    location: (a: ServerArgs) => `Location: ${str(a, 'location')}.`,
    remoteOk: (a: ServerArgs) => `Remote work OK (${str(a, 'mode')}).`,
    neitherLocationNorRemote: () => 'Neither location nor remote setup matches.',
    locationRemoteUnknown: () => 'Location and remote setup not stated.',
    locationMismatchRemoteUnknown: () => 'Location doesn’t match; remote setup not stated.',
    locationUnknownRemoteMismatch: () => 'Location not stated; remote setup doesn’t match.',
    agePenalty: (a: ServerArgs) => `Posted ${num(a, 'days')} days ago — rating reduced for age.`,
  } satisfies Record<string, ServerRenderer>,

  // Keyed by DropReason — the reason value is already the message key.
  dropContext: {
    above_max_age: (a: ServerArgs) => `posted ${num(a, 'days')} days ago, max ${num(a, 'maxAge')}`,
    missing_required_primary: () => 'no primary-stack keyword matched in title or description',
    disqualifier: (a: ServerArgs) => `matched disqualifier: ${list(a, 'hits').join(', ')}`,
    below_min_score: (a: ServerArgs) =>
      `score ${dec(num(a, 'score'), 2)} below threshold ${dec(num(a, 'threshold'), 2)}`,
    beyond_top_n: (a: ServerArgs) =>
      `rank ${num(a, 'rank')} of ${num(a, 'total')} (top ${num(a, 'topN')} taken)`,
    outside_radius: (a: ServerArgs) =>
      `located ~${n(num(a, 'km'))} km away (${str(a, 'place')}), max ${n(num(a, 'maxKm'))} km`,
  } satisfies Record<string, ServerRenderer>,
}
