import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { setMark } from '../api/client'
import type { MarkRequest, RunDetail } from '../api/types'
import { MarkWhy } from './MarkWhy'
import { useT } from '../i18n'

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

export function MarkButton({ runId, listingId, current, reason, compact }: Props) {
  const t = useT('listing')
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
      if (!res.success) throw new Error(res.error ?? t.markFailed)
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

  function handleToggle(target: 'good' | 'bad') {
    const next = optimistic === target ? undefined : target
    setOptimistic(next)
    setError(null)
    // Flipping or clearing the mark drops the reason — it explained the old mark.
    mutation.mutate({ mark: next, reason: null })
  }

  function toggleCls(target: 'good' | 'bad') {
    return [
      'mark-toggle',
      `mark-toggle--${target}`,
      optimistic === target ? 'mark-toggle--active' : '',
      compact ? 'mark-toggle--compact' : '',
    ].filter(Boolean).join(' ')
  }

  function handleSaveReason(next: string | null) {
    setError(null)
    mutation.mutate({ mark: optimistic, reason: next })
  }

  const goodTip = optimistic === 'good' ? t.markTooltip.goodActive : t.markTooltip.good
  const badTip = optimistic === 'bad' ? t.markTooltip.badActive : t.markTooltip.bad

  return (
    <div className="mark-button-wrap">
      <div className="mark-toggle-group" role="group" aria-label={t.markGroupAria}>
        <button
          type="button"
          className={toggleCls('good')}
          onClick={() => handleToggle('good')}
          disabled={mutation.isPending}
          aria-pressed={optimistic === 'good'}
          aria-label={goodTip}
          data-tooltip={goodTip}
        >
          {compact ? <span aria-hidden>✓</span> : t.markGood}
        </button>
        <button
          type="button"
          className={toggleCls('bad')}
          onClick={() => handleToggle('bad')}
          disabled={mutation.isPending}
          aria-pressed={optimistic === 'bad'}
          aria-label={badTip}
          data-tooltip={badTip}
        >
          {compact ? <span aria-hidden>✕</span> : t.markBad}
        </button>
      </div>
      {optimistic !== undefined && (
        <MarkWhy reason={reason} saving={mutation.isPending} onSave={handleSaveReason} />
      )}
      {error && <span className="mark-button__error">{error}</span>}
    </div>
  )
}
