import type { RunDetail } from '../../api/types'
import { n, useT } from '../../i18n'
import type { TabKey } from './hash'

export function ResultsToggle({
  active,
  onChange,
  data,
}: {
  active: TabKey
  onChange: (tab: TabKey) => void
  data: RunDetail
}) {
  const t = useT('history')
  const longlistCount = data.scored?.length
  const longlistAvailable = !!data.scored
  return (
    <div className="view-toggle" role="tablist" aria-label={t.resultViewAria}>
      <button
        type="button"
        role="tab"
        aria-selected={active === 'shortlist'}
        className={`view-toggle__seg ${active === 'shortlist' ? 'view-toggle__seg--active' : ''}`}
        onClick={() => onChange('shortlist')}
      >
        {t.tabTopJobs} <span className="view-toggle__count">{n(data.shortlist.length)}</span>
      </button>
      <button
        type="button"
        role="tab"
        aria-selected={active === 'longlist'}
        className={`view-toggle__seg ${active === 'longlist' ? 'view-toggle__seg--active' : ''}`}
        onClick={() => longlistAvailable && onChange('longlist')}
        disabled={!longlistAvailable}
        title={longlistAvailable ? t.tabAllRatedTitle : t.notRecorded}
      >
        {t.tabAllRated} {longlistCount !== undefined && <span className="view-toggle__count">{n(longlistCount)}</span>}
      </button>
    </div>
  )
}
