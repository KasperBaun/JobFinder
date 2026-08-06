import { useT } from '../../i18n'
import { countTone, FILTER_KEYS, type FilterCounts, type FilterKey } from './filters'

interface Props {
  query: string
  onQueryChange: (q: string) => void
  filter: FilterKey
  onFilterChange: (f: FilterKey) => void
  counts: FilterCounts
}

export function ProviderToolbar({ query, onQueryChange, filter, onFilterChange, counts }: Props) {
  const t = useT('providers')
  return (
    <div className="provider-toolbar">
      <input
        type="search"
        className="input provider-toolbar__search"
        placeholder={t.searchPlaceholder}
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        aria-label={t.searchAria}
      />
      <div className="provider-toolbar__filters" role="group" aria-label={t.filterAria}>
        {FILTER_KEYS.map((key) => {
          const tone = countTone(key, counts)
          return (
            <button
              key={key}
              type="button"
              className={filter === key ? 'chip chip--active' : 'chip'}
              onClick={() => onFilterChange(key)}
              aria-pressed={filter === key}
            >
              {t.filter[key]}{' '}
              <span
                className={`provider-toolbar__count${tone ? ` provider-toolbar__count--${tone}` : ''}`}
              >
                {counts[key]}
              </span>
            </button>
          )
        })}
      </div>
    </div>
  )
}
