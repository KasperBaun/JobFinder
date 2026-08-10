import { useState } from 'react'
import type { RunDetail } from '../../api/types'
import { dec, useT } from '../../i18n'

// The pairs arrive sorted strongest-first, so the preview is the part worth a human glance.
const POSSIBLE_PREVIEW = 30

export function DedupeTab({ data }: { data: RunDetail }) {
  const t = useT('history')
  const [showAllPossible, setShowAllPossible] = useState(false)
  const possible = data.possibleDuplicates ?? []
  const shownPossible = showAllPossible ? possible : possible.slice(0, POSSIBLE_PREVIEW)
  if (!data.dedupeMerges) {
    return <div className="muted">{t.noDedupeRecorded}</div>
  }
  if (data.dedupeMerges.length === 0 && possible.length === 0) {
    return <div className="muted">{t.noDuplicatesMerged}</div>
  }
  // Build lookups so we can show titles and sources for canonical / merged listings —
  // which portal each sighting came from is the point of auditing a merge.
  const titleById = new Map<string, string>()
  const sourceById = new Map<string, string>()
  for (const r of data.raw ?? []) {
    for (const l of r.listings) {
      titleById.set(l.id, l.title)
      sourceById.set(l.id, r.provider)
    }
  }
  for (const s of data.scored ?? []) {
    titleById.set(s.id, s.title)
    sourceById.set(s.id, s.portalDisplayName ?? s.portal)
  }

  const source = (id: string) => {
    const name = sourceById.get(id)
    return name ? <span className="dedupe-group__source">{name}</span> : null
  }
  const title = (id: string) =>
    titleById.get(id) ?? <code className="mono">{id.slice(0, 12)}…</code>

  return (
    <section className="dedupe-list">
      {data.dedupeMerges.map(g => (
        <div key={g.canonicalId} className="dedupe-group">
          <div className="dedupe-group__canonical">
            <span className="dedupe-group__label">{t.dedupeKept}</span>
            <span className="dedupe-group__title">
              {titleById.get(g.canonicalId) ?? g.canonicalId}
              {source(g.canonicalId)}
            </span>
          </div>
          <div className="dedupe-group__merges">
            <span className="dedupe-group__label">{t.dedupeAlsoSeen(g.mergedFromIds.length)}</span>
            <ul>
              {/* A group can list the same merged-from id twice, so position disambiguates. */}
              {g.mergedFromIds.map((id, i) => (
                <li key={`${id}-${i}`}>
                  {title(id)}
                  {source(id)}
                </li>
              ))}
            </ul>
          </div>
        </div>
      ))}

      {possible.length > 0 && (
        <div className="dedupe-possible">
          <h3 className="dedupe-possible__heading">{t.possibleHeading(possible.length)}</h3>
          <p className="dedupe-possible__intro muted">{t.possibleIntro}</p>
          <ul className="dedupe-possible__list">
            {shownPossible.map((p, i) => (
              <li key={`${p.keptId}-${p.candidateId}-${i}`} className="dedupe-possible__pair">
                <span className="dedupe-possible__side">{title(p.keptId)}{source(p.keptId)}</span>
                <span className="dedupe-possible__side">{title(p.candidateId)}{source(p.candidateId)}</span>
                <span className="dedupe-possible__prob">{t.possibleProbability(dec(p.probability, 2))}</span>
              </li>
            ))}
          </ul>
          {possible.length > POSSIBLE_PREVIEW && !showAllPossible && (
            <button type="button" className="btn btn--ghost" onClick={() => setShowAllPossible(true)}>
              {t.possibleShowAll(possible.length)}
            </button>
          )}
        </div>
      )}
    </section>
  )
}
