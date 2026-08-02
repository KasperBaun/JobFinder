import type { Messages } from '../en'
import { n } from '../format'

export const applications: Messages['applications'] = {
  eyebrow: '05 / ansøgninger',
  heading: () => <>Dine <em>ansøgninger</em></>,
  lede:
    'Alle de job, du følger — på tværs af søgninger. Samtaler og tilbud lærer AI’en, hvordan et '
    + 'stærkt match ser ud.',
  loading: 'Indlæser ansøgninger…',
  loadFailed: 'Ansøgningerne kunne ikke indlæses.',

  emptyPrefix: 'Du følger ikke noget endnu. Sæt en status — f.eks.',
  emptyStatus: 'Ansøgt',
  emptyMiddle: '— på et job i en',
  emptyLink: 'tidligere søgning',
  emptySuffix: 'for at følge det her.',

  colTitle: 'Titel',
  colCompany: 'Virksomhed',
  colSource: 'Jobkilde',
  colRating: 'Score',
  colStatus: 'Status',
  colStatusSet: 'Status sat',
  colYourRating: 'Din vurdering',
  colFromSearch: 'Fra søgning',

  tilesLabel: 'Ansøgninger pr. status',
  filterAll: 'Alle',
  filterNoMatch: 'Ingen ansøgninger med denne status.',
  filterReset: 'Vis alle',

  waiting: (days: number) => (days === 1 ? 'venter 1 dag' : `venter ${n(days)} dage`),
  waitingTitle: 'Ansøgt uden svar endnu — overvej at følge op.',
}
