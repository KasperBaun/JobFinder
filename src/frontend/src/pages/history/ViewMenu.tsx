import { useState } from 'react'
import type { RunDetail } from '../../api/types'
import { usePopover } from '../../components/usePopover'
import { n, useT } from '../../i18n'
import type { TabKey } from './hash'

type Entry = { key: TabKey; label: string; count?: number; available: boolean; title?: string }

/**
 * The one chooser for which of a run's five datasets is on screen: the two primary views
 * (top jobs, all rated) and the three audit views (raw fetch, dedupe, dropped). These used to
 * be a wide segmented control plus a separate menu — two mechanisms for the same decision.
 * The trigger names the active view with its count, because a collapsed control you cannot
 * read is worse than an expanded one.
 */
export function ViewMenu({
  active,
  onChange,
  data,
}: {
  active: TabKey
  onChange: (tab: TabKey) => void
  data: RunDetail
}) {
  const t = useT('history')
  const [open, setOpen] = useState(false)
  const { panelId, wrapRef, triggerRef, panelRef } = usePopover(open, setOpen)

  const primary: Entry[] = [
    { key: 'shortlist', label: t.tabTopJobs, count: data.shortlist.length, available: true },
    { key: 'longlist',  label: t.tabAllRated, count: data.scored?.length, available: !!data.scored, title: t.tabAllRatedTitle },
  ]
  const audit: Entry[] = [
    { key: 'raw',     label: t.tabRaw,     count: data.raw?.reduce((n, p) => n + p.listings.length, 0), available: !!data.raw },
    { key: 'dedupe',  label: t.tabDedupe,  count: data.dedupeMerges?.length, available: !!data.dedupeMerges },
    { key: 'dropped', label: t.tabDropped, count: data.dropped?.length, available: !!data.dropped },
  ]
  const current = [...primary, ...audit].find((e) => e.key === active) ?? primary[0]

  const item = (e: Entry) => (
    <button
      key={e.key}
      type="button"
      className={`view-menu__item${active === e.key ? ' view-menu__item--active' : ''}`}
      aria-current={active === e.key || undefined}
      onClick={() => {
        if (!e.available) return
        onChange(e.key)
        setOpen(false)
      }}
      disabled={!e.available}
      title={!e.available ? t.notRecorded : e.title ?? ''}
    >
      {/* Explicit space: JSX drops the newline, which would leave the accessible name
          reading "all fetched3". The flex layout only fixes the look. */}
      {e.label}
      {e.count !== undefined && <>{' '}<span className="view-menu__count">{n(e.count)}</span></>}
    </button>
  )

  return (
    <div className="filter-pop" ref={wrapRef}>
      <button
        type="button"
        ref={triggerRef}
        className="view-switch"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen(!open)}
      >
        {current.label}
        {current.count !== undefined && <>{' '}<span className="view-switch__count">{n(current.count)}</span></>}
        <span className="filter-pop__caret" aria-hidden>▾</span>
      </button>

      {open && (
        <div className="filter-pop__panel" id={panelId} ref={panelRef} role="group" aria-label={t.resultViewAria}>
          <div className="view-menu">
            {primary.map(item)}
            <div className="view-menu__divider" role="separator" />
            {audit.map(item)}
          </div>
        </div>
      )}
    </div>
  )
}
