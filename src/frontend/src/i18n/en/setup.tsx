export const setup = {
  eyebrow: 'first-time setup',
  step: (current: number, total: number) => `step ${current} of ${total}`,

  welcomeHeading: () => <>Welcome to <em>jobfinder</em></>,
  welcomeLede:
    'First, choose where jobfinder should keep your data on this computer — your profile, job '
    + 'sites, marks, and search history all live in one folder that stays on your machine. '
    + 'Nothing is created until you confirm.',

  languageLabel: 'Language',
  languageNote: 'You can change this later in Settings.',

  emailLabel: 'Your email',
  emailNote: 'Just a label for your data folder — never sent anywhere.',
  dataDirLabel: 'Data folder',
  dataDirNote: 'Change it to any folder you like.',
  acknowledge: 'I understand my data will be stored in this folder on my computer.',
  rememberedIn: 'Your choice is remembered in',

  profileHeading: () => <>Set up your <em>profile</em></>,
  profileLede: 'This is what jobfinder rates every listing against. Just the essentials for now — you can fine-tune everything later on the Profile page.',
  cvLink: 'Have a CV? Let AI fill this in.',

  nameLabel: 'Your name',
  namePlaceholder: 'Jane Doe',
  locationLabel: 'Where you’re based',
  locationPlaceholder: 'e.g. Copenhagen, Denmark',
  yearsLabel: 'Years of experience',
  seniorityLabel: 'Experience level',
  remoteLabel: 'Where you want to work',
  rolesLabel: 'Roles you want',
  rolesPlaceholder: 'e.g. Senior Backend Engineer',
  primaryStackLabel: 'Must-have skills',
  primaryStackHint: '— the ones a job should mention',
  primaryStackPlaceholder: 'e.g. C#, .NET, Postgres',

  finish: 'Finish setup',
  skip: 'Skip for now — I’ll fill this in later',
  saveFailed: 'Could not save your profile.',
}
