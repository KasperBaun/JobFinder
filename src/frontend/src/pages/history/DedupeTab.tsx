import type { RunDetail } from '../../api/types'
import { useT } from '../../i18n'

export function DedupeTab({ data }: { data: RunDetail }) {
  const t = useT('history')
  if (!data.dedupeMerges) {
    return <div className="muted">{t.noDedupeRecorded}</div>
  }
  if (data.dedupeMerges.length === 0) {
    return <div className="muted">{t.noDuplicatesMerged}</div>
  }
  // Build a lookup so we can show titles for canonical / merged listings.
  const titleById = new Map<string, string>()
  for (const r of data.raw ?? []) for (const l of r.listings) titleById.set(l.id, l.title)
  for (const s of data.scored ?? []) titleById.set(s.id, s.title)

  return (
    <section className="dedupe-list">
      {data.dedupeMerges.map(g => (
        <div key={g.canonicalId} className="dedupe-group">
          <div className="dedupe-group__canonical">
            <span className="dedupe-group__label">{t.dedupeKept}</span>
            <span className="dedupe-group__title">{titleById.get(g.canonicalId) ?? g.canonicalId}</span>
          </div>
          <div className="dedupe-group__merges">
            <span className="dedupe-group__label">{t.dedupeAlsoSeen(g.mergedFromIds.length)}</span>
            <ul>
              {/* A group can list the same merged-from id twice, so position disambiguates. */}
              {g.mergedFromIds.map((id, i) => (
                <li key={`${id}-${i}`}>
                  {titleById.get(id) ?? <code className="mono">{id.slice(0, 12)}…</code>}
                </li>
              ))}
            </ul>
          </div>
        </div>
      ))}
    </section>
  )
}
