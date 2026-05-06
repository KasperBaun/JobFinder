import { useEffect } from 'react'

interface Props {
  kind: 'ok' | 'err'
  message: string
  onDismiss: () => void
  autoDismissMs?: number
}

export function Toast({ kind, message, onDismiss, autoDismissMs = 3500 }: Props) {
  useEffect(() => {
    if (kind !== 'ok') return
    const id = setTimeout(onDismiss, autoDismissMs)
    return () => clearTimeout(id)
  }, [kind, autoDismissMs, onDismiss])

  return (
    <div className={`toast toast--${kind}`} role="status" onClick={onDismiss}>
      {message}
    </div>
  )
}
