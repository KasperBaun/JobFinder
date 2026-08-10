import { useT } from '../../i18n'
import type { LonglistFilters } from './filterState'

/** The free-text filter, leading the run toolbar: the most-reached-for control sits leftmost. */
export function SearchField({
  filters,
  onChange,
}: {
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
}) {
  const t = useT('history')
  return (
    <input
      className="input longlist__search"
      type="search"
      placeholder={t.searchTitleOrCompany}
      value={filters.q}
      onChange={(e) => onChange({ ...filters, q: e.target.value })}
      onKeyDown={(e) => { if (e.key === 'Escape') onChange({ ...filters, q: '' }) }}
    />
  )
}
