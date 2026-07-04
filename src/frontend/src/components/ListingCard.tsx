import type { ListingMatch } from '../api/types'
import { MarkButton } from './MarkButton'

interface Props {
  match: ListingMatch
  runId: string
  mark?: 'good' | 'bad'
}

// Runs ranked before the favorite badge existed carry the boost as a sentence in
// the reasoning text instead of the favoriteCompany flag — detect it, show the
// badge, and keep it out of the prose.
const LEGACY_FAVORITE_NOTE = /One of your favorite companies \([^)]*\) — rating boosted\.\s*/

export function ListingCard({ match, runId, mark }: Props) {
  const subline = [match.company, match.location].filter(Boolean).join(' · ')
  const favorite = match.favoriteCompany || LEGACY_FAVORITE_NOTE.test(match.reasoning)
  const reasoning = match.reasoning.replace(LEGACY_FAVORITE_NOTE, '').trim()
  return (
    <article className="listing-card">
      <header className="listing-card__header">
        <div className="listing-card__title-block">
          <h3 className="listing-card__title">{match.title}</h3>
          {subline && <div className="listing-card__subline">{subline}</div>}
        </div>
        <div className="listing-card__badges">
          {favorite && <span className="badge badge--fav" title="One of your favorite companies — rating boosted">★ Favorite</span>}
          <span className="badge badge--score">{match.score.toFixed(2)}</span>
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
        <MarkButton runId={runId} listingId={match.id} current={mark} />
        <a href={match.url} target="_blank" rel="noreferrer" className="btn btn--primary">
          Open job posting →
        </a>
      </footer>
    </article>
  )
}
