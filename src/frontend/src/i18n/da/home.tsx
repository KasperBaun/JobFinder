import type { Messages } from '../en'

export const home: Messages['home'] = {
  eyebrow: '00 / overblik',
  headline: () => <>Find arbejde, der <em>passer.</em></>,
  lede:
    'Jobfinder gør jobsøgningen nem. Den henter jobopslag fra de jobkilder, du vælger, vurderer '
    + 'dem op mod din profil og dine præferencer og foreslår de bedste match. Alt kører lokalt, '
    + 'så intet forlader din computer.',
  loading: 'indlæser…',
  runSearch: 'Kør en ny søgning',
  editProfile: 'Redigér profil',
  atAGlance: 'Overblik',
  errorShort: 'fejl',

  sources: 'Jobkilder',
  sourcesOn: 'til',
  sourcesSetUp: total => `${total} sat op`,

  lastSearch: 'Seneste søgning',
  noSearchesYet: 'Ingen søgninger endnu',
  topJobs: 'topjob',
  best: 'bedste',

  goodMatches: 'Gode match',
  acrossAllSearches: 'på tværs af alle søgninger',

  profile: 'Profil',
  profileReady: 'klar',
  profileNotSetUp: 'ikke sat op',
  profileReadyHint: 'kompetencer, brancher, dealbreakers',
  profileFinishHint: 'gør opsætningen færdig →',

  recentSearches: 'Seneste søgninger',
  viewAll: 'Se alle →',
  colWhen: 'Hvornår',
  colTopJobs: 'Topjob',
  colBestRating: 'Bedste vurdering',
  colGoodMatches: 'Gode match',
}
