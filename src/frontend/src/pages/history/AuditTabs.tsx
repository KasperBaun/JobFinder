import { useState } from 'react'
import type { RunDetail } from '../../api/types'
import { FilterPopover } from '../../components/longlist/FilterPopover'
import { n, useT } from '../../i18n'
import type { TabKey } from './hash'

/**
 * The three audit views (raw fetch, dedupe, dropped) collapsed behind one trigger, so the run
 * toolbar stays a single row. The trigger names the active audit view — a collapsed control you
 * cannot read is worse than an expanded one — and goes active with it, since none of the three
 * is otherwise visible as a tab.
 */
export function AuditTabs({
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
  const tabs: { key: TabKey; label: string; count?: number; available: boolean }[] = [
    { key: 'raw',     label: t.tabRaw,     count: data.raw?.reduce((n, p) => n + p.listings.length, 0), available: !!data.raw },
    { key: 'dedupe',  label: t.tabDedupe,  count: data.dedupeMerges?.length, available: !!data.dedupeMerges },
    { key: 'dropped', label: t.tabDropped, count: data.dropped?.length, available: !!data.dropped },
  ]
  const activeTab = tabs.find((tab) => tab.key === active)

  return (
    <FilterPopover
      label={t.showLabel}
      summary={activeTab?.label}
      open={open}
      onOpenChange={setOpen}
    >
      <div className="view-menu">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={`view-menu__item${active === tab.key ? ' view-menu__item--active' : ''}`}
            onClick={() => {
              if (!tab.available) return
              onChange(tab.key)
              setOpen(false)
            }}
            disabled={!tab.available}
            title={tab.available ? '' : t.notRecorded}
          >
            {/* Explicit space: JSX drops the newline, which would leave the accessible name
                reading "all fetched3". The flex layout only fixes the look. */}
            {tab.label}
            {tab.count !== undefined && <>{' '}<span className="view-menu__count">{n(tab.count)}</span></>}
          </button>
        ))}
      </div>
    </FilterPopover>
  )
}
