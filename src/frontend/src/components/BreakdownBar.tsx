import type { ScoreBreakdown, ScoredEntry } from '../api/types'

export const COMPONENT_LABELS: Array<{ key: keyof ScoreBreakdown; label: string }> = [
  { key: 'primaryStack', label: 'must-have skills' },
  { key: 'secondaryStack', label: 'nice-to-have skills' },
  { key: 'seniority', label: 'experience level' },
  { key: 'locationRemote', label: 'location' },
  { key: 'domain', label: 'industry' },
  { key: 'freshness', label: 'freshness' },
]

export function BreakdownBar({ b }: { b: ScoreBreakdown }) {
  const positives = COMPONENT_LABELS.map(c => ({ ...c, value: Math.max(0, b[c.key]) }))
  const totalPositive = positives.reduce((n, c) => n + c.value, 0)
  if (totalPositive === 0 && b.disqualifierPenalty === 0) {
    return <span className="muted">—</span>
  }
  return (
    <div className="bd-bar" aria-label="rating breakdown">
      {positives.map((c, i) => {
        if (c.value <= 0) return null
        const pct = (c.value / Math.max(totalPositive, 0.001)) * 100
        return (
          <span
            key={c.key}
            className={`bd-bar__seg bd-bar__seg--${i}`}
            style={{ width: `${pct}%` }}
            title={`${c.label}: ${c.value.toFixed(3)}`}
          />
        )
      })}
      {b.disqualifierPenalty < 0 && (
        <span
          className="bd-bar__seg bd-bar__seg--penalty"
          style={{ width: '100%' }}
          title={`deal-breaker penalty: ${b.disqualifierPenalty.toFixed(3)}`}
        />
      )}
    </div>
  )
}

export function BreakdownDetail({ entry }: { entry: ScoredEntry }) {
  return (
    <div className="bd-detail">
      {COMPONENT_LABELS.map(c => (
        <div key={c.key} className="bd-detail__row">
          <span className="bd-detail__label">{c.label}</span>
          <span className="bd-detail__value mono tabular">
            {entry.breakdown[c.key].toFixed(3)}
          </span>
        </div>
      ))}
      {entry.breakdown.disqualifierPenalty !== 0 && (
        <div className="bd-detail__row">
          <span className="bd-detail__label" style={{ color: 'var(--c-bad)' }}>deal-breaker penalty</span>
          <span className="bd-detail__value mono tabular" style={{ color: 'var(--c-bad)' }}>
            {entry.breakdown.disqualifierPenalty.toFixed(3)}
          </span>
        </div>
      )}
      <div className="bd-detail__row bd-detail__row--total">
        <span className="bd-detail__label">total rating</span>
        <span className="bd-detail__value mono tabular">{entry.score.toFixed(3)}</span>
      </div>
    </div>
  )
}
