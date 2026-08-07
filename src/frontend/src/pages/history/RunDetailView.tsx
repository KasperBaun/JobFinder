import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getRun } from '../../api/client'
import { RunSummaryCard } from '../../components/RunSummaryCard'
import { FilterBar } from '../../components/longlist/FilterBar'
import { useLonglistState } from '../../components/longlist/useLonglistState'
import { isTerminalState } from '../../api/types'
import { useT } from '../../i18n'
import { DedupeTab } from './DedupeTab'
import { DroppedTab } from './DroppedTab'
import { buildHash, parseHash, type TabKey } from './hash'
import { LonglistView } from './LonglistView'
import { RawFetchTab } from './RawFetchTab'
import { ShortlistTab } from './ShortlistTab'
import { ViewMenu } from './ViewMenu'
import { StateBadge } from './StateBadge'
import { TimelineList } from './TimelineList'

export function RunDetailView({ runId }: { runId: string }) {
  const t = useT('history')
  const navigate = useNavigate()
  const location = useLocation()
  const { tab, provider } = useMemo(() => parseHash(location.hash), [location.hash])
  const [llState, setLlState] = useLonglistState()

  const { data, isLoading, error } = useQuery({
    queryKey: ['run', runId],
    queryFn: () => getRun(runId),
    // A queued/running run has no results yet — poll until it's terminal.
    refetchInterval: query => {
      const state = query.state.data?.state
      return state !== undefined && !isTerminalState(state) ? 2000 : false
    },
  })

  function setTab(next: TabKey) {
    navigate(`/history/${runId}${buildHash(location.hash, next)}`, { replace: false })
  }

  // Legacy runs have no state and always carry results. New runs only have the rich result tabs
  // once they've succeeded; otherwise we show the lifecycle timeline.
  const hasResults = data != null && (data.state === undefined || data.state === 'succeeded')

  return (
    <div className="page page--wide">
      <header className="page__header">
        <Link to="/history" className="back-link">{t.backToHistory}</Link>
        <div className="page__eyebrow">{t.detailEyebrow}</div>
        <h1 className="page__heading">{t.detailHeading()}</h1>
      </header>

      {isLoading && <div className="muted">{t.detailLoading}</div>}
      {error && <div className="error-text">{t.detailLoadFailed}</div>}

      {data && (
        <>
          <RunSummaryCard run={data} />
          {hasResults ? (
            <>
              {/* One toolbar row: the view chooser and — on the longlist — its filters, so the
                  controls cost one line instead of three (R-085). */}
              <div className="run-toolbar">
                <ViewMenu active={tab} onChange={setTab} data={data} />
                {tab === 'longlist' && data.scored && (
                  <FilterBar
                    scored={data.scored}
                    filters={llState.filters}
                    onChange={(filters) => setLlState({ ...llState, filters })}
                  />
                )}
              </div>
              {tab === 'shortlist' && <ShortlistTab data={data} />}
              {tab === 'longlist'  && <LonglistView data={data} />}
              {tab === 'raw'       && <RawFetchTab data={data} focusProvider={provider} />}
              {tab === 'dedupe'    && <DedupeTab data={data} />}
              {tab === 'dropped'   && <DroppedTab data={data} />}
            </>
          ) : (
            <section className="progress-panel">
              <div className="progress-panel__head">
                <h2 className="progress-panel__heading">
                  <StateBadge state={data.state} />
                </h2>
              </div>
              <TimelineList data={data} />
            </section>
          )}
        </>
      )}
    </div>
  )
}
