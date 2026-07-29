import type { ScoreBreakdown, ScoredEntry } from '../api/types'
import { dec, useT } from '../i18n'
import type { Messages } from '../i18n'

type ComponentKey = keyof Messages['listing']['component']

const COMPONENT_KEYS: ComponentKey[] = [
  'primaryStack', 'secondaryStack', 'seniority', 'locationRemote', 'domain', 'freshness',
]

export function BreakdownBar({ b }: { b: ScoreBreakdown }) {
  const t = useT('listing')
  const positives = COMPONENT_KEYS.map(key => ({ key, label: t.component[key], value: Math.max(0, b[key as keyof ScoreBreakdown]) }))
  const totalPositive = positives.reduce((n, c) => n + c.value, 0)
  if (totalPositive === 0 && b.disqualifierPenalty === 0) {
    return <span className="muted">—</span>
  }
  return (
    <div className="bd-bar" aria-label={t.breakdownAria}>
      {positives.map((c, i) => {
        if (c.value <= 0) return null
        const pct = (c.value / Math.max(totalPositive, 0.001)) * 100
        return (
          <span
            key={c.key}
            className={`bd-bar__seg bd-bar__seg--${i}`}
            style={{ width: `${pct}%` }}
            title={`${c.label}: ${dec(c.value, 3)}`}
          />
        )
      })}
      {b.disqualifierPenalty < 0 && (
        <span
          className="bd-bar__seg bd-bar__seg--penalty"
          style={{ width: '100%' }}
          title={`${t.disqualifierPenalty}: ${dec(b.disqualifierPenalty, 3)}`}
        />
      )}
    </div>
  )
}

export function BreakdownDetail({ entry }: { entry: ScoredEntry }) {
  const t = useT('listing')
  return (
    <div className="bd-detail">
      {COMPONENT_KEYS.map(key => (
        <div key={key} className="bd-detail__row">
          <span className="bd-detail__label">{t.component[key]}</span>
          <span className="bd-detail__value mono tabular">
            {dec(entry.breakdown[key as keyof ScoreBreakdown], 3)}
          </span>
        </div>
      ))}
      {entry.breakdown.disqualifierPenalty !== 0 && (
        <div className="bd-detail__row">
          <span className="bd-detail__label" style={{ color: 'var(--c-bad)' }}>{t.disqualifierPenalty}</span>
          <span className="bd-detail__value mono tabular" style={{ color: 'var(--c-bad)' }}>
            {dec(entry.breakdown.disqualifierPenalty, 3)}
          </span>
        </div>
      )}
      <div className="bd-detail__row bd-detail__row--total">
        <span className="bd-detail__label">{t.totalRating}</span>
        <span className="bd-detail__value mono tabular">{dec(entry.score, 3)}</span>
      </div>
    </div>
  )
}
