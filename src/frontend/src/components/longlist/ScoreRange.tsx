import { useMemo } from 'react'
import { dec, useT } from '../../i18n'
import { withScoreMax, withScoreMin, type LonglistFilters } from './filterState'

const BINS = 20

interface Props {
  filters: LonglistFilters
  /** Every rated listing's score — the distribution the window is cut against. */
  scores: readonly number[]
  onChange: (next: LonglistFilters) => void
}

/**
 * The rating window as one two-thumbed track under a histogram of the run's scores. The
 * histogram is what makes the filter usable: with 2 000 ratings the useful cut depends on where
 * the mass sits, and without the preview the thumbs are dragged blind. It was two separate
 * sliders side by side, which read as a single broken control and let the minimum be dragged
 * above the maximum — emptying the table with no visible cause.
 */
export function ScoreRange({ filters, scores, onChange }: Props) {
  const t = useT('history')
  const { scoreMin, scoreMax } = filters

  const bins = useMemo(() => {
    const counts = new Array<number>(BINS).fill(0)
    for (const s of scores) counts[Math.min(BINS - 1, Math.floor(s * BINS))]++
    return counts
  }, [scores])
  const peak = Math.max(...bins, 1)

  // Both thumbs sit on one track, so they overlap when the values meet. Whichever one still has room
  // to move has to be on top, or the pair locks up: at 1.00/1.00 only the minimum can go anywhere,
  // and at 0.00/0.00 only the maximum can.
  const minOnTop = (scoreMin + scoreMax) / 2 > 0.5

  return (
    <div className="longlist__score">
      {/* Decorative to assistive tech: the inputs below carry the values, the table the result. */}
      <div className="score-hist" aria-hidden="true">
        {bins.map((count, i) => {
          const centre = (i + 0.5) / BINS
          const inWindow = centre >= scoreMin && centre <= scoreMax
          return (
            <div
              key={i}
              className={`score-hist__bar${inWindow ? ' score-hist__bar--in' : ''}`}
              // 8% floor: a bin holding one score out of 2 000 must still be visible.
              style={{ height: count === 0 ? '0%' : `${Math.max(8, (count / peak) * 100)}%` }}
            />
          )
        })}
      </div>
      <div className={`range-dual${minOnTop ? ' range-dual--min-on-top' : ''}`}>
        <div className="range-dual__track" aria-hidden="true">
          <div
            className="range-dual__fill"
            style={{ left: `${scoreMin * 100}%`, right: `${(1 - scoreMax) * 100}%` }}
          />
        </div>
        <input
          className="range-dual__input range-dual__input--min"
          type="range" min={0} max={1} step={0.01}
          value={scoreMin}
          aria-label={t.ratingMinAria}
          onChange={(e) => onChange(withScoreMin(filters, parseFloat(e.target.value)))}
        />
        <input
          className="range-dual__input range-dual__input--max"
          type="range" min={0} max={1} step={0.01}
          value={scoreMax}
          aria-label={t.ratingMaxAria}
          onChange={(e) => onChange(withScoreMax(filters, parseFloat(e.target.value)))}
        />
      </div>
      <div className="score-ends" aria-hidden="true">
        <span>{dec(scoreMin, 2)}</span>
        <span>{dec(scoreMax, 2)}</span>
      </div>
    </div>
  )
}
