import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { setMark } from '../api/client'
import type { MarkRequest, RunDetail } from '../api/types'

type MarkValue = 'good' | 'bad' | undefined

interface Props {
  runId: string
  listingId: string
  current: MarkValue
}

function nextState(value: MarkValue): MarkValue {
  if (value === undefined) return 'good'
  if (value === 'good') return 'bad'
  return undefined
}

export function MarkButton({ runId, listingId, current }: Props) {
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
    'Mark'

  const cls =
    optimistic === 'good' ? 'mark-button mark-button--good' :
    optimistic === 'bad' ? 'mark-button mark-button--bad' :
    'mark-button'

  return (
    <div className="mark-button-wrap">
      <button
        type="button"
        className={cls}
        onClick={handleClick}
        disabled={mutation.isPending}
        aria-label={`Mark listing as ${label}`}
      >
        {label}
      </button>
      {error && <span className="mark-button__error">{error}</span>}
    </div>
  )
}
