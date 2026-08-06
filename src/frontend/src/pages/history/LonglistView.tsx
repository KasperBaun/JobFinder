import { useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import type { RunDetail } from '../../api/types'
import { LonglistTable } from '../../components/LonglistTable'
import {
  decodeFromHash,
  encodeToHash,
  type LonglistFilters,
  type LonglistState,
} from '../../components/longlist/filterState'
import type { LonglistSort } from '../../components/longlist/sortState'

export function LonglistView({ data }: { data: RunDetail }) {
  const navigate = useNavigate()
  const location = useLocation()

  const state = useMemo(() => {
    const params = new URLSearchParams(location.hash.startsWith('#') ? location.hash.slice(1) : location.hash)
    return decodeFromHash(params)
  }, [location.hash])

  const shortlistIds = useMemo(() => new Set(data.shortlist.map((m) => m.id)), [data.shortlist])

  // Filters and sort travel in one hash but change independently, so each setter carries the other
  // through untouched — resetting the filters must not silently reorder the table.
  const setState = (next: LonglistState) =>
    navigate(`${location.pathname}#${encodeToHash(next).toString()}`, { replace: true })

  return (
    <LonglistTable
      data={data}
      filters={state.filters}
      sort={state.sort}
      onFiltersChange={(filters: LonglistFilters) => setState({ ...state, filters })}
      onSortChange={(sort: LonglistSort) => setState({ ...state, sort })}
      shortlistIds={shortlistIds}
    />
  )
}
