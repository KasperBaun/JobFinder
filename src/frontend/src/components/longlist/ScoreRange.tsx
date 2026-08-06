import { dec, useT } from '../../i18n'
import { withScoreMax, withScoreMin, type LonglistFilters } from './filterState'

interface Props {
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
}

/**
 * The rating window as one two-thumbed track. It was two separate sliders side by side, which read
 * as a single broken control and let the minimum be dragged above the maximum — emptying the table
 * with no visible cause.
 */
export function ScoreRange({ filters, onChange }: Props) {
  const t = useT('history')
  const { scoreMin, scoreMax } = filters

  // Both thumbs sit on one track, so they overlap when the values meet. Whichever one still has room
  // to move has to be on top, or the pair locks up: at 1.00/1.00 only the minimum can go anywhere,
  // and at 0.00/0.00 only the maximum can.
  const minOnTop = (scoreMin + scoreMax) / 2 > 0.5

  return (
    <div className="longlist__score">
      <span className="muted small">{t.ratingRange(dec(scoreMin, 2), dec(scoreMax, 2))}</span>
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
    </div>
  )
}
