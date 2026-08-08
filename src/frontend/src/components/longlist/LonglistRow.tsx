import { useState } from 'react'
import type { ApplicationStatus, ScoredEntry } from '../../api/types'
import { BreakdownBar, BreakdownDetail } from '../BreakdownBar'
import { MarkButton } from '../MarkButton'
import { StatusSelect } from '../StatusSelect'
import { formatRelative } from '../../utils/time'
import { dec, useT } from '../../i18n'

interface Props {
  entry: ScoredEntry
  runId: string
  mark?: 'good' | 'bad'
  markReason?: string
  markStatus?: ApplicationStatus
}

export function LonglistRow({ entry, runId, mark, markReason, markStatus }: Props) {
  const t = useT('history')
  const [open, setOpen] = useState(false)
  return (
    <>
      <tr>
        <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
        <td>{entry.company ?? <span className="muted">—</span>}</td>
        <td><span className="longlist__portal">{entry.portalDisplayName ?? entry.portal}</span></td>
        <td>{entry.location ?? <span className="muted">—</span>}</td>
        <td className="tabular mono">
          {entry.postedAt ? formatRelative(entry.postedAt) : <span className="muted">—</span>}
        </td>
        <td className="tabular mono">
          <div className="longlist__score-cell">
            <span>{dec(entry.score, 2)}</span>
            <BreakdownBar b={entry.breakdown} score={entry.score} />
          </div>
        </td>
        <td>
          <div className="longlist__mark-cell">
            <MarkButton runId={runId} listingId={entry.id} current={mark} reason={markReason} compact />
            <StatusSelect runId={runId} listingId={entry.id} current={markStatus} compact />
          </div>
        </td>
        <td>
          <button type="button" className="link-button" onClick={() => setOpen(!open)} aria-label={open ? t.collapse : t.expand}>
            {open ? '▾' : '▸'}
          </button>
        </td>
      </tr>
      {open && (
        <tr className="longlist__expanded">
          <td colSpan={8}>
            <BreakdownDetail entry={entry} />
          </td>
        </tr>
      )}
    </>
  )
}
