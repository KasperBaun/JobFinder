import { useCallback, useEffect, useRef, useState } from 'react'
import { startSearch } from '../api/client'
import type { ListingMatch, SearchProgressEvent, SearchRequest } from '../api/types'

export type SearchStatus = 'idle' | 'running' | 'complete' | 'error'

export type UseSearchStream = {
  status: SearchStatus
  events: SearchProgressEvent[]
  runId?: string
  shortlist: ListingMatch[]
  error?: string
  start: (req: SearchRequest) => void
  reset: () => void
}

export function useSearchStream(): UseSearchStream {
  const [status, setStatus] = useState<SearchStatus>('idle')
  const [events, setEvents] = useState<SearchProgressEvent[]>([])
  const [runId, setRunId] = useState<string | undefined>(undefined)
  const [shortlist, setShortlist] = useState<ListingMatch[]>([])
  const [error, setError] = useState<string | undefined>(undefined)
  const abortRef = useRef<AbortController | null>(null)

  const reset = useCallback(() => {
    abortRef.current?.abort()
    abortRef.current = null
    setStatus('idle')
    setEvents([])
    setRunId(undefined)
    setShortlist([])
    setError(undefined)
  }, [])

  const start = useCallback((req: SearchRequest) => {
    abortRef.current?.abort()
    const controller = new AbortController()
    abortRef.current = controller
    setStatus('running')
    setEvents([])
    setRunId(undefined)
    setShortlist([])
    setError(undefined)

    void (async () => {
      try {
        for await (const ev of startSearch(req, controller.signal)) {
          setEvents(prev => [...prev, ev])
          if (ev.type === 'complete') {
            setRunId(ev.runId)
            setShortlist(ev.shortlist)
            setStatus('complete')
          } else if (ev.type === 'error') {
            setError(ev.message)
            setStatus('error')
          }
        }
        setStatus(prev => (prev === 'running' ? 'complete' : prev))
      } catch (err) {
        if (controller.signal.aborted) return
        setError(err instanceof Error ? err.message : String(err))
        setStatus('error')
      }
    })()
  }, [])

  useEffect(() => {
    return () => {
      abortRef.current?.abort()
    }
  }, [])

  return { status, events, runId, shortlist, error, start, reset }
}
