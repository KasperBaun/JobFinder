import type { Messages } from '../en'

export const settings: Messages['settings'] = {
  eyebrow: 'indstillinger',
  title: 'Indstillinger',
  lede: 'Dit sprog og din profils dataplacering — og sikkerhedskopiér eller gendan det hele fra en fil.',

  languageTitle: 'Sprog',
  languageBody: 'Skifter sproget i hele brugerfladen. Din profil, dine jobkilder og dine søgeresultater påvirkes ikke.',
  languageLabel: 'Sprog i brugerfladen',
  languageSaved: 'Sproget er gemt.',

  activeProfileTitle: 'Aktiv profil',
  activeProfileBody:
    'Alt, hvad jobfinder ved, ligger i én mappe på denne computer. Skift til en anden e-mail '
    + 'eller mappe for at holde opsætninger adskilt.',
  email: 'E-mail',
  dataFolder: 'Datamappe',
  switchProfileCta: 'Skift profil…',
  switchProfile: 'Skift profil',
  switchConfirm: dir =>
    `Peg jobfinder på:\n\n${dir}\n\nDen læser din profil, dine jobkilder, dine markeringer og din `
    + 'historik derfra. Dine nuværende data bliver, hvor de er — skift tilbage når som helst ved at '
    + 'indtaste den gamle mappe igen.',
  switched: 'Profilen er skiftet — alle sider læser nu fra den nye mappe.',
  bothRequired: 'Både en e-mail og en datamappe er påkrævet.',

  exportTitle: 'Eksportér en sikkerhedskopi',
  exportBody: () => (
    <>
      Henter én <code>.zip</code> med alt i denne profil: dine jobkilder og deres indstillinger,
      din profil, dine gemte markeringer og hele din søgehistorik. Den store AI-model er ikke med
      — den hentes automatisk igen, når der er brug for den.
    </>
  ),
  exportWarning: '⚠ Filen indeholder eventuelle gemte adgangskoder og API-nøgler. Opbevar den et privat sted.',
  downloadBackup: 'Hent sikkerhedskopi',
  backupDownloaded: 'Sikkerhedskopien er hentet.',

  importTitle: 'Importér en sikkerhedskopi',
  importBody: () => (
    <>
      Gendanner en sikkerhedskopi, du tidligere har eksporteret. Dette <strong>erstatter</strong>{' '}
      data i denne profil. Bare rolig — de nuværende data bliver sikkerhedskopieret automatisk,
      før noget overskrives.
    </>
  ),
  importConfirm:
    'Import erstatter alt i denne profil — dine jobkilder, din profil, dine markeringer og din '
    + 'søgehistorik. Der gemmes automatisk en sikkerhedskopi af de nuværende data først. Fortsæt?',
  chooseBackupFile: 'Vælg sikkerhedskopi…',
  restored: (files, skipped) =>
    `Gendannede ${files} fil${files === 1 ? '' : 'er'}.`
    + (skipped > 0 ? ` (${skipped} element${skipped === 1 ? '' : 'er'} sprunget over)` : ''),
}
