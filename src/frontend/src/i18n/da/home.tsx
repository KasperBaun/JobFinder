import type { Messages } from '../en'

export const home: Messages['home'] = {
  eyebrow: '00 / overblik',
  headline: () => <>Find jobbet, der <em>passer.</em></>,
  lede:
    'Jobfinder henter opslag fra de jobkilder, du vælger, vurderer dem op mod din profil og '
    + 'finder de bedste match. Alt kører lokalt — intet forlader din computer.',
  loading: 'indlæser…',
  runSearch: 'Kør en ny søgning',
  editProfile: 'Redigér profil',
  // Nav and eyebrow already say "Overblik"; a third one would just echo.
  atAGlance: 'Kort fortalt',
  errorShort: 'fejl',

  sources: 'Jobkilder',
  sourcesOn: 'aktive',
  sourcesSetUp: total => `${total} i alt`,

  lastSearch: 'Seneste søgning',
  noSearchesYet: 'Ingen søgninger endnu',
  topJobs: 'topjob',
  best: 'højeste',

  goodMatches: 'Gode match',
  acrossAllSearches: 'i alle søgninger',

  profile: 'Profil',
  profileReady: 'klar',
  profileNotSetUp: 'mangler',
  profileReadyHint: 'kompetencer, brancher, dealbreakers',
  profileFinishHint: 'færdiggør opsætningen →',

  recentSearches: 'Seneste søgninger',
  viewAll: 'Se alle →',
  colWhen: 'Hvornår',
  colTopJobs: 'Topjob',
  colBestRating: 'Højeste score',
  colGoodMatches: 'Gode match',
}
