import { useId } from 'react'
import type { SourceOverlap } from '../../api/types'
import { useT } from '../../i18n'

/**
 * Shown once a candidate has actually been fetched, when its jobs turn out to be jobs the user
 * already gets. A full match is a decision to make — hence the way out to the source they already
 * have; a partial one is just something to know before adding.
 */
export function SourceOverlapNotice({
  overlap,
  fetchedCount,
  onOpenExisting,
}: {
  overlap: SourceOverlap
  fetchedCount: number
  onOpenExisting: (overlap: SourceOverlap) => void
}) {
  const t = useT('sources')
  const titleId = useId()

  if (!overlap.duplicate) {
    return (
      <p className="add-source__warn">
        {t.overlapBody(overlap.displayName, overlap.sharedCount, fetchedCount)}
      </p>
    )
  }

  return (
    <div className="add-source__duplicate" role="group" aria-labelledby={titleId}>
      <h3 id={titleId} className="add-source__duplicate-title">{t.duplicateTitle}</h3>
      <p className="add-source__duplicate-body">
        {t.duplicateBody(overlap.displayName, overlap.sharedCount, fetchedCount)}
      </p>
      <button
        type="button"
        className="btn btn--primary btn--sm"
        onClick={() => onOpenExisting(overlap)}
      >
        {t.openExisting(overlap.displayName)}
      </button>
    </div>
  )
}
