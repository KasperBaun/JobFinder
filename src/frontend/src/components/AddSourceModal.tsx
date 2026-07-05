import { useEffect, useId, useState } from 'react'
import { createSource, detectSource, previewSource } from '../api/client'
import type { DetectedSource, ProviderTestResult } from '../api/types'

type Step = 'paste' | 'confirm' | 'notfound' | 'manual'

export function AddSourceModal({
  onClose,
  onCreated,
}: {
  onClose: () => void
  onCreated: (id: number, name: string) => void
}) {
  const titleId = useId()
  const [step, setStep] = useState<Step>('paste')
  const [url, setUrl] = useState('')
  const [candidate, setCandidate] = useState<DetectedSource | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [busy, setBusy] = useState(false)
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  async function run<T>(fn: () => Promise<T>): Promise<T | undefined> {
    setBusy(true)
    setError(null)
    try {
      return await fn()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
      return undefined
    } finally {
      setBusy(false)
    }
  }

  async function find() {
    setTestResult(null)
    const res = await run(() => detectSource(url))
    if (!res) return
    if (res.candidates.length === 0) {
      setStep('notfound')
      return
    }
    const c = res.candidates[0]
    setCandidate(c)
    setDisplayName(c.displayName)
    setStep('confirm')
  }

  async function test() {
    if (!candidate) return
    const res = await run(() => previewSource({ url, kind: candidate.kind, displayName }))
    if (res) setTestResult(res)
  }

  async function add(kind: string) {
    const res = await run(() =>
      createSource({ url: kind === 'manual' ? undefined : url, kind, displayName: displayName.trim() || undefined }),
    )
    if (res) onCreated(res.id, displayName.trim() || 'source')
  }

  function goManual() {
    setError(null)
    setTestResult(null)
    setDisplayName('')
    setStep('manual')
  }

  return (
    <div className="overlay" onClick={onClose}>
      <div
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-card__head">
          <h2 id={titleId} className="modal-card__title">Add a source</h2>
          <button type="button" className="modal-card__close" aria-label="Close" onClick={onClose}>×</button>
        </div>

        {step === 'paste' && (
          <div className="add-source__body">
            <p className="field__hint">
              Paste the web address of a company's jobs page or a job feed. We'll recognise the common
              ones and set them up for you.
            </p>
            <input
              className="input"
              type="url"
              autoFocus
              placeholder="https://boards.greenhouse.io/company"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && url.trim()) void find() }}
            />
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={busy || !url.trim()} onClick={() => void find()}>
                {busy ? <span className="spinner" /> : 'Find it'}
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={goManual}>
                Import a spreadsheet instead
              </button>
            </div>
          </div>
        )}

        {step === 'confirm' && candidate && (
          <div className="add-source__body">
            <div className="add-source__found">{candidate.summary}</div>
            {candidate.duplicateWarning && (
              <p className="add-source__warn">{candidate.duplicateWarning}</p>
            )}
            <label className="field__label" htmlFor={`${titleId}-name`}>Name</label>
            <input
              id={`${titleId}-name`}
              className="input"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
            {testResult && <TestResultLine result={testResult} />}
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={busy || !displayName.trim()} onClick={() => void add(candidate.kind)}>
                {busy ? <span className="spinner" /> : 'Add source'}
              </button>
              <button type="button" className="btn btn--secondary" disabled={busy} onClick={() => void test()}>
                Test first
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>Back</button>
            </div>
          </div>
        )}

        {step === 'notfound' && (
          <div className="add-source__body">
            <p className="field__hint">
              We couldn't recognise that address automatically. You can still add it as a manual import —
              you export the roles yourself and drop them in, and they'll be included in your next search.
            </p>
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" onClick={goManual}>Set up manual import</button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>Try another address</button>
            </div>
          </div>
        )}

        {step === 'manual' && (
          <div className="add-source__body">
            <label className="field__label" htmlFor={`${titleId}-manual`}>Name this source</label>
            <input
              id={`${titleId}-manual`}
              className="input"
              autoFocus
              placeholder="e.g. LinkedIn saved roles"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
            <p className="field__hint">
              After adding, export your roles to a CSV and save it in your imports folder. Open the source
              afterwards for the exact file name and columns.
            </p>
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={busy || !displayName.trim()} onClick={() => void add('manual')}>
                {busy ? <span className="spinner" /> : 'Add source'}
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>Back</button>
            </div>
          </div>
        )}

        {error && <p className="error-text add-source__error">{error}</p>}
      </div>
    </div>
  )
}

function TestResultLine({ result }: { result: ProviderTestResult }) {
  return (
    <div className={`provider-test-result provider-test-result--${result.ok ? 'ok' : 'fail'}`}>
      <div className="provider-test-result__head">
        <span className="provider-test-result__dot" aria-hidden />
        <span>{result.ok ? `Found ${result.fetchedCount} jobs` : 'Nothing came back'}</span>
        <span className="provider-test-result__meta">{result.durationMs}ms</span>
      </div>
      {result.sampleTitle && <div className="add-source__sample">e.g. “{result.sampleTitle}”</div>}
      {result.error && !result.ok && <div className="add-source__sample">{result.error}</div>}
    </div>
  )
}
