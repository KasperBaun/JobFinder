import type { JobSearchEvent } from '../api/types'
import { formatRelative } from '../utils/time'

type Props = { events: JobSearchEvent[] }

// The full per-event timeline, demoted into a collapsed disclosure (default closed) so the play-by-
// play is available for debugging without dominating the card — the source grid + summary already
// show every source's count and status.
export function ActivityLog({ events }: Props) {
  if (events.length === 0) return null
  return (
    <details className="activity-log">
      <summary className="activity-log__summary">Activity log ({events.length} events)</summary>
      <div className="activity-log__scroll">
        <ol className="timeline">
          {events.map((ev, i) => (
            <li key={i} className={`timeline__item timeline__item--${ev.level}`}>
              <span className="timeline__time tabular mono">{formatRelative(ev.timestamp)}</span>
              <span className="timeline__msg">{ev.message}</span>
            </li>
          ))}
        </ol>
      </div>
    </details>
  )
}
