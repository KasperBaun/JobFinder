import type { Messages } from '../en'

export const setup: Messages['setup'] = {
  eyebrow: 'førstegangsopsætning',
  step: (current, total) => `trin ${current} af ${total}`,

  welcomeHeading: () => <>Velkommen til <em>jobfinder</em></>,
  welcomeLede:
    'Vælg først, hvor jobfinder skal gemme dine data. Profil, jobkilder, markeringer og '
    + 'søgehistorik ligger i én mappe på din computer. Der oprettes intet, før du bekræfter.',

  languageLabel: 'Sprog',
  languageNote: 'Du kan altid skifte under Indstillinger.',

  emailLabel: 'Din e-mail',
  emailNote: 'Bruges kun til at navngive din datamappe — den sendes ingen steder hen.',
  dataDirLabel: 'Datamappe',
  dataDirNote: 'Vælg en anden mappe, hvis du vil.',
  acknowledge: 'Jeg forstår, at mine data bliver gemt i denne mappe på min computer.',
  rememberedIn: 'Dit valg bliver husket i',

  profileHeading: () => <>Sæt din <em>profil</em> op</>,
  profileLede: 'Det er den, jobfinder vurderer hvert opslag op mod. Kun det vigtigste nu — resten kan du finjustere senere.',
  cvLink: 'Har du et CV? Lad AI udfylde det.',

  nameLabel: 'Dit navn',
  namePlaceholder: 'Jens Jensen',
  locationLabel: 'Hvor du bor',
  locationPlaceholder: 'f.eks. København, Danmark',
  yearsLabel: 'Års erfaring',
  seniorityLabel: 'Erfaringsniveau',
  remoteLabel: 'Hvor du vil arbejde',
  rolesLabel: 'Roller, du ønsker',
  rolesPlaceholder: 'f.eks. Senior Backend Engineer',
  primaryStackLabel: 'Skal-have-kompetencer',
  primaryStackHint: '— dem et opslag bør nævne',
  primaryStackPlaceholder: 'f.eks. C#, .NET, Postgres',

  finish: 'Afslut opsætning',
  skip: 'Spring over — jeg udfylder det senere',
  saveFailed: 'Din profil kunne ikke gemmes.',
}
