import type { RunSummary } from '../api/types'
import { formatRelative, formatAbsolute } from '../utils/time'
import { dec, useT } from '../i18n'

interface Props {
  run: RunSummary
}

export function RunSummaryCard({ run }: Props) {
  const t = useT('listing')
  const ok = run.providers.filter(p => p.status === 'ok').length
  const failed = run.providers.filter(p => p.status === 'failed').length

  return (
    <div className="run-stat-bar">
      <div className="run-stat-bar__lead">
        <h2 className="run-stat-bar__id">{t.runTitle}</h2>
        <time
          className="run-stat-bar__time"
          title={formatAbsolute(run.startedAt)}
          dateTime={run.startedAt}
        >
          {formatRelative(run.startedAt)}
        </time>
      </div>

      <dl className="run-stat-bar__metrics">
        <div className={`stat${failed > 0 ? ' stat--bad' : ''}`}>
          <dt>{t.runSources}</dt>
          <dd>
            <span className="stat__num">{ok}</span> {t.runOk}
            {failed > 0 && <> · <span className="stat__num">{failed}</span> {t.runFailed}</>}
          </dd>
        </div>
        <div className="stat">
          <dt>{t.runJobsFound}</dt>
          <dd><span className="stat__num">{run.fetchedCount}</span></dd>
        </div>
        <div className="stat">
          <dt>{t.runUniqueJobs}</dt>
          <dd><span className="stat__num">{run.dedupedCount}</span></dd>
        </div>
        <div className="stat">
          <dt>{t.runTopJobs}</dt>
          <dd><span className="stat__num">{run.shortlistCount}</span></dd>
        </div>
        <div className="stat">
          <dt>{t.runBestRating}</dt>
          <dd><span className="stat__num">{dec(run.topScore, 2)}</span></dd>
        </div>
        <div className="stat">
          <dt>{t.runGoodMatches}</dt>
          <dd><span className="stat__num">{run.goodMarks}</span> / {run.shortlistCount}</dd>
        </div>
      </dl>
    </div>
  )
}
