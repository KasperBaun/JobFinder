import type { Messages } from '../en'

export const listing: Messages['listing'] = {
  favoriteBadge: '★ Favorit',
  favoriteTitle: 'En af dine favoritvirksomheder — vurderingen er løftet',
  openPosting: 'Åbn jobopslaget →',

  markGood: 'Godt match',
  markBad: 'Ikke et match',
  markUnset: 'Vurdér dette job',
  markTooltip: {
    unset: 'Vurdér dette job. Dine vurderinger hjælper jobfinder med at finde bedre job næste gang. Tryk for et godt match.',
    good: 'Markeret som et godt match. Tryk for at skifte til Ikke et match.',
    bad: 'Markeret som Ikke et match. Tryk for at rydde vurderingen.',
  },
  markFailed: 'Markeringen mislykkedes',

  whyAdd: '+ hvorfor?',
  whyTooltip: 'Skriv en kort begrundelse — den lærer AI’en, hvad den skal kigge efter næste gang.',
  whyPlaceholder: 'Hvorfor? f.eks. “Jeg er ikke studerende”',
  whyEdit: reason => `${reason} — tryk for at redigere`,

  statusTooltip: 'Hold styr på, hvad der skete, efter du søgte. Samtaler og tilbud lærer AI’en, hvordan et stærkt match ser ud.',
  statusPlaceholder: 'Status…',
  statusClear: 'Ryd status',
  statusFailed: 'Statussen kunne ikke opdateres',
  status: {
    applied: 'Ansøgt',
    interview: 'Samtale',
    offer: 'Tilbud',
    rejected: 'Afvist',
    'no-response': 'Intet svar',
  },

  breakdownAria: 'fordeling af vurderingen',
  component: {
    primaryStack: 'skal-have-kompetencer',
    secondaryStack: 'gode-at-have-kompetencer',
    seniority: 'erfaringsniveau',
    locationRemote: 'sted',
    domain: 'branche',
    freshness: 'friskhed',
  },
  disqualifierPenalty: 'dealbreaker-fradrag',
  totalRating: 'samlet vurdering',

  runTitle: 'Søgning',
  runSources: 'Jobkilder',
  runOk: 'ok',
  runFailed: 'fejlede',
  runJobsFound: 'Job fundet',
  runUniqueJobs: 'Unikke job',
  runTopJobs: 'Topjob',
  runBestRating: 'Bedste vurdering',
  runGoodMatches: 'Gode match',
}
