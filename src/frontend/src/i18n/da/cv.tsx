import type { Messages } from '../en'

export const cv: Messages['cv'] = {
  title: 'Udfyld fra CV',

  aiDisabled: () => (
    <>
      AI-gennemgang er slået fra (<code>llm.enabled</code> i <code>ranking.yml</code>), og det kræver
      den lokale AI-model at læse et CV. Slå den til, og kom tilbage.
    </>
  ),
  modelMissing:
    'Det kræver den lokale AI-model at læse et CV, og den er ikke hentet endnu. Start hentningen '
    + 'nedenfor — du kan roligt bruge appen imens og komme tilbage, når den er færdig.',

  reading: 'Læser dit CV…',
  readingHint:
    'Dette kører den lokale AI-model — typisk et minut eller to på CPU. Du kan lukke dialogen eller '
    + 'gå videre; den kører videre, og resultatet er her, når du kommer tilbage.',

  reviewHint:
    'Her er, hvad CV’et angiver, ved siden af det, din profil har nu. At anvende udfylder kun '
    + 'formularen — gennemgå resultatet, og tryk Gem for at beholde det.',
  nothingNew: 'Intet nyt — din profil dækker allerede alt, hvad CV’et angiver.',
  colField: 'Felt',
  colCurrent: 'Nuværende',
  colFromCv: 'Fra CV',
  applyAria: field => `Anvend ${field}`,
  applyFields: count => `Anvend ${count} felt${count === 1 ? '' : 'er'}`,
  startOver: 'Start forfra',

  modePaste: 'Indsæt tekst',
  modeFile: 'Upload fil',
  modeUrl: 'Fra et link',
  pastePlaceholder: 'Indsæt hele teksten fra dit CV her…',
  fileHint: '.pdf, .txt eller .md — for Word-dokumenter kan du indsætte teksten i stedet.',
  urlPlaceholder: 'https://example.com/mit-cv.pdf',
  extractionFailed: error => `Læsningen mislykkedes: ${error}`,
  unknownError: 'ukendt fejl',
  readMyCv: 'Læs mit CV',

  fields: {
    name: 'Navn',
    location: 'Placering',
    country: 'Land',
    region: 'Region',
    metro: 'Byer / områder',
    experienceYears: 'Års erfaring',
    seniority: 'Erfaringsniveau',
    remotePreference: 'Hvor du vil arbejde',
    targetRoles: 'Roller, du ønsker',
    primaryStack: 'Skal-have-kompetencer',
    secondaryStack: 'Gode-at-have-kompetencer',
    domains: 'Brancher',
    languages: 'Sprog',
    employmentTypes: 'Ansættelsestyper',
  },

  modelBannerTitle: 'AI-gennemgang er slået til, men den lokale model er ikke hentet endnu.',
  modelBannerExpects: (provider, path) => (
    <>Motoren <code>{provider}</code> forventer <code style={{ wordBreak: 'break-all' }}>{path}</code>.</>
  ),
  retryDownload: 'Prøv at hente igen',
  downloadModel: 'Hent model (~2,3 GB)',
  downloading: (done, total, pct) =>
    `Henter ${done}${total ? ` af ${total}` : ''}${pct !== null ? ` (${pct} %)` : ''}`,
  downloadFailed: error => `Hentningen mislykkedes: ${error}`,
}
