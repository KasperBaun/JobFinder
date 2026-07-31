import type { Messages } from '../en'

export const skillset: Messages['skillset'] = {
  // Danish tech ads use these levels in English — translating them reads wrong.
  seniority: {
    junior: 'junior',
    mid: 'mid',
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
    + 'et navn og et sted er nok til at komme i gang.',
  ledeEdit: 'Det er den, jobfinder vurderer hvert opslag op mod.',
  fillFromCv: 'Udfyld fra CV',
  loading: 'Indlæser profil…',
  loadFailed: 'Profilen kunne ikke indlæses.',
  created: 'Profilen er oprettet',
  saved: 'Profilen er gemt',
  saveFailed: 'Kunne ikke gemme',
  prefilled: 'Udfyldt fra CV — gennemgå det, og gem',

  aboutYou: 'Om dig',
  name: 'Navn',
  location: 'Sted',
  country: 'Land',
  region: 'Region',
  optional: 'valgfrit',
  experienceYears: 'Års erfaring',
  languages: 'Sprog',
  languagesPlaceholder: 'f.eks. da, en',
  metro: 'Byer / områder',
  metroPlaceholder: 'valgfrit — f.eks. København, Aarhus',

  homeAddress: 'Hjemmeadresse',
  homeAddressPlaceholder: 'valgfrit — f.eks. Rådhuspladsen 1, 1550 København V',
  homeAddressHint: 'Kun danske adresser. Med en maks. afstand sat bliver job længere væk frasorteret.',
  radiusKm: 'Maks. afstand (km)',
  radiusKmHint: '0 = slået fra',
  addressResolved: resolved => `Adressen blev fundet: ${resolved}`,
  addressNotResolved: 'Adressen blev ikke genkendt — afstandsfilteret er slået fra.',
  addressPending: 'Adressen bliver tjekket, når du gemmer.',

  rolesAndPreferences: 'Roller og præferencer',
  seniorityLabel: 'Erfaringsniveau',
  remoteLabel: 'Hvor du vil arbejde',
  targetRoles: 'Roller, du ønsker',
  targetRolesPlaceholder: 'f.eks. Senior Backend Engineer',
  employmentTypes: 'Ansættelsestyper',
  employmentTypesPlaceholder: 'f.eks. fuldtid, kontrakt',

  skills: 'Kompetencer',
  primaryStack: 'Skal-have-kompetencer',
  primaryStackHint: 'opslaget skal nævne dem. Flere match giver højere score',
  primaryStackPlaceholder: 'f.eks. C#, .NET, Postgres',
  secondaryStack: 'Gode-at-have-kompetencer',
  secondaryStackHint: 'giver et lille løft, når de nævnes',
  secondaryStackPlaceholder: 'f.eks. Docker, Kubernetes',

  industries: 'Brancher',
  industriesHint: 'Områder, du gerne vil arbejde inden for.',
  industriesPlaceholder: 'f.eks. fintech, b2b saas',

  favoriteCompanies: 'Favoritvirksomheder',
  favoriteCompaniesHint: 'Arbejdsgivere, du gerne vil arbejde for. Deres opslag får et løft i scoren.',
  favoriteCompaniesPlaceholder: 'f.eks. LEGO, Mærsk',

  dealBreakers: 'Dealbreakers',
  dealBreakersHint: 'Et opslag med bare én af dem bliver fjernet.',
  dealBreakersPlaceholder: 'f.eks. kun på kontoret, vikarbureau',
}
