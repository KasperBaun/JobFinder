export const sources = {
  title: 'Add a source',
  close: 'Close',

  pasteHint:
    'Paste the web address of a company’s jobs page or a job feed. We’ll recognise the common '
    + 'ones and set them up for you.',
  urlPlaceholder: 'https://boards.greenhouse.io/company',
  findIt: 'Find it',
  importSpreadsheet: 'Import a spreadsheet instead',

  nameLabel: 'Name',
  addSource: 'Add source',
  fallbackName: 'source',

  notFoundHint:
    'We couldn’t recognise that address automatically. You can still add it as a manual import — '
    + 'you export the roles yourself and drop them in, and they’ll be included in your next search.',
  setUpManual: 'Set up manual import',
  tryAnother: 'Try another address',

  manualNameLabel: 'Name this source',
  manualNamePlaceholder: 'e.g. LinkedIn saved roles',
  manualHint:
    'After adding, export your roles to a CSV and save it in your imports folder. Open the source '
    + 'afterwards for the exact file name and columns.',

  foundJobs: (count: number) => `Found ${count} jobs`,
  nothingCameBack: 'Nothing came back',
  sample: (title: string) => `e.g. “${title}”`,

  fetching: 'Fetching the jobs…',
  checkingExisting: 'Checking whether you already have this…',

  duplicateTitle: 'You already have this source',
  duplicateBody: (name: string, shared: number, total: number) =>
    `“${name}” already brings in ${shared} of these ${total} jobs. Adding this would fetch them twice.`,
  overlapBody: (name: string, shared: number, total: number) =>
    `${shared} of these ${total} jobs also come from “${name}”. The rest would be new.`,
  openExisting: (name: string) => `Open ${name}`,
  addAnyway: 'Add anyway',
}
