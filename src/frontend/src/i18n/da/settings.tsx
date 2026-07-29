import type { Messages } from '../en'

export const settings: Messages['settings'] = {
  eyebrow: 'indstillinger',
  title: 'Indstillinger',
  lede: 'Sprog, hvor dine data ligger, og sikkerhedskopiering af det hele.',

  languageTitle: 'Sprog',
  languageBody: 'Skifter sproget i hele brugerfladen. Din profil, dine jobkilder og dine resultater påvirkes ikke.',
  languageLabel: 'Sprog i brugerfladen',
  languageSaved: 'Sproget er gemt.',

  activeProfileTitle: 'Aktiv profil',
  activeProfileBody:
    'Alt, hvad jobfinder ved, ligger i én mappe på denne computer. Skift til en anden e-mail '
    + 'eller mappe for at holde flere opsætninger adskilt.',
  email: 'E-mail',
  dataFolder: 'Datamappe',
  switchProfileCta: 'Skift profil…',
  switchProfile: 'Skift profil',
  switchConfirm: dir =>
    `Peg jobfinder på:\n\n${dir}\n\nDin profil, dine jobkilder, markeringer og historik læses `
    + 'derfra. De nuværende data bliver, hvor de er — skift tilbage når som helst ved at indtaste '
    + 'den gamle mappe igen.',
  switched: 'Profilen er skiftet — alle sider læser nu fra den nye mappe.',
  bothRequired: 'Du skal angive både en e-mail og en datamappe.',

  exportTitle: 'Eksportér en sikkerhedskopi',
  exportBody: () => (
    <>
      Henter én <code>.zip</code> med alt i denne profil: dine jobkilder og deres indstillinger,
      din profil, dine markeringer og hele din søgehistorik. Den store AI-model er ikke med — den
      hentes automatisk igen, når der er brug for den.
    </>
  ),
  exportWarning: '⚠ Filen indeholder de adgangskoder og API-nøgler, du har gemt. Opbevar den et sikkert sted.',
  downloadBackup: 'Hent sikkerhedskopi',
  backupDownloaded: 'Sikkerhedskopien er hentet.',

  importTitle: 'Importér en sikkerhedskopi',
  importBody: () => (
    <>
      Gendanner en sikkerhedskopi, du har eksporteret tidligere. Det{' '}
      <strong>erstatter</strong> data i denne profil. Bare rolig — de nuværende data bliver
      sikkerhedskopieret automatisk først.
    </>
  ),
  importConfirm:
    'Import erstatter alt i denne profil — jobkilder, profil, markeringer og søgehistorik. '
    + 'Der gemmes automatisk en sikkerhedskopi af de nuværende data først. Fortsæt?',
  chooseBackupFile: 'Vælg sikkerhedskopi…',
  restored: (files, skipped) =>
    `Gendannede ${files} fil${files === 1 ? '' : 'er'}.`
    + (skipped > 0 ? ` (${skipped} sprunget over)` : ''),
}
