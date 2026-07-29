export const cv = {
  title: 'Fill from CV',

  aiDisabled: () => (
    <>
      AI review is turned off (<code>llm.enabled</code> in <code>ranking.yml</code>), and reading a CV
      needs the local AI model. Enable it and come back.
    </>
  ),
  modelMissing:
    'Reading a CV uses the local AI model, which hasn’t been downloaded yet. Start the download '
    + 'below — you can keep using the app and come back when it’s done.',

  reading: 'Reading your CV…',
  readingHint:
    'This runs the local AI model — typically a minute or two on CPU. You can close this dialog '
    + 'or navigate away; it keeps running and the result will be here when you return.',

  reviewHint:
    'Here’s what the CV states, next to what your profile has now. Applying only fills the form — '
    + 'review the result and hit Save to keep it.',
  nothingNew: 'Nothing new — your profile already covers everything the CV states.',
  colField: 'Field',
  colCurrent: 'Current',
  colFromCv: 'From CV',
  applyAria: (field: string) => `Apply ${field}`,
  applyFields: (count: number) => `Apply ${count} field${count === 1 ? '' : 's'}`,
  startOver: 'Start over',

  modePaste: 'Paste text',
  modeFile: 'Upload file',
  modeUrl: 'From a link',
  pastePlaceholder: 'Paste the full text of your CV here…',
  fileHint: '.pdf, .txt or .md — for Word documents, paste the text instead.',
  urlPlaceholder: 'https://example.com/my-cv.pdf',
  extractionFailed: (error: string) => `Extraction failed: ${error}`,
  unknownError: 'unknown error',
  readMyCv: 'Read my CV',

  // Field labels for the diff table — same wording as the profile page.
  fields: {
    name: 'Name',
    location: 'Location',
    country: 'Country',
    region: 'Region',
    metro: 'Cities / areas',
    experienceYears: 'Years of experience',
    seniority: 'Experience level',
    remotePreference: 'Where you want to work',
    targetRoles: 'Roles you want',
    primaryStack: 'Must-have skills',
    secondaryStack: 'Nice-to-have skills',
    domains: 'Industries',
    languages: 'Languages',
    employmentTypes: 'Employment types',
  },

  modelBannerTitle: 'AI review is enabled, but the local model hasn’t been downloaded yet.',
  modelBannerExpects: (provider: string, path: string) => (
    <>Engine <code>{provider}</code> expects <code style={{ wordBreak: 'break-all' }}>{path}</code>.</>
  ),
  retryDownload: 'Retry download',
  downloadModel: 'Download model (~2.3 GB)',
  downloading: (done: string, total: string | null, pct: number | null) =>
    `Downloading ${done}${total ? ` of ${total}` : ''}${pct !== null ? ` (${pct}%)` : ''}`,
  downloadFailed: (error: string) => `Download failed: ${error}`,
}
