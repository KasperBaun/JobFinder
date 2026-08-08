import { useRef, useState } from 'react'
import type { DropReason, DroppedEntry, RunDetail } from '../../api/types'
import { Pager } from '../../components/longlist/Pager'
import { DEFAULT_PAGE_SIZE } from '../../components/longlist/filterState'
import { dec, n, serverText, useT } from '../../i18n'

export function DroppedTab({ data }: { data: RunDetail }) {
  const t = useT('history')
  const [filter, setFilter] = useState<DropReason | 'all'>('all')
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(DEFAULT_PAGE_SIZE)
  const sectionRef = useRef<HTMLElement>(null)
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

  // Paged like the all-rated table: clamped rather than navigated, and re-anchored
  // at the top on a page change so the rows don't swap underneath the reader.
  const pageCount = Math.max(1, Math.ceil(filtered.length / size))
  const current = Math.min(page, pageCount)
  const paged = filtered.slice((current - 1) * size, current * size)

  const changeFilter = (next: DropReason | 'all') => {
    setFilter(next)
    setPage(1)
  }
  const changePage = (next: number) => {
    setPage(next)
    // Optional call: jsdom has no scrollIntoView.
    sectionRef.current?.scrollIntoView?.({ block: 'start' })
  }
  const changeSize = (next: number) => {
    setSize(next)
    setPage(1)
  }

  return (
    <section ref={sectionRef}>
      <div className="dropped-filters">
        <button
          type="button"
          className={`chip ${filter === 'all' ? 'chip--active' : ''}`}
          onClick={() => changeFilter('all')}
        >
          {t.dropFilterAll} <span className="tab__count">{n(data.dropped.length)}</span>
        </button>
        {(Object.keys(t.dropReason) as DropReason[]).map(r => (
          counts[r] ? (
            <button
              key={r}
              type="button"
              className={`chip ${filter === r ? 'chip--active' : ''}`}
              onClick={() => changeFilter(r)}
            >
              {t.dropReason[r]} <span className="tab__count">{n(counts[r])}</span>
            </button>
          ) : null
        ))}
      </div>
      <div className="longlist__sortbar">
        <span className="longlist__sortbar-count">{t.sortCount(filtered.length, data.dropped.length)}</span>
        <Pager page={current} pageCount={pageCount} size={size} onPageChange={changePage} onSizeChange={changeSize} />
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
            {paged.map(d => <DroppedRow key={d.id} entry={d} />)}
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
