import { useMemo, useState } from 'react'
import { n, useT } from '../../i18n'

export type SourceOption = { slug: string; label: string; count: number }

interface Props {
  sources: readonly SourceOption[]
  selected: readonly string[]
  onToggle: (slug: string) => void
}

/** Search appears once the list is longer than can be scanned at a glance. */
const SEARCHABLE_FROM = 12

/**
 * The source list inside its popover: a checkbox per source, busiest first, searchable. Checkboxes
 * rather than the chips used elsewhere — in a vertical panel they state selection unambiguously and
 * come with keyboard behaviour for free.
 */
export function SourceFilter({ sources, selected, onToggle }: Props) {
  const t = useT('history')
  const [query, setQuery] = useState('')

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase()
    return q ? sources.filter((s) => s.label.toLowerCase().includes(q)) : sources
  }, [sources, query])

  return (
    <>
      {sources.length > SEARCHABLE_FROM && (
        <input
          className="input input--narrow"
          type="search"
          placeholder={t.filterSourceSearch}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      )}
      {shown.length === 0 && <p className="muted small">{t.filterNoSourceMatch}</p>}
      <div className="filter-pop__list">
        {shown.map(({ slug, label, count }) => (
          <label key={slug} className="filter-pop__option">
            <input
              type="checkbox"
              checked={selected.includes(slug)}
              onChange={() => onToggle(slug)}
            />
            <span>{label}</span>
            <span className="filter-pop__option-count">{n(count)}</span>
          </label>
        ))}
      </div>
    </>
  )
}
