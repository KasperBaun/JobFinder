import type { RunDetail } from '../../api/types'
import { n, useT } from '../../i18n'
import type { TabKey } from './hash'

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
  const tabs: { key: TabKey; label: string; count?: number; available: boolean }[] = [
    { key: 'raw',     label: t.tabRaw,     count: data.raw?.reduce((n, p) => n + p.listings.length, 0), available: !!data.raw },
    { key: 'dedupe',  label: t.tabDedupe,  count: data.dedupeMerges?.length, available: !!data.dedupeMerges },
    { key: 'dropped', label: t.tabDropped, count: data.dropped?.length, available: !!data.dropped },
  ]
  return (
    <nav className="audit-tabs" aria-label={t.detailsAria}>
      <span className="audit-tabs__label">{t.showLabel}</span>
      {tabs.map(tab => (
        <button
          key={tab.key}
          type="button"
          className={`audit-tab ${active === tab.key ? 'audit-tab--active' : ''} ${!tab.available ? 'audit-tab--disabled' : ''}`}
          onClick={() => tab.available && onChange(tab.key)}
          disabled={!tab.available}
          title={tab.available ? '' : t.notRecorded}
        >
          {tab.label}
          {tab.count !== undefined && <span className="audit-tab__count">{n(tab.count)}</span>}
        </button>
      ))}
    </nav>
  )
}
