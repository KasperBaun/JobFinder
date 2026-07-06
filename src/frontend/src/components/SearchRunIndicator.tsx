import { Link, useLocation } from 'react-router-dom'
import { useSearchRun } from '../context/SearchRunContext'
import { PHASE_LABEL } from '../utils/searchLabels'

/**
 * Floating badge shown on every page while a search is running, so the user can navigate freely and
 * still see progress (and cancel) from anywhere. Hidden on the search page itself (which shows the
 * full panel) and when no run is active.
 */
export function SearchRunIndicator() {
  const { job, isActive, cancel } = useSearchRun()
  const location = useLocation()

  if (!isActive || !job) return null
  if (location.pathname === '/search') return null

  const done = job.providers.filter(p => p.status === 'ok' || p.status === 'failed').length
  const total = job.providers.length

  return (
    <div className="run-indicator" role="status" aria-live="polite">
      <span className="run-indicator__spinner" aria-hidden="true" />
      <Link to="/search" className="run-indicator__text">
        Search running · {PHASE_LABEL[job.phase]}
        {job.phase === 'fetching' && total > 0 && (
          <> · {done}/{total} sources</>
        )}
      </Link>
      <button
        type="button"
        className="run-indicator__cancel"
        onClick={() => void cancel()}
      >
        cancel
      </button>
    </div>
  )
}
