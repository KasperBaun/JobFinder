import { useCallback, useMemo } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { decodeFromHash, encodeToHash, type LonglistState } from './filterState'

/**
 * The longlist's filters and sort live in the URL hash, and two components render from them:
 * the filter bar up in the run-detail toolbar and the table below it. Both go through this hook
 * so there is exactly one decode and one way to write back.
 *
 * Filters and sort travel in one hash but change independently, so a caller replacing one axis
 * must carry the other through untouched — resetting the filters must not silently reorder the
 * table.
 */
export function useLonglistState(): [LonglistState, (next: LonglistState) => void] {
  const navigate = useNavigate()
  const location = useLocation()

  const state = useMemo(() => {
    const cleaned = location.hash.startsWith('#') ? location.hash.slice(1) : location.hash
    return decodeFromHash(new URLSearchParams(cleaned))
  }, [location.hash])

  const setState = useCallback(
    (next: LonglistState) =>
      navigate(`${location.pathname}#${encodeToHash(next).toString()}`, { replace: true }),
    [navigate, location.pathname],
  )

  return [state, setState]
}
