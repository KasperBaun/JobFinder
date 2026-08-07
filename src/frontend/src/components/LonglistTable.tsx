import { useMemo } from 'react'
import type { RunDetail, ScoredEntry } from '../api/types'
import { DEFAULT_FILTERS, filterRows, type LonglistFilters } from './longlist/filterState'
import { LonglistRow } from './longlist/LonglistRow'
import { SortBar } from './longlist/SortBar'
import { SortableHeader } from './longlist/SortableHeader'
import { sortRows } from './longlist/sortRows'
import { toggleSort, type LonglistSort, type SortKey } from './longlist/sortState'
import { useT } from '../i18n'

interface Props {
  data: RunDetail
  filters: LonglistFilters
  sort: LonglistSort
  onFiltersChange: (next: LonglistFilters) => void
  onSortChange: (next: LonglistSort) => void
}

export function LonglistTable(props: Props) {
  const t = useT('history')
  // The guard has to sit in front of a component that owns no hooks: `scored` is absent on legacy
  // runs and arrives mid-poll on a running one, and returning early past a hook list changes its
  // length between renders.
  if (!props.data.scored) return <div className="muted">{t.noRatingsRecorded}</div>
  return <LonglistBody {...props} scored={props.data.scored} />
}

function LonglistBody({
  data, scored, filters, sort, onFiltersChange, onSortChange,
}: Props & { scored: ScoredEntry[] }) {
  const t = useT('history')

  // Filtering and sorting are memoised apart, so changing only the sort skips a full filter pass
  // over every listing in the run.
  const filtered = useMemo(
    () => filterRows(scored, filters, data.marks),
    [scored, filters, data.marks],
  )
  const rows = useMemo(() => sortRows(filtered, sort, data.marks), [filtered, sort, data.marks])

  const dirFor = (key: SortKey) => (sort.key === key ? sort.dir : undefined)
  const header = (key: SortKey, label: string) => (
    <SortableHeader dir={dirFor(key)} onActivate={() => onSortChange(toggleSort(sort, key))}>
      {label}
    </SortableHeader>
  )

  // The filter bar is not rendered here: it lives up in the run-detail toolbar, on the same row
  // as the view switcher, and reaches this table through the shared hash state.
  return (
    <section className="longlist">
      <SortBar sort={sort} onChange={onSortChange} shown={rows.length} total={scored.length} />
      <div className="table-wrap">
        <table className="table longlist__table" aria-label={t.longlistTableAria}>
          <thead>
            <tr>
              {header('title', t.colTitle)}
              {header('company', t.colCompany)}
              {header('portal', t.colSource)}
              {header('location', t.colLocation)}
              {header('posted', t.colPosted)}
              {header('score', t.colRating)}
              {header('mark', t.colYourRating)}
              <th scope="col" aria-label={t.expand}></th>
            </tr>
          </thead>
          <tbody>
            {rows.map((s) => (
              <LonglistRow
                key={s.id}
                entry={s}
                runId={data.runId}
                mark={data.marks[s.id]}
                markReason={data.markReasons?.[s.id]}
                markStatus={data.markStatuses?.[s.id]}
              />
            ))}
          </tbody>
        </table>
        {rows.length === 0 && (
          <div className="muted longlist__empty">
            {t.noJobsMatchFilters}{' '}
            <button type="button" className="link-button" onClick={() => onFiltersChange(DEFAULT_FILTERS)}>
              {t.reset}
            </button>
          </div>
        )}
      </div>
    </section>
  )
}
