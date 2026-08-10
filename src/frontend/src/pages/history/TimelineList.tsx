import type { RunDetail } from '../../api/types'
import { formatAbsolute, formatRelative } from '../../utils/time'
import { serverText, useT } from '../../i18n'

export function TimelineList({ data }: { data: RunDetail }) {
  const s = useT('server')
  if (!data.timeline || data.timeline.length === 0) return null
  return (
    <ol className="timeline">
      {data.timeline.map((ev, i) => (
        <li key={i} className={`timeline__item timeline__item--${ev.level}`}>
          <span className="timeline__time tabular mono" title={formatAbsolute(ev.timestamp)}>
            {formatRelative(ev.timestamp)}
          </span>
          <span className="timeline__msg">{serverText(s.timeline, ev.messageKey, ev.args, ev.message)}</span>
        </li>
      ))}
    </ol>
  )
}
