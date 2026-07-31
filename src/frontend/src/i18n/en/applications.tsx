import { n } from '../format'

export const applications = {
  eyebrow: '05 / applications',
  heading: () => <>Your <em>applications</em></>,
  lede:
    'Every job you’ve tracked, across all searches. Interviews and offers teach the AI what a '
    + 'strong fit looks like.',
  loading: 'Loading applications…',
  loadFailed: 'Failed to load applications.',

  emptyPrefix: 'Nothing tracked yet. Set a status — like',
  emptyStatus: 'Applied',
  emptyMiddle: '— on any job in a',
  emptyLink: 'past search',
  emptySuffix: 'to start tracking it here.',

  colTitle: 'Title',
  colCompany: 'Company',
  colSource: 'Source',
  colRating: 'Rating',
  colStatus: 'Status',
  colStatusSet: 'Status set',
  colYourRating: 'Your rating',
  colFromSearch: 'From search',

  tilesLabel: 'Applications by status',
  filterAll: 'All',
  filterNoMatch: 'No applications with this status.',
  filterReset: 'Show all',

  waiting: (days: number) => (days === 1 ? 'waiting 1 day' : `waiting ${n(days)} days`),
  waitingTitle: 'Applied with no response yet — consider following up.',
}
