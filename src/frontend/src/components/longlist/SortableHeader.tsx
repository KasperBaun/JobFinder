type Direction = 'asc' | 'desc'

interface Props {
  /** Undefined when this column isn't the one being sorted. */
  dir?: Direction
  onActivate: () => void
  children: React.ReactNode
}

const GLYPH: Record<Direction, string> = { asc: '↑', desc: '↓' }

/**
 * A column header you can actually reach. The clickable target is a real `<button>`, so it takes
 * focus and Enter/Space for free — a `<th onClick>` is reachable by mouse only — and `aria-sort`
 * on the cell is what a screen reader reports, which is why no live region is needed.
 */
export function SortableHeader({ dir, onActivate, children }: Props) {
  return (
    <th
      scope="col"
      className={`sortable ${dir ? 'sortable--active' : ''}`}
      aria-sort={dir === undefined ? undefined : dir === 'desc' ? 'descending' : 'ascending'}
    >
      <button type="button" className="sort-header" onClick={onActivate}>
        {children}
        {/* Ghost ↕ until this column is the active one — it teaches that the header is a control
            before the first click, which nothing in the old header did. */}
        <span className="sort-header__glyph" aria-hidden>{dir ? GLYPH[dir] : '↕'}</span>
      </button>
    </th>
  )
}
