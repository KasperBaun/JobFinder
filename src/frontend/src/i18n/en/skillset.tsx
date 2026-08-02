export const skillset = {
  // Keyed by the raw domain value stored in skillset.md, so the profile file stays language-neutral
  // and only the rendering changes.
  seniority: {
    junior: 'junior',
    mid: 'mid',
    senior: 'senior',
    lead: 'lead',
    any: 'any',
  },

  remote: {
    onsite: 'onsite',
    hybrid: 'hybrid',
    remote: 'remote',
    any: 'any',
  },

  eyebrow: '02 / profile',
  headingCreate: () => <>Set up your <em>profile</em></>,
  headingEdit: () => <>Your <em>profile</em></>,
  ledeCreate:
    'You skipped this during setup. Fill it in so jobfinder can rate listings for you — at least '
    + 'a name and location to start.',
  // Was "Saved automatically." — untrue, the page has a save bar.
  ledeEdit: 'This is what jobfinder rates every listing against.',
  fillFromCv: 'Fill from CV',
  loading: 'Loading profile…',
  loadFailed: 'Failed to load profile.',
  created: 'Profile created',
  saved: 'Profile saved',
  saveFailed: 'Save failed',
  prefilled: 'Prefilled from CV — review, then save',

  aboutYou: 'About you',
  name: 'Name',
  location: 'Location',
  country: 'Country',
  region: 'Region',
  optional: 'optional',
  experienceYears: 'Years of experience',
  languages: 'Languages',
  languagesPlaceholder: 'e.g. en, da',
  metro: 'Cities / areas',
  metroPlaceholder: 'optional — e.g. Copenhagen, Aarhus',

  homeAddress: 'Home address',
  homeAddressPlaceholder: 'optional — e.g. Rådhuspladsen 1, 1550 København V',
  homeAddressHint: 'Danish addresses only. With a max distance set, jobs farther away are filtered out.',
  radiusKm: 'Max distance (km)',
  radiusKmHint: '0 = off',
  addressResolved: (resolved: string) => `Address found: ${resolved}`,
  addressNotResolved: 'Address not recognized — the distance filter is off.',
  addressPending: 'The address is checked when you save.',

  rolesAndPreferences: 'Roles & preferences',
  seniorityLabel: 'Experience level',
  remoteLabel: 'Where you want to work',
  targetRoles: 'Roles you want',
  targetRolesPlaceholder: 'e.g. Senior Backend Engineer',
  employmentTypes: 'Employment types',
  employmentTypesPlaceholder: 'e.g. full-time, contract',

  skills: 'Skills',
  primaryStack: 'Must-have skills',
  primaryStackHint: 'the job has to mention these. More matches = higher rating',
  primaryStackPlaceholder: 'e.g. C#, .NET, Postgres',
  secondaryStack: 'Nice-to-have skills',
  secondaryStackHint: 'small bonus when mentioned',
  secondaryStackPlaceholder: 'e.g. Docker, Kubernetes',

  industries: 'Industries',
  industriesHint: 'Areas you’d like to work in.',
  industriesPlaceholder: 'e.g. fintech, b2b saas',

  favoriteCompanies: 'Favorite companies',
  favoriteCompaniesHint: 'Employers you’d love to work for. Their listings get a rating boost.',
  favoriteCompaniesPlaceholder: 'e.g. LEGO, Maersk',

  dealBreakers: 'Deal-breakers',
  dealBreakersHint: 'A listing with any of these is removed.',
  dealBreakersPlaceholder: 'e.g. on-site only, agency',
}
