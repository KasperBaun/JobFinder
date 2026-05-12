import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { setMark } from '../api/client'
import type { MarkRequest, RunDetail } from '../api/types'

type MarkValue = 'good' | 'bad' | undefined

interface Props {
  runId: string
  listingId: string
  current: MarkValue
  compact?: boolean
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

export function MarkButton({ runId, listingId, current, compact }: Props) {
  const [optimistic, setOptimistic] = useState<MarkValue>(current)
  const [error, setError] = useState<string | null>(null)
  const queryClient = useQueryClient()

  useEffect(() => {
    setOptimistic(current)
  }, [current])

  const mutation = useMutation({
    mutationFn: async (target: MarkValue) => {
      const req: MarkRequest = { runId, listingId, mark: target ?? null }
      const res = await setMark(req)
      if (!res.success) throw new Error(res.error ?? 'Mark failed')
      return target
    },
    onSuccess: (target) => {
      setError(null)
      queryClient.setQueryData<RunDetail | undefined>(['run', runId], (prev) => {
        if (!prev) return prev
        const marks = { ...prev.marks }
        if (target === undefined) {
          delete marks[listingId]
        } else {
          marks[listingId] = target
        }
        return { ...prev, marks }
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
    mutation.mutate(target)
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
      {error && <span className="mark-button__error">{error}</span>}
    </div>
  )
}
