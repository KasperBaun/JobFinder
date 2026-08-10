import type { RunDetail } from '../../api/types'
import { LonglistTable } from '../../components/LonglistTable'
import {
  withFilters,
  withPage,
  withPageSize,
  withSort,
} from '../../components/longlist/filterState'
import { useLonglistState } from '../../components/longlist/useLonglistState'

export function LonglistView({ data }: { data: RunDetail }) {
  const [state, setState] = useLonglistState()

  return (
    <LonglistTable
      data={data}
      filters={state.filters}
      sort={state.sort}
      page={state.page}
      size={state.size}
      onFiltersChange={(filters) => setState(withFilters(state, filters))}
      onSortChange={(sort) => setState(withSort(state, sort))}
      onPageChange={(page) => setState(withPage(state, page))}
      onSizeChange={(size) => setState(withPageSize(state, size))}
    />
  )
}
