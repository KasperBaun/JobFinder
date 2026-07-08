import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { setMark } from '../api/client'
import type { MarkRequest, RunDetail } from '../api/types'
import { MarkWhy } from './MarkWhy'

type MarkValue = 'good' | 'bad' | undefined

interface Props {
  runId: string
  listingId: string
  current: MarkValue
  reason?: string
  compact?: boolean
}

interface MarkPayload {
  mark: MarkValue
  reason: string | null
}

function nextState(value: MarkValue): MarkValue {
  if (value === undefined) return 'good'
  if (value === 'good') return 'bad'
  return undefined
}

const TOOLTIPS: Record<'unset' | 'good' | 'bad', string> = {
  unset: 'Rate this job. Your ratings help jobfinder find better jobs next time. Click for a good match.',
  good:  'Marked as a Good match. Click to flip to Not a match.',
  bad:   'Marked as Not a match. Click to clear the rating.',
}

export function MarkButton({ runId, listingId, current, reason, compact }: Props) {
  const [optimistic, setOptimistic] = useState<MarkValue>(current)
  const [error, setError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  useEffect(() => {
    setOptimistic(current)
  }, [current])

  const mutation = useMutation({
    mutationFn: async (payload: MarkPayload) => {
      const req: MarkRequest = { runId, listingId, mark: payload.mark ?? null, reason: payload.reason }
      const res = await setMark(req)
      if (!res.success) throw new Error(res.error ?? 'Mark failed')
      return payload
    },
    onSuccess: (payload) => {
      setError(null)
      queryClient.setQueryData<RunDetail | undefined>(['run', runId], (prev) => {
        if (!prev) return prev
        const marks = { ...prev.marks }
        const markReasons = { ...prev.markReasons }
        if (payload.mark === undefined) {
          delete marks[listingId]
        } else {
          marks[listingId] = payload.mark
        }
        if (payload.mark !== undefined && payload.reason) {
          markReasons[listingId] = payload.reason
        } else {
          delete markReasons[listingId]
        }
        return { ...prev, marks, markReasons }
      })
      void queryClient.invalidateQueries({ queryKey: ['history'] })
    },
    onError: (err) => {
      setOptimistic(current)
      const msg = err instanceof Error ? err.message : String(err)
      setError(msg)
      console.error('Mark failed:', err)
    },
  })

  function handleClick() {
    const target = nextState(optimistic)
    setOptimistic(target)
    setError(null)
    // Flipping or clearing the mark drops the reason — it explained the old mark.
    mutation.mutate({ mark: target, reason: null })
  }

  function handleSaveReason(next: string | null) {
    setError(null)
    mutation.mutate({ mark: optimistic, reason: next })
  }

  const label =
    optimistic === 'good' ? 'Good match' :
    optimistic === 'bad' ? 'Not a match' :
    'Rate this job'

  const cls =
    optimistic === 'good' ? `mark-button mark-button--good${compact ? ' mark-button--compact' : ''}` :
    optimistic === 'bad' ? `mark-button mark-button--bad${compact ? ' mark-button--compact' : ''}` :
    `mark-button${compact ? ' mark-button--compact' : ''}`

  const tooltip =
    optimistic === 'good' ? TOOLTIPS.good :
    optimistic === 'bad' ? TOOLTIPS.bad :
    TOOLTIPS.unset

  return (
    <div className="mark-button-wrap">
      <button
        type="button"
        className={cls}
        onClick={handleClick}
        disabled={mutation.isPending}
        aria-label={tooltip}
        data-tooltip={tooltip}
      >
        {label}
      </button>
      {optimistic !== undefined && (
        <MarkWhy reason={reason} saving={mutation.isPending} onSave={handleSaveReason} />
      )}
      {error && <span className="mark-button__error">{error}</span>}
    </div>
  )
}
