import { useState } from 'react'
import type { DropReason, DroppedEntry, RunDetail } from '../../api/types'
import { dec, n, serverText, useT } from '../../i18n'

export function DroppedTab({ data }: { data: RunDetail }) {
  const t = useT('history')
  const [filter, setFilter] = useState<DropReason | 'all'>('all')
  if (!data.dropped) {
    return <div className="muted">{t.noDroppedRecorded}</div>
  }
  if (data.dropped.length === 0) {
    return <div className="muted">{t.nothingRemoved}</div>
  }

  const counts = data.dropped.reduce<Record<string, number>>((acc, d) => {
    acc[d.reason] = (acc[d.reason] ?? 0) + 1
    return acc
  }, {})

  const filtered = filter === 'all' ? data.dropped : data.dropped.filter(d => d.reason === filter)

  return (
    <section>
      <div className="dropped-filters">
        <button
          type="button"
          className={`chip ${filter === 'all' ? 'chip--active' : ''}`}
          onClick={() => setFilter('all')}
        >
          {t.dropFilterAll} <span className="tab__count">{n(data.dropped.length)}</span>
        </button>
        {(Object.keys(t.dropReason) as DropReason[]).map(r => (
          counts[r] ? (
            <button
              key={r}
              type="button"
              className={`chip ${filter === r ? 'chip--active' : ''}`}
              onClick={() => setFilter(r)}
            >
              {t.dropReason[r]} <span className="tab__count">{n(counts[r])}</span>
            </button>
          ) : null
        ))}
      </div>
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th>{t.colTitle}</th>
              <th>{t.colCompany}</th>
              <th>{t.colRating}</th>
              <th>{t.colReason}</th>
              <th>{t.colWhy}</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map(d => <DroppedRow key={d.id} entry={d} />)}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function DroppedRow({ entry }: { entry: DroppedEntry }) {
  const t = useT('history')
  const s = useT('server')
  return (
    <tr>
      <td>{entry.title}</td>
      <td>{entry.company ?? <span className="muted">—</span>}</td>
      <td className="tabular mono">{dec(entry.score, 2)}</td>
      <td><span className={`reason-badge reason-badge--${entry.reason}`}>{t.dropReason[entry.reason]}</span></td>
      <td className="muted">{serverText(s.dropContext, entry.reason, entry.contextArgs, entry.context ?? '')}</td>
    </tr>
  )
}
