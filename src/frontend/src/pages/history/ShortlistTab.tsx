import type { RunDetail } from '../../api/types'
import { ListingCard } from '../../components/ListingCard'
import { useT } from '../../i18n'

export function ShortlistTab({ data }: { data: RunDetail }) {
  const t = useT('history')
  return (
    <section className="results">
      <h2 className="results__heading">
        {t.tabTopJobs} <span className="muted serif" style={{ fontStyle: 'italic' }}>({data.shortlist.length})</span>
      </h2>
      {data.shortlist.length === 0 && <div className="muted">{t.noTopJobs}</div>}
      <div className="listing-list">
        {data.shortlist.map(m => (
          <ListingCard
            key={m.id}
            match={m}
            runId={data.runId}
            mark={data.marks[m.id]}
            markReason={data.markReasons?.[m.id]}
            markStatus={data.markStatuses?.[m.id]}
            breakdownEntry={data.scored?.find(e => e.id === m.id)}
          />
        ))}
      </div>
    </section>
  )
}
