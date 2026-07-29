import type { Messages } from '../en'

export const search: Messages['search'] = {
  phase: {
    pending: 'I kø',
    fetching: 'Henter opslag',
    deduping: 'Fjerner dubletter',
    ranking: 'Vurderer job',
    llmJudging: 'AI-gennemgang',
    writing: 'Gør færdig',
    done: 'Færdig',
  },

  state: {
    queued: 'I kø',
    running: 'Kører',
    succeeded: 'Fuldført',
    failed: 'Mislykkedes',
    cancelled: 'Annulleret',
    interrupted: 'Afbrudt',
  },

  steps: {
    fetching: 'Hent job',
    deduping: 'Fjern dubletter',
    ranking: 'Vurdér match',
    llmJudging: 'AI-gennemgang',
    done: 'Færdig',
  },
  stepsAria: 'Trin i søgningen',

  eyebrow: '03 / søg',
  heading: () => <>Kør en <em>søgning</em></>,
  lede:
    'Henter de nyeste opslag fra dine aktive jobkilder, fjerner dubletter, vurderer dem op mod din '
    + 'profil og viser dig de bedste bud. Søgningen kører videre, selv om du går til en anden side '
    + 'eller genindlæser.',

  profileFirst: 'Sæt din profil op først — jobfinder vurderer hvert opslag op mod den.',
  setUpProfile: 'Sæt din profil op',

  running: 'Kører…',
  searchRunning: 'Søgning kører',
  runSearch: 'Kør en søgning',
  reset: 'Nulstil',
  hideOptions: 'Skjul indstillinger',
  moreOptions: 'Flere indstillinger…',

  topNLabel: 'Antal topjob',
  minScoreLabel: 'Mindste vurdering',
  defaultPlaceholder: 'standard',
  sourcesLabel: 'Jobkilder',
  sourceTurnedOff: 'Slået fra på siden Jobkilder',
  sourceNeedsKey: 'Kræver en API-nøgle — angiv den på siden Jobkilder',

  lastSearchSummary: (when, count, score) => (
    <>
      Seneste søgning var <strong>{when}</strong> — <span className="tabular">{count}</span> topjob,
      bedste vurdering <span className="tabular mono">{score}</span>.
    </>
  ),
  runToRefresh: viewLink => (
    <>Tryk <strong>Kør en søgning</strong> for at opdatere, eller {viewLink}.</>
  ),
  viewThatSearch: 'se den søgning',
  readyWhenYouAre: () => (
    <>Klar, når du er. Tryk <strong>Kør en søgning</strong> for at hente de nyeste opslag.</>
  ),

  attempt: n => ` · forsøg ${n}`,
  hideSteps: 'skjul trin ▴',
  showSteps: 'vis trin ▾',
  searchFailed: error => `Søgningen mislykkedes: ${error}`,

  jobsFound: count => `${count} job fundet`,
  sourcesRunning: count => `${count} kører`,
  sourcesFailed: count => `${count} fejlede`,
  unique: count => `${count} unikke`,
  topScore: score => `bedste ${score}`,

  activityLog: count => `Aktivitetslog (${count} hændelser)`,

  topJobs: 'Topjob',
  loadingResults: 'Indlæser resultater…',
  noJobsMetMinimum: 'Ingen job nåede op på den mindste vurdering.',
}
