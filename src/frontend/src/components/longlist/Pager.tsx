import { useId } from 'react'
import { n, useT } from '../../i18n'
import { PAGE_SIZES } from './filterState'

interface Props {
  page: number
  pageCount: number
  size: number
  onPageChange: (page: number) => void
  onSizeChange: (size: number) => void
}

/**
 * The slice control in the middle of the sort bar: how many rows per page, and which page.
 * Rendering every one of a run's 2 000 rated listings cost more DOM than any window shows;
 * the pager keeps the table a screenful while the count on the left still states the whole.
 */
export function Pager({ page, pageCount, size, onPageChange, onSizeChange }: Props) {
  const t = useT('history')
  const selectId = useId()

  return (
    <div className="longlist__pager">
      <label className="longlist__sortbar-label" htmlFor={selectId}>{t.perPage}</label>
      <select
        id={selectId}
        className="select select--inline select--pager"
        value={size}
        onChange={(e) => onSizeChange(Number(e.target.value))}
      >
        {PAGE_SIZES.map((s) => (
          <option key={s} value={s}>{n(s)}</option>
        ))}
      </select>
      <button
        type="button"
        className="sort-dir"
        disabled={page <= 1}
        aria-label={t.prevPageAria}
        onClick={() => onPageChange(page - 1)}
      >
        <span aria-hidden>‹</span>
      </button>
      <span className="longlist__pager-status">{t.pageOf(n(page), n(pageCount))}</span>
      <button
        type="button"
        className="sort-dir"
        disabled={page >= pageCount}
        aria-label={t.nextPageAria}
        onClick={() => onPageChange(page + 1)}
      >
        <span aria-hidden>›</span>
      </button>
    </div>
  )
}
