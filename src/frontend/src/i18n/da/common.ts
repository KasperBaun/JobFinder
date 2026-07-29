import type { Messages } from '../en'

export const common: Messages['common'] = {
  emDash: '—',
  justNow: 'lige nu',
  units: { ms: 'ms', s: 's', m: 'm' },

  save: 'Gem',
  saving: 'Gemmer…',
  cancel: 'Annullér',
  close: 'Luk',
  continue: 'Fortsæt',
  back: 'Tilbage',
  retry: 'Prøv igen',
  remove: 'Fjern',
  delete: 'Slet',
  loading: 'Indlæser…',
  none: 'Ingen',
  unknown: 'Ukendt',
  enabled: 'Slået til',
  disabled: 'Slået fra',

  unsavedChanges: 'Ugemte ændringer',
  revert: 'Fortryd',
  saveChanges: 'Gem ændringer',
  typeAndPressEnter: 'Skriv og tryk Enter',
  removeValue: value => `Fjern ${value}`,

  serverDisconnectedTitle: 'Forbindelsen er afbrudt',
  serverDisconnectedBody: 'Jobfinder kører ikke længere. Du kan lukke denne fane.',
  goodbyeTitle: 'Farvel',
  goodbyeBody: 'Jobfinder er stoppet. Du kan lukke denne fane.',
}
