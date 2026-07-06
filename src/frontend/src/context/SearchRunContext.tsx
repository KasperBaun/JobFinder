import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { cancelSearch, getActiveSearch, startSearch, streamJobSearch } from '../api/client'
import { isTerminalState } from '../api/types'
import type { JobSearch, SearchRequest } from '../api/types'

type SearchRunContextValue = {
  /** The current/last run, or null if none has been started this session and none is active. */
  job: JobSearch | null
  /** True while a run is queued or running (drives the global indicator + disables Run). */
  isActive: boolean
  start: (req: SearchRequest) => Promise<void>
  cancel: () => Promise<void>
  /** Clear the panel after viewing a finished run (does not affect the server). */
  reset: () => void
}

const SearchRunContext = createContext<SearchRunContextValue | null>(null)

/**
 * Owns the single live search across the whole app. Because it lives above the router, navigating
 * between pages never unmounts it, so the SSE stream — and the run it observes — survive navigation.
 * On mount it reconnects to any run still active on the server (covers a full reload / host restart).
 */
export function SearchRunProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [job, setJob] = useState<JobSearch | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const jobRef = useRef<JobSearch | null>(null)

  const consume = useCallback(
    async (id: string, controller: AbortController) => {
      try {
        for await (const snap of streamJobSearch(id, controller.signal)) {
          jobRef.current = snap
          setJob(snap)
          if (isTerminalState(snap.state)) {
            queryClient.invalidateQueries({ queryKey: ['history'] })
            if (snap.state === 'succeeded') {
              queryClient.invalidateQueries({ queryKey: ['run', id] })
            }
          }
        }
      } catch {
        // Stream dropped (e.g. server restart). The run may still be alive server-side; a manual
        // refresh / navigation re-resolves it via getActiveSearch. Leave the last snapshot in place.
      }
    },
    [queryClient],
  )

  const openStream = useCallback(
    (id: string) => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      void consume(id, controller)
    },
    [consume],
  )

  const start = useCallback(
    async (req: SearchRequest) => {
      const { id } = await startSearch(req)
      openStream(id)
    },
    [openStream],
  )

  const cancel = useCallback(async () => {
    const id = jobRef.current?.id
    if (id) await cancelSearch(id)
  }, [])

  const reset = useCallback(() => {
    abortRef.current?.abort()
    abortRef.current = null
    jobRef.current = null
    setJob(null)
  }, [])

  useEffect(() => {
    let cancelled = false
    void (async () => {
      try {
        const active = await getActiveSearch()
        if (!cancelled && active && !isTerminalState(active.state)) {
          jobRef.current = active
          setJob(active)
          openStream(active.id)
        }
      } catch {
        // no active run / server unreachable — nothing to resume
      }
    })()
    return () => {
      cancelled = true
    }
  }, [openStream])

  const isActive = job != null && !isTerminalState(job.state)

  return (
    <SearchRunContext.Provider value={{ job, isActive, start, cancel, reset }}>
      {children}
    </SearchRunContext.Provider>
  )
}

export function useSearchRun(): SearchRunContextValue {
  const ctx = useContext(SearchRunContext)
  if (!ctx) throw new Error('useSearchRun must be used within a SearchRunProvider')
  return ctx
}
