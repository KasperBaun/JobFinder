import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { testProvider } from '../../api/client'
import type { ProviderTestResult } from '../../api/types'
import { formatRelative } from '../../utils/time'
import { useT } from '../../i18n'

// Runs a one-off fetch and shows the outcome: count, timing, every returned listing (scrollable),
// and — when the fetch stopped at a configured ceiling — a warning that there may be more.
export function TestPanel({
  providerId,
  providerType,
  onError,
}: {
  providerId: number
  providerType: string
  onError: (message: string) => void
}) {
  const t = useT('providers')
  const [result, setResult] = useState<ProviderTestResult | null>(null)

  const test = useMutation({
    mutationFn: () => testProvider(providerId),
    onMutate: () => setResult(null),
    onSuccess: (r) => setResult(r),
    onError: (err) => onError(err instanceof Error ? err.message : String(err)),
  })

  const isManual = providerType === 'manual'

  return (
    <section className="card">
      <div className="row-spread">
        <h2 className="card__title" style={{ marginBottom: 0 }}>{t.testTitle}</h2>
        <button
          type="button"
          className="btn btn--secondary"
          onClick={() => test.mutate()}
          disabled={test.isPending || isManual}
        >
          {test.isPending ? <span className="spinner" /> : t.testNow}
        </button>
      </div>
      <p className="field__hint" style={{ marginTop: 'var(--space-3)' }}>
        {isManual ? t.testManualHint : t.testHint}
      </p>

      {result && (
        <div className={`provider-test-result provider-test-result--${result.ok ? 'ok' : 'fail'}`}>
          <div className="provider-test-result__head">
            <span className="provider-test-result__dot" aria-hidden />
            <span>{result.ok ? t.testWorking : t.testConnectionFailed}</span>
            <span className="provider-test-result__meta">
              {result.durationMs}ms · {formatRelative(result.testedAt)}
            </span>
          </div>

          <dl className="provider-test-result__grid">
            <div>
              <dt>{t.jobsFoundLabel}</dt>
              <dd className="tabular">{result.fetchedCount}</dd>
            </div>
            {result.error && (
              <div className="provider-test-result__error">
                <dt>{t.errorLabel}</dt>
                <dd>{result.error}</dd>
              </div>
            )}
          </dl>

          {result.hitPageCap && (
            <div className="provider-cap-warn">
              {t.hitPageCap()}
            </div>
          )}
          {!result.hitPageCap && result.possiblyCapped && (
            <div className="provider-cap-warn provider-cap-warn--soft">
              {t.possiblyCapped()}
            </div>
          )}

          {result.samples.length > 0 && (
            <div className="provider-test-preview">
              <div className="provider-test-preview__head">
                {result.fetchedCount > result.samples.length ? (
                  <span className="provider-test-preview__more">
                    {t.showingFirst(result.samples.length, result.fetchedCount)}
                  </span>
                ) : (
                  <>{t.allListings(result.fetchedCount)}</>
                )}
              </div>
              <ul className="provider-test-preview__list">
                {result.samples.map((s, i) => (
                  <li key={`${s.url}-${i}`} className="provider-test-preview__row">
                    <a href={s.url} target="_blank" rel="noreferrer" className="provider-test-preview__title">
                      {s.title}
                    </a>
                    {(s.company || s.location) && (
                      <span className="provider-test-preview__meta">
                        {[s.company, s.location].filter(Boolean).join(' · ')}
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </section>
  )
}
