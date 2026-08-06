import { useEffect, useId, useRef } from 'react'
import { n, useT } from '../../i18n'

interface Props {
  label: string
  /** The chosen value shown on the trigger, e.g. "7d" or "0,20–0,80". Undefined when unfiltered. */
  summary?: string
  /** How many values are selected, for the multi-select groups. */
  count?: number
  open: boolean
  onOpenChange: (open: boolean) => void
  onClear?: () => void
  children: React.ReactNode
}

/**
 * One filter collapsed to a single trigger. Expanded inline, five groups plus 50 source chips cost
 * ~215px of vertical space above the listings; as triggers the whole bar is one row. The trigger has
 * to carry its own state — the count or the chosen value — because a collapsed filter you cannot read
 * is worse than no filter at all.
 *
 * Open state is owned by the caller so that opening one group closes the others.
 */
export function FilterPopover({ label, summary, count, open, onOpenChange, onClear, children }: Props) {
  const t = useT('history')
  const panelId = useId()
  const wrapRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)
  const active = summary !== undefined || (count ?? 0) > 0

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return
      onOpenChange(false)
      triggerRef.current?.focus()
    }
    // pointerdown rather than click: a press outside should dismiss before the press can act on
    // whatever is underneath.
    const onPointerDown = (e: PointerEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) onOpenChange(false)
    }
    document.addEventListener('keydown', onKey)
    document.addEventListener('pointerdown', onPointerDown)
    return () => {
      document.removeEventListener('keydown', onKey)
      document.removeEventListener('pointerdown', onPointerDown)
    }
  }, [open, onOpenChange])

  useEffect(() => {
    const el = panelRef.current
    if (!open || !el) return
    // Right-align when opening leftward would run off screen. Measured, not assumed: the bar wraps,
    // so which trigger sits closest to the edge changes with the window. Written straight to the
    // class list rather than through state — this is a layout read, and it must not cause a render.
    el.classList.remove('filter-pop__panel--end')
    if (el.getBoundingClientRect().right > window.innerWidth - 8) {
      el.classList.add('filter-pop__panel--end')
    }
    // Move focus in, so the panel is operable the moment it opens.
    el.querySelector<HTMLElement>('input, button, select')?.focus()
  }, [open])

  return (
    <div className="filter-pop" ref={wrapRef}>
      <button
        type="button"
        ref={triggerRef}
        className={`chip filter-pop__trigger${active ? ' chip--active' : ''}`}
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => onOpenChange(!open)}
      >
        {label}
        {/* Explicit spaces: JSX drops the newline between these, which left the accessible name
            reading "posted7d". The flex gap only fixes the look. */}
        {summary !== undefined && <>{' '}<span className="filter-pop__summary">{summary}</span></>}
        {count !== undefined && count > 0 && <>{' '}<span className="chip__count">{n(count)}</span></>}
        <span className="filter-pop__caret" aria-hidden>▾</span>
      </button>

      {open && (
        <div className="filter-pop__panel" id={panelId} ref={panelRef} role="group" aria-label={label}>
          {children}
          {onClear && active && (
            <div className="filter-pop__foot">
              <button type="button" className="link-button" onClick={onClear}>{t.filterClear}</button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
