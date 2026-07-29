export const providers = {
  eyebrow: '01 / sources',
  heading: () => <>Job <em>sites</em></>,
  lede: 'Where listings come from. Turn each one on or off, test it, or add your own.',
  addSource: '+ Add a source',
  addFirstSource: '+ Add your first source',
  added: (name: string) => `Added ${name}.`,
  loading: 'Loading sources…',
  loadFailed: 'Failed to load sources.',
  searchPlaceholder: 'Search sources…',
  searchAria: 'Search sources by name',
  filterAria: 'Filter sources',
  filter: { all: 'All', on: 'On', off: 'Off', failing: 'Failing' },
  noneYet: 'No job sites set up yet.',
  noMatches: (query: string, filter: string) =>
    `No sources match${query ? ` “${query}”` : ''}${filter ? ` in “${filter}”` : ''}.`,

  tileTooltip: 'Open this source to view its details and change how much it fetches',
  health: {
    working: 'OK',
    failing: 'failing',
    stale: 'stale',
    untested: 'not tested yet',
    blocked: 'needs key',
  },
  testedOk: (count: number, ms: number) => `tested · ${count} jobs · ${ms}ms`,
  testedFail: (error: string) => `tested · ${error}`,
  blockedMeta: 'won’t run until keyed',
  fetchedMeta: (relative: string, count?: number) =>
    `${relative}${typeof count === 'number' ? ` · ${count} jobs` : ''}`,
  neverUsed: 'never used',
  addKey: (label: string) => `Add ${label} →`,
  addKeyAria: (label: string, name: string) => `Add ${label} for ${name}`,
  test: 'Test',
  manualCantTest: 'Manual sources can’t be tested automatically',
  enableAria: (name: string) => `Enable ${name}`,
  on: 'On',
  off: 'Off',
  testResultOk: (name: string, count: number, ms: number) => `${name}: ${count} listings · ${ms}ms`,
  testResultFail: (name: string, error: string) => `${name}: ${error}`,
  failedShort: 'failed',
  saveFailed: 'Save failed',

  // Short form on the tiles; the detail panel spells out the mechanism.
  type: {
    api: 'Auto-fetched',
    rss: 'News feed',
    html: 'Read from website',
    teamtailor: 'Auto-fetched',
    hrmanager: 'Auto-fetched',
    manual: 'Manual import',
  },
  typeDetailed: {
    api: 'Auto-fetched (API)',
    rss: 'News feed (RSS)',
    html: 'Read from website',
    teamtailor: 'Auto-fetched (Teamtailor)',
    hrmanager: 'Auto-fetched (HR Manager)',
    manual: 'Manual import',
  },
  secretLabel: { api_key: 'API key', affid: 'Affiliate ID', other: 'Access key' },

  backToAll: '← all sources',
  detailLoading: 'Loading…',
  detailLoadFailed: 'Failed to load source.',
  sourceEyebrow: 'source',
  enabledAria: 'on',
  removeTitle: 'Remove this source',
  removeBody: 'You added this source, so you can remove it. This only affects your setup.',
  removeConfirm: 'Yes, remove',
  removeFailed: 'Remove failed',
  recentSearches: 'Recent searches',
  viewSearch: 'view search →',

  informationTitle: 'Information',
  platform: 'Platform',
  accessMethod: 'Access method',
  endpoint: 'Endpoint',
  searchQuery: 'Search query',
  fetchStrategy: 'Fetch strategy',
  rateLimit: 'Rate limit',
  perSecond: (rate: number) => `${rate}/s`,
  fullDescriptions: 'Full descriptions',
  fullDescriptionsOn: 'On — fetches each listing’s page',
  fullDescriptionsOff: 'Off — list data only',
  notesLabel: 'Notes',
  singleFetch: 'Single fetch — returns everything the endpoint gives',
  upToPagesCeiling: (pages: number, size: number, ceiling: number) =>
    `Up to ${pages} pages × ${size} = ${ceiling} listings max`,
  upToPages: (pages: string) => `Up to ${pages} pages`,

  configurationTitle: 'Configuration',
  configurationHint:
    'Overrides are saved on this computer and apply to searches and tests. Raise the ceiling to '
    + 'pull more; Reset restores the shipped defaults.',
  maxPages: 'Max pages',
  pageSize: 'Page size',
  rateLimitField: 'Rate limit (req/sec)',
  custom: ' · custom',
  defaultIs: (value: string | number) => `Default: ${value}`,
  enrichHint: (defaultLabel: string) =>
    `Default: ${defaultLabel}. On is slower but gives the ranker each listing’s full text.`,
  resetToDefaults: 'Reset to defaults',
  bodyEnrichmentAria: 'body enrichment',

  secretHint: 'Saved on this computer only. Until you save a value here, this source is skipped when you search.',
  secretPlaceholderSet: '••••••••  (overwrite to update)',
  secretPlaceholder: (label: string) => `Paste your ${label}`,
  clear: 'Clear',
  savedShort: 'Saved.',
  clearedShort: 'Cleared.',
  clearFailed: 'Clear failed',

  testTitle: 'Test the source',
  testNow: 'Test now',
  testManualHint: 'This source doesn’t fetch automatically — there’s nothing to test.',
  testHint: 'Pulls listings once and shows how many came back, how long it took, and every listing returned.',
  testWorking: 'Working',
  testConnectionFailed: 'Connection failed',
  jobsFoundLabel: 'jobs found',
  errorLabel: 'error',
  hitPageCap: () => (
    <>
      <strong>Hit the page cap.</strong> This source stopped at its max-pages limit while more
      listings were still coming — there are almost certainly more. Raise <em>Max pages</em> below and
      re-test to pull more.
    </>
  ),
  possiblyCapped: () => (
    <>
      <strong>Possibly capped.</strong> This source returned exactly its configured limit, so it may
      be holding back more results.
    </>
  ),
  showingFirst: (shown: number, total: number) => `showing first ${shown} of ${total}`,
  allListings: (total: number) => `all ${total} listings`,

  gridRunning: 'running',
  gridFailed: 'failed',
  gridPending: 'pending',
  gridCapped: '⚠ capped',
  gridCappedHard: 'Hit its page cap — there may be more',
  gridCappedSoft: 'Returned exactly its configured limit — may be more',
}
