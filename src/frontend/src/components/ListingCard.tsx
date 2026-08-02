import { useState } from 'react'
import type { ApplicationStatus, ListingMatch, ScoredEntry } from '../api/types'
import { BreakdownDetail } from './BreakdownBar'
import { MarkButton } from './MarkButton'
import { PrintListingButton } from './PrintListingButton'
import { AiVerdict, ReasoningFacts } from './ReasoningFacts'
import { StatusSelect } from './StatusSelect'
import { dec, useT } from '../i18n'

interface Props {
  match: ListingMatch
  runId: string
  mark?: 'good' | 'bad'
  markReason?: string
  markStatus?: ApplicationStatus
  /** The listing's scored entry from the same run — makes the score badge expandable. */
  breakdownEntry?: ScoredEntry
}

// Runs ranked before the favorite badge existed carry the boost as a sentence in
// the reasoning text instead of the favoriteCompany flag — detect it, show the
// badge, and keep it out of the prose.
const LEGACY_FAVORITE_NOTE = /One of your favorite companies \([^)]*\) — rating boosted\.\s*/

export function ListingCard({ match, runId, mark, markReason, markStatus, breakdownEntry }: Props) {
  const t = useT('listing')
  const [showBreakdown, setShowBreakdown] = useState(false)
  const favorite = match.favoriteCompany || LEGACY_FAVORITE_NOTE.test(match.reasoning)
  // Runs ranked before the notes were structured carry only prose, so fall back to it (and to the
  // legacy-badge strip). Newer runs render the structured fact list in whatever language is active.
  const legacyReasoning = match.reasoningNotes ? null : match.reasoning.replace(LEGACY_FAVORITE_NOTE, '').trim()
  return (
    <article className="listing-card">
      <header className="listing-card__header">
        <div className="listing-card__title-block">
          <h3 className="listing-card__title">{match.title}</h3>
          {/* Location and remote mode live in the fact rows — the subline is the employer alone. */}
          {match.company && <div className="listing-card__subline">{match.company}</div>}
        </div>
        <div className="listing-card__badges">
          {favorite && <span className="badge badge--fav" title={t.favoriteTitle}>{t.favoriteBadge}</span>}
          {breakdownEntry ? (
            <button
              type="button"
              className="badge badge--score badge--clickable"
              onClick={() => setShowBreakdown(v => !v)}
              title={t.breakdownToggle}
              aria-expanded={showBreakdown}
            >
              {dec(match.score, 2)} ▾
            </button>
          ) : (
            <span className="badge badge--score">{dec(match.score, 2)}</span>
          )}
          <span className="badge badge--muted">{match.portalDisplayName ?? match.portal}</span>
        </div>
      </header>

      {showBreakdown && breakdownEntry && (
        <div className="listing-card__breakdown">
          <BreakdownDetail entry={breakdownEntry} />
        </div>
      )}

      {match.reasoningNotes && <AiVerdict match={match} />}
      {match.reasoningNotes && <ReasoningFacts match={match} />}
      {legacyReasoning && <p className="listing-card__reasoning">{legacyReasoning}</p>}

      {/* The fact list renders skill pills inline; the standalone pill strip remains only for
          runs recorded before the notes were structured. */}
      {!match.reasoningNotes && (match.primaryStackHits.length > 0 || match.secondaryStackHits.length > 0) && (
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
