import type { JobSearchState } from '../../api/types'
import { useT } from '../../i18n'

export function StateBadge({ state }: { state?: JobSearchState }) {
  const s = state ?? 'succeeded'
  return <span className={`state-badge state-badge--${s}`}>{useT('search').state[s]}</span>
}
