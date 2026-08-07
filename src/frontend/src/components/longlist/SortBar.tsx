import { useId } from 'react'
import { useT } from '../../i18n'
import { Pager } from './Pager'
import {
  DEFAULT_SORT,
  flipDir,
  isDefaultSort,
  isSortKey,
  SORT_KEYS,
  type LonglistSort,
} from './sortState'

interface Props {
  sort: LonglistSort
  onChange: (next: LonglistSort) => void
  shown: number
  total: number
  page: number
  pageCount: number
  size: number
  onPageChange: (page: number) => void
  onSizeChange: (size: number) => void
}

/**
 * Says what the table is ordered by, in words, and stays in view while you scroll it. The column
 * headers sort too, but they only ever show a glyph, they scroll sideways out of reach, and nothing
 * about a column title advertises that it is clickable.
 */
export function SortBar({ sort, onChange, shown, total, page, pageCount, size, onPageChange, onSizeChange }: Props) {
  const t = useT('history')
  const selectId = useId()
  const dirWord = sort.dir === 'desc' ? t.sortDirDesc : t.sortDirAsc

  return (
    <div className="longlist__sortbar" role="group" aria-label={t.sortBarAria}>
      <span className="longlist__sortbar-count">{t.sortCount(shown, total)}</span>

      <Pager page={page} pageCount={pageCount} size={size} onPageChange={onPageChange} onSizeChange={onSizeChange} />

      <div className="longlist__sortbar-group">
        <label className="longlist__sortbar-label" htmlFor={selectId}>{t.sortByLabel}</label>
        <select
          id={selectId}
          className="select select--inline"
          value={sort.key}
          onChange={(e) => isSortKey(e.target.value) && onChange({ key: e.target.value, dir: sort.dir })}
        >
          {SORT_KEYS.map((key) => (
            <option key={key} value={key}>{t.sortKey[key]}</option>
          ))}
        </select>
        <button
          type="button"
          className="sort-dir"
          onClick={() => onChange(flipDir(sort))}
          aria-label={t.sortDirToggleAria(dirWord)}
        >
          <span aria-hidden>{sort.dir === 'desc' ? '↓' : '↑'}</span>
        </button>
      </div>

      {!isDefaultSort(sort) && (
        <button type="button" className="link-button" onClick={() => onChange(DEFAULT_SORT)}>
          {t.resetSort}
        </button>
      )}
    </div>
  )
}
