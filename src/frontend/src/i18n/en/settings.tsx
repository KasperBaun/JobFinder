export const settings = {
  eyebrow: 'settings',
  title: 'Settings',
  lede: 'Your interface language and profile’s data location, and backup or restore everything to a file.',

  languageTitle: 'Language',
  languageBody: 'Changes the interface language everywhere. Your profile, job sites and search results are unaffected.',
  languageLabel: 'Interface language',
  languageSaved: 'Language saved.',

  activeProfileTitle: 'Active profile',
  activeProfileBody:
    'Everything jobfinder knows lives in one folder on this computer. Switch to a different email '
    + 'or folder to keep separate setups.',
  email: 'Email',
  dataFolder: 'Data folder',
  switchProfileCta: 'Switch profile…',
  switchProfile: 'Switch profile',
  switchConfirm: (dir: string) =>
    `Point jobfinder at:\n\n${dir}\n\nIt will read your profile, job sites, marks and history from `
    + 'there. Your current data stays where it is — switch back any time by entering the old '
    + 'folder again.',
  switched: 'Switched profile — every page now reads from the new folder.',
  bothRequired: 'Both an email and a data folder are required.',

  exportTitle: 'Export a backup',
  exportBody: () => (
    <>
      Downloads a single <code>.zip</code> with everything in this profile: your job sites and their
      settings, your profile, your saved marks, and your full search history. The large AI model
      isn’t included — it re-downloads automatically when needed.
    </>
  ),
  exportWarning: '⚠ The file includes any saved site passwords / API keys. Keep it somewhere private.',
  downloadBackup: 'Download backup',
  backupDownloaded: 'Backup downloaded.',

  importTitle: 'Import a backup',
  importBody: () => (
    <>
      Restores a backup file you exported earlier. This <strong>replaces</strong> the data in this
      profile. Don’t worry — the current data is backed up automatically before anything is
      overwritten.
    </>
  ),
  importConfirm:
    'Importing replaces everything currently in this profile — your job sites, profile, marks, '
    + 'and search history. A backup of the current data is saved automatically first. Continue?',
  chooseBackupFile: 'Choose backup file…',
  restored: (files: number, skipped: number) =>
    `Restored ${files} file${files === 1 ? '' : 's'}.`
    + (skipped > 0 ? ` (${skipped} item${skipped === 1 ? '' : 's'} skipped)` : ''),
}
