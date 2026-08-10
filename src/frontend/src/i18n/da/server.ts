import type { Messages } from '../en'
import { dec, n } from '../format'
import { list, num, str } from '../serverText'
import type { ServerArgs } from '../serverText'

export const server: Messages['server'] = {
  remoteMode: {
    remote: 'hjemmefra',
    hybrid: 'hybrid',
    onsite: 'på kontoret',
    unknown: 'ukendt',
  },

  timeline: {
    queued: () => 'Søgning i kø',
    started: () => 'Søgning startet',
    resumed: (a: ServerArgs) => `Søgning genoptaget (forsøg ${num(a, 'attempt')})`,
    complete: (a: ServerArgs) => `Søgning færdig — ${num(a, 'count')} topjob`,
    failed: (a: ServerArgs) => `Søgningen mislykkedes: ${str(a, 'error')}`,
    cancelled: () => 'Søgning annulleret',

    fetchingFrom: (a: ServerArgs) => `Henter opslag fra ${num(a, 'total')} jobkilder`,
    providerRunning: (a: ServerArgs) => `Henter ${str(a, 'provider')} (${num(a, 'index')}/${num(a, 'total')})`,
    providerDone: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} job · ${dec(num(a, 'seconds'), 0)} s`,
    providerDoneCapped: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} job · ${dec(num(a, 'seconds'), 0)} s · ramte sidegrænsen, der kan være flere`,
    providerDoneLimited: (a: ServerArgs) =>
      `${str(a, 'provider')}: ${n(num(a, 'count'))} job · ${dec(num(a, 'seconds'), 0)} s · på den fastsatte grænse, der kan være flere`,
    providerFailed: (a: ServerArgs) =>
      `${str(a, 'provider')} fejlede: ${str(a, 'error')} · ${dec(num(a, 'seconds'), 0)} s`,

    deduped: (a: ServerArgs) => `${n(num(a, 'count'))} unikke job efter dubletter er fjernet`,
    llmJudging: (a: ServerArgs) => `AI gennemgår de ${num(a, 'count')} bedste job…`,
    llmJudgingMore: (a: ServerArgs) => `AI gennemgår ${num(a, 'count')} job mere, der er rykket ind på toplisten…`,
    ranked: (a: ServerArgs) => `${n(num(a, 'count'))} job vurderet · højeste ${dec(num(a, 'topScore'), 2)}`,
    writing: () => 'Skriver resultater',
  },

  reasoning: {
    disqualified: (a: ServerArgs) => `Fjernet på grund af: ${list(a, 'hits').join(', ')}.`,
    primaryHits: (a: ServerArgs) => `Match på skal-have-kompetencer: ${list(a, 'skills').join(', ')}.`,
    noPrimaryHits: () => 'Ingen af dine skal-have-kompetencer nævnes.',
    secondaryHits: (a: ServerArgs) => `Matcher også: ${list(a, 'skills').join(', ')}.`,
    domainHits: (a: ServerArgs) => `Branche: ${list(a, 'domains').join(', ')}.`,
    seniorityClose: () => 'Erfaringsniveauet er tæt på.',
    seniorityMatches: () => 'Erfaringsniveauet passer.',
    seniorityMismatch: () => 'Erfaringsniveauet passer ikke.',
    seniorityUnknown: () => 'Erfaringsniveau ikke oplyst.',
    titleNotDeveloper: () => 'Titlen ligner ikke en udviklerrolle — scoren er sat ned.',
    location: (a: ServerArgs) => `Sted: ${str(a, 'location')}.`,
    remoteOk: (a: ServerArgs) => `Hjemmearbejde er OK (${server.remoteMode[str(a, 'mode')] ?? str(a, 'mode')}).`,
    neitherLocationNorRemote: () => 'Hverken sted eller hjemmearbejde passer.',
    locationRemoteUnknown: () => 'Sted og hjemmearbejde er ikke oplyst.',
    locationMismatchRemoteUnknown: () => 'Stedet passer ikke; hjemmearbejde er ikke oplyst.',
    locationUnknownRemoteMismatch: () => 'Stedet er ikke oplyst; hjemmearbejde passer ikke.',
    agePenalty: (a: ServerArgs) => `Slået op for ${num(a, 'days')} dage siden — scoren er sat ned for alder.`,
  },
}
