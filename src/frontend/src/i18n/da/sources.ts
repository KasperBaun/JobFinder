import type { Messages } from '../en'

export const sources: Messages['sources'] = {
  title: 'Tilføj en jobkilde',
  close: 'Luk',

  pasteHint:
    'Indsæt adressen på en virksomheds jobside eller et jobfeed. Vi genkender de mest almindelige '
    + 'og sætter dem op for dig.',
  urlPlaceholder: 'https://boards.greenhouse.io/virksomhed',
  findIt: 'Find den',
  importSpreadsheet: 'Importér et regneark i stedet',

  nameLabel: 'Navn',
  addSource: 'Tilføj jobkilde',
  testFirst: 'Test først',
  fallbackName: 'jobkilde',

  notFoundHint:
    'Vi kunne ikke genkende adressen automatisk. Du kan stadig tilføje den som manuel import — så '
    + 'eksporterer du selv opslagene, og de kommer med i din næste søgning.',
  setUpManual: 'Sæt manuel import op',
  tryAnother: 'Prøv en anden adresse',

  manualNameLabel: 'Giv jobkilden et navn',
  manualNamePlaceholder: 'f.eks. Gemte opslag på LinkedIn',
  manualHint:
    'Når den er tilføjet, eksporterer du dine opslag til en CSV-fil og gemmer den i din '
    + 'imports-mappe. Åbn jobkilden bagefter for at se det præcise filnavn og de nødvendige kolonner.',

  foundJobs: count => `Fandt ${count} job`,
  nothingCameBack: 'Der kom intet tilbage',
  sample: title => `f.eks. “${title}”`,
}
