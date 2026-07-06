import { useEffect, useState } from 'react'
import { formatDuration } from '../utils/time'

// Live elapsed time for a run, formatted m:ss / h:mm:ss. Ticks every second only while `active`
// (mirrors useServerConnection's interval + cleanup), so a running search shows a moving timer
// even though SSE snapshots don't arrive on a fixed cadence. Freezes at `endIso` once terminal.
export function useElapsed(startIso: string | undefined, endIso: string | undefined, active: boolean): string {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!active) return
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [active])

  if (!startIso) return formatDuration(0)
  const start = Date.parse(startIso)
  if (Number.isNaN(start)) return formatDuration(0)
  const end = active ? now : (endIso ? Date.parse(endIso) : now)
  return formatDuration(end - start)
}
