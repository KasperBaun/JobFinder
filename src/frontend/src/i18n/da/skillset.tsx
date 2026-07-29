import type { Messages } from '../en'

export const skillset: Messages['skillset'] = {
  seniority: {
    junior: 'junior',
    mid: 'mellem',
    senior: 'senior',
    lead: 'lead',
    any: 'alle',
  },

  remote: {
    onsite: 'på kontoret',
    hybrid: 'hybrid',
    remote: 'hjemmefra',
    any: 'alle',
  },

  eyebrow: '02 / profil',
  headingCreate: () => <>Sæt din <em>profil</em> op</>,
  headingEdit: () => <>Din <em>profil</em></>,
  ledeCreate:
    'Du sprang den over under opsætningen. Udfyld den, så jobfinder kan vurdere opslag for dig — '
    + 'som minimum et navn og en placering til at starte med.',
  ledeEdit: 'Redigér det, jobfinder bruger til at vurdere hvert jobopslag. Gemmes automatisk.',
  fillFromCv: 'Udfyld fra CV',
  loading: 'Indlæser profil…',
  loadFailed: 'Profilen kunne ikke indlæses.',
  created: 'Profilen er oprettet',
  saved: 'Profilen er gemt',
  saveFailed: 'Kunne ikke gemme',
  prefilled: 'Udfyldt fra CV — gennemgå det, og gem derefter',

  aboutYou: 'Om dig',
  name: 'Navn',
  location: 'Placering',
  country: 'Land',
  region: 'Region',
  optional: 'valgfrit',
  experienceYears: 'Års erfaring',
  languages: 'Sprog',
  languagesPlaceholder: 'f.eks. da, en',
  metro: 'Byer / områder',
  metroPlaceholder: 'valgfrit — f.eks. København, Aarhus',

  rolesAndPreferences: 'Roller og præferencer',
  seniorityLabel: 'Erfaringsniveau',
  remoteLabel: 'Hvor du vil arbejde',
  targetRoles: 'Roller, du ønsker',
  targetRolesPlaceholder: 'f.eks. Senior Backend Engineer',
  employmentTypes: 'Ansættelsestyper',
  employmentTypesPlaceholder: 'f.eks. fuldtid, kontrakt',

  skills: 'Kompetencer',
  primaryStack: 'Skal-have-kompetencer',
  primaryStackHint: 'jobopslaget skal nævne disse. Flere match = højere vurdering',
  primaryStackPlaceholder: 'f.eks. C#, .NET, Postgres',
  secondaryStack: 'Gode-at-have-kompetencer',
  secondaryStackHint: 'lille bonus, når de nævnes',
  secondaryStackPlaceholder: 'f.eks. Docker, Kubernetes',

  industries: 'Brancher',
  industriesHint: 'Områder, du gerne vil arbejde inden for.',
  industriesPlaceholder: 'f.eks. fintech, b2b saas',

  favoriteCompanies: 'Favoritvirksomheder',
  favoriteCompaniesHint: 'Arbejdsgivere, du meget gerne vil arbejde for. Deres opslag får et løft i vurderingen.',
  favoriteCompaniesPlaceholder: 'f.eks. LEGO, Mærsk',

  dealBreakers: 'Dealbreakers',
  dealBreakersHint: 'Et opslag med bare én af disse bliver fjernet.',
  dealBreakersPlaceholder: 'f.eks. kun på kontoret, vikarbureau',
}
