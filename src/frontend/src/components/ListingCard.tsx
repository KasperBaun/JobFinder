import type { ApplicationStatus, ListingMatch } from '../api/types'
import { MarkButton } from './MarkButton'
import { PrintListingButton } from './PrintListingButton'
import { StatusSelect } from './StatusSelect'
import { dec, serverText, useT } from '../i18n'

interface Props {
  match: ListingMatch
  runId: string
  mark?: 'good' | 'bad'
  markReason?: string
  markStatus?: ApplicationStatus
}

// Runs ranked before the favorite badge existed carry the boost as a sentence in
// the reasoning text instead of the favoriteCompany flag — detect it, show the
// badge, and keep it out of the prose.
const LEGACY_FAVORITE_NOTE = /One of your favorite companies \([^)]*\) — rating boosted\.\s*/

export function ListingCard({ match, runId, mark, markReason, markStatus }: Props) {
  const t = useT('listing')
  const s = useT('server')
  const subline = [match.company, match.location].filter(Boolean).join(' · ')
  const favorite = match.favoriteCompany || LEGACY_FAVORITE_NOTE.test(match.reasoning)
  // Runs ranked before the notes were structured carry only prose, so fall back to it (and to the
  // legacy-badge strip). Newer runs re-render in whatever language is active, including old history.
  const reasoning = match.reasoningNotes
    ? match.reasoningNotes.map(note => serverText(s.reasoning, note.key, note.args, '')).filter(Boolean).join(' ')
    : match.reasoning.replace(LEGACY_FAVORITE_NOTE, '').trim()
  return (
    <article className="listing-card">
      <header className="listing-card__header">
        <div className="listing-card__title-block">
          <h3 className="listing-card__title">{match.title}</h3>
          {subline && <div className="listing-card__subline">{subline}</div>}
        </div>
        <div className="listing-card__badges">
          {favorite && <span className="badge badge--fav" title={t.favoriteTitle}>{t.favoriteBadge}</span>}
          <span className="badge badge--score">{dec(match.score, 2)}</span>
          {match.remoteMode && match.remoteMode !== 'unknown' && <span className="badge">{match.remoteMode}</span>}
          <span className="badge badge--muted">{match.portalDisplayName ?? match.portal}</span>
        </div>
      </header>

      {reasoning && <p className="listing-card__reasoning">{reasoning}</p>}

      {(match.primaryStackHits.length > 0 || match.secondaryStackHits.length > 0) && (
        <div className="listing-card__pills">
          {match.primaryStackHits.map(p => (
            <span key={`p-${p}`} className="pill pill--primary">{p}</span>
          ))}
          {match.secondaryStackHits.map(p => (
            <span key={`s-${p}`} className="pill pill--secondary">{p}</span>
          ))}
        </div>
      )}

      <footer className="listing-card__footer">
        <MarkButton runId={runId} listingId={match.id} current={mark} reason={markReason} />
        <StatusSelect runId={runId} listingId={match.id} current={markStatus} />
        <PrintListingButton match={match} />
        <a href={match.url} target="_blank" rel="noreferrer" className="btn btn--primary">
          {t.openPosting}
        </a>
      </footer>
    </article>
  )
}
