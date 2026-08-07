import type { RunDetail } from '../../api/types'
import { LonglistTable } from '../../components/LonglistTable'
import type { LonglistFilters } from '../../components/longlist/filterState'
import type { LonglistSort } from '../../components/longlist/sortState'
import { useLonglistState } from '../../components/longlist/useLonglistState'

export function LonglistView({ data }: { data: RunDetail }) {
  const [state, setState] = useLonglistState()

  return (
    <LonglistTable
      data={data}
      filters={state.filters}
      sort={state.sort}
      onFiltersChange={(filters: LonglistFilters) => setState({ ...state, filters })}
      onSortChange={(sort: LonglistSort) => setState({ ...state, sort })}
    />
  )
}
