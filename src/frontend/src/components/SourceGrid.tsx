import { Link } from 'react-router-dom'
import type { ProviderRowState } from '../utils/progress'

type Props = {
  rows: ProviderRowState[]
  runId: string
  linkable: boolean
}

function statusText(row: ProviderRowState): string {
  switch (row.status) {
    case 'ok': return String(row.fetchedCount ?? 0)
    case 'running': return 'running'
    case 'failed': return 'failed'
    default: return 'pending'
  }
}

// Dense responsive grid replacing the old one-row-per-source list: every source's status + count in
// ~one screen instead of five. Completed sources link through to their raw listings once the run
// succeeds; failed cells are tinted and show the error on hover.
export function SourceGrid({ rows, runId, linkable }: Props) {
  return (
    <div className="source-grid">
      {rows.map(row => {
        const isLink = linkable && row.status === 'ok'
        const cls = `source-cell source-cell--${row.status}${isLink ? ' source-cell--link' : ''}`
        const body = (
          <>
            <span className={`dot dot--${row.status}`} />
            <span className="source-cell__name">{row.name}</span>
            <span className="source-cell__count tabular mono">{statusText(row)}</span>
          </>
        )
        return isLink ? (
          <Link
            key={row.name}
            to={`/history/${runId}#tab=raw&provider=${encodeURIComponent(row.name)}`}
            className={cls}
            title={row.name}
          >
            {body}
          </Link>
        ) : (
          <div key={row.name} className={cls} title={row.status === 'failed' ? (row.error ?? 'failed') : row.name}>
            {body}
          </div>
        )
      })}
    </div>
  )
}
