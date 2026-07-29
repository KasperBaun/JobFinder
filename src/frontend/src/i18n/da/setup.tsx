import type { Messages } from '../en'

export const setup: Messages['setup'] = {
  eyebrow: 'førstegangsopsætning',
  step: (current, total) => `trin ${current} af ${total}`,

  welcomeHeading: () => <>Velkommen til <em>jobfinder</em></>,
  welcomeLede:
    'Vælg først, hvor jobfinder skal gemme dine data på denne computer — din profil, dine '
    + 'jobkilder, dine markeringer og din søgehistorik ligger alle i én mappe, der bliver på din '
    + 'maskine. Der bliver ikke oprettet noget, før du bekræfter.',

  languageLabel: 'Sprog',
  languageNote: 'Du kan ændre det senere under Indstillinger.',

  emailLabel: 'Din e-mail',
  emailNote: 'Kun en etiket til din datamappe — sendes aldrig nogen steder hen.',
  dataDirLabel: 'Datamappe',
  dataDirNote: 'Skift den til en hvilken som helst mappe, du vil.',
  acknowledge: 'Jeg forstår, at mine data bliver gemt i denne mappe på min computer.',
  rememberedIn: 'Dit valg bliver husket i',

  profileHeading: () => <>Sæt din <em>profil</em> op</>,
  profileLede: 'Det er den, jobfinder vurderer hvert jobopslag op mod. Kun det vigtigste for nu — du kan finjustere alt senere på profilsiden.',
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
  primaryStackHint: '— dem et jobopslag bør nævne',
  primaryStackPlaceholder: 'f.eks. C#, .NET, Postgres',

  finish: 'Afslut opsætning',
  skip: 'Spring over — jeg udfylder det senere',
  saveFailed: 'Din profil kunne ikke gemmes.',
}
