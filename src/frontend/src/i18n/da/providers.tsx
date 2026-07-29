import type { Messages } from '../en'

export const providers: Messages['providers'] = {
  eyebrow: '01 / jobkilder',
  // Matches the nav — "jobsider" would be a second word for the same thing.
  heading: () => <>Dine <em>jobkilder</em></>,
  lede: 'Her kommer opslagene fra. Slå dem til og fra, test dem, eller tilføj dine egne.',
  addSource: '+ Tilføj en jobkilde',
  addFirstSource: '+ Tilføj din første jobkilde',
  added: name => `${name} er tilføjet.`,
  loading: 'Indlæser jobkilder…',
  loadFailed: 'Jobkilderne kunne ikke indlæses.',
  searchPlaceholder: 'Søg i jobkilder…',
  searchAria: 'Søg i jobkilder efter navn',
  filterAria: 'Filtrér jobkilder',
  filter: { all: 'Alle', on: 'Til', off: 'Fra', failing: 'Fejler' },
  noneYet: 'Du har ingen jobkilder endnu.',
  noMatches: (query, filter) =>
    `Ingen jobkilder matcher${query ? ` “${query}”` : ''}${filter ? ` under “${filter}”` : ''}.`,

  tileTooltip: 'Åbn jobkilden for at se detaljer og justere, hvor meget den henter',
  health: {
    working: 'OK',
    failing: 'fejler',
    stale: 'forældet',
    untested: 'ikke testet',
    blocked: 'mangler nøgle',
  },
  testedOk: (count, ms) => `testet · ${count} job · ${ms} ms`,
  testedFail: error => `testet · ${error}`,
  blockedMeta: 'kører ikke uden nøgle',
  fetchedMeta: (relative, count) =>
    `${relative}${typeof count === 'number' ? ` · ${count} job` : ''}`,
  neverUsed: 'aldrig brugt',
  addKey: label => `Tilføj ${label} →`,
  addKeyAria: (label, name) => `Tilføj ${label} for ${name}`,
  test: 'Test',
  manualCantTest: 'Manuelle jobkilder kan ikke testes automatisk',
  enableAria: name => `Slå ${name} til`,
  on: 'Til',
  off: 'Fra',
  testResultOk: (name, count, ms) => `${name}: ${count} opslag · ${ms} ms`,
  testResultFail: (name, error) => `${name}: ${error}`,
  failedShort: 'mislykkedes',
  saveFailed: 'Kunne ikke gemme',

  type: {
    api: 'Hentes automatisk',
    rss: 'Nyhedsfeed',
    html: 'Læses fra hjemmeside',
    teamtailor: 'Hentes automatisk',
    hrmanager: 'Hentes automatisk',
    manual: 'Manuel import',
  },
  typeDetailed: {
    api: 'Hentes automatisk (API)',
    rss: 'Nyhedsfeed (RSS)',
    html: 'Læses fra hjemmeside',
    teamtailor: 'Hentes automatisk (Teamtailor)',
    hrmanager: 'Hentes automatisk (HR Manager)',
    manual: 'Manuel import',
  },
  secretLabel: { api_key: 'API-nøgle', affid: 'Affiliate-id', other: 'Adgangsnøgle' },

  backToAll: '← alle jobkilder',
  detailLoading: 'Indlæser…',
  detailLoadFailed: 'Jobkilden kunne ikke indlæses.',
  sourceEyebrow: 'jobkilde',
  enabledAria: 'til',
  removeTitle: 'Fjern denne jobkilde',
  removeBody: 'Du har selv tilføjet den, så du kan fjerne den igen. Det påvirker kun din egen opsætning.',
  removeConfirm: 'Ja, fjern',
  removeFailed: 'Kunne ikke fjerne',
  recentSearches: 'Seneste søgninger',
  viewSearch: 'se søgning →',

  informationTitle: 'Oplysninger',
  platform: 'Platform',
  accessMethod: 'Hentemetode',
  endpoint: 'Endpoint',
  searchQuery: 'Søgestreng',
  fetchStrategy: 'Hentestrategi',
  rateLimit: 'Hastighedsgrænse',
  perSecond: rate => `${rate}/s`,
  fullDescriptions: 'Fulde beskrivelser',
  fullDescriptionsOn: 'Til — henter hvert opslags egen side',
  fullDescriptionsOff: 'Fra — kun data fra listen',
  notesLabel: 'Noter',
  singleFetch: 'Én hentning — henter alt, hvad endpointet giver',
  upToPagesCeiling: (pages, size, ceiling) =>
    `Op til ${pages} sider × ${size} = højst ${ceiling} opslag`,
  upToPages: pages => `Op til ${pages} sider`,

  configurationTitle: 'Konfiguration',
  configurationHint:
    'Dine justeringer gemmes på denne computer og gælder både søgninger og tests. Hæv loftet for at '
    + 'hente mere; Nulstil sætter alt tilbage til standard.',
  maxPages: 'Maks. sider',
  pageSize: 'Sidestørrelse',
  rateLimitField: 'Hastighedsgrænse (kald/sek.)',
  custom: ' · tilpasset',
  defaultIs: value => `Standard: ${value}`,
  enrichHint: defaultLabel =>
    `Standard: ${defaultLabel}. Til er langsommere, men så vurderes opslaget ud fra hele teksten.`,
  resetToDefaults: 'Nulstil til standard',
  bodyEnrichmentAria: 'fulde beskrivelser',

  secretHint: 'Gemmes kun på denne computer. Uden en nøgle her springes jobkilden over, når du søger.',
  secretPlaceholderSet: '••••••••  (overskriv for at opdatere)',
  secretPlaceholder: label => `Indsæt din ${label}`,
  clear: 'Ryd',
  savedShort: 'Gemt.',
  clearedShort: 'Ryddet.',
  clearFailed: 'Kunne ikke rydde',

  testTitle: 'Test jobkilden',
  testNow: 'Test nu',
  testManualHint: 'Denne jobkilde henter ikke automatisk — der er ikke noget at teste.',
  testHint: 'Henter opslag én gang og viser, hvor mange der kom, hvor lang tid det tog, og hvilke det var.',
  testWorking: 'Virker',
  testConnectionFailed: 'Forbindelsen mislykkedes',
  jobsFoundLabel: 'job fundet',
  errorLabel: 'fejl',
  hitPageCap: () => (
    <>
      <strong>Ramte sidegrænsen.</strong> Jobkilden stoppede ved sin grænse for maks. sider, mens
      der stadig kom flere opslag — så der er næsten helt sikkert flere. Hæv <em>Maks. sider</em>{' '}
      nedenfor, og test igen.
    </>
  ),
  possiblyCapped: () => (
    <>
      <strong>Muligvis afkortet.</strong> Jobkilden returnerede præcis sin grænse, så den holder
      måske flere resultater tilbage.
    </>
  ),
  showingFirst: (shown, total) => `viser de første ${shown} af ${total}`,
  allListings: total => `alle ${total} opslag`,

  gridRunning: 'kører',
  gridFailed: 'fejlede',
  gridPending: 'afventer',
  gridCapped: '⚠ afkortet',
  gridCappedHard: 'Ramte sin sidegrænse — der kan være flere',
  gridCappedSoft: 'Returnerede præcis sin grænse — der kan være flere',
}
