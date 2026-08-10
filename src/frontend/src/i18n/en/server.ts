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
    llmJudgingMore: (a: ServerArgs) => `AI reviewing ${num(a, 'count')} more jobs that moved into the top list…`,
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
}
