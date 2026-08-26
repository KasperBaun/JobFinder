import { useEffect, useId, useState } from 'react'
import { createSource, detectSource, previewSource } from '../api/client'
import type { DetectedSource, SourceOverlap, SourcePreviewResult } from '../api/types'
import { useT } from '../i18n'
import { SourceOverlapNotice } from './addSource/SourceOverlapNotice'
import { TestResultLine } from './addSource/TestResultLine'

type Step = 'paste' | 'confirm' | 'notfound' | 'manual'

export function AddSourceModal({
  onClose,
  onCreated,
  onOpenExisting,
}: {
  onClose: () => void
  onCreated: (id: number, name: string) => void
  onOpenExisting: (overlap: SourceOverlap) => void
}) {
  const t = useT('sources')
  const common = useT('common')
  const titleId = useId()
  const [step, setStep] = useState<Step>('paste')
  const [url, setUrl] = useState('')
  const [candidate, setCandidate] = useState<DetectedSource | null>(null)
  const [displayName, setDisplayName] = useState('')
  const [busy, setBusy] = useState(false)
  const [preview, setPreview] = useState<SourcePreviewResult | null>(null)
  const [previewing, setPreviewing] = useState(false)
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
    setPreview(null)
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
    // Recognising the address is not the answer the user came for — whether it returns jobs is. The
    // same call reports any existing source already bringing those jobs in.
    void fetchPreview(c)
  }

  async function fetchPreview(c: DetectedSource) {
    setPreviewing(true)
    try {
      const res = await run(() => previewSource({ url, kind: c.kind, displayName: c.displayName }))
      if (res) setPreview(res)
    } finally {
      setPreviewing(false)
    }
  }

  async function add(kind: string) {
    const res = await run(() =>
      createSource({ url: kind === 'manual' ? undefined : url, kind, displayName: displayName.trim() || undefined }),
    )
    if (res) onCreated(res.id, displayName.trim() || t.fallbackName)
  }

  function goManual() {
    setError(null)
    setPreview(null)
    setDisplayName('')
    setStep('manual')
  }

  const duplicate = preview?.overlap?.duplicate === true

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
          <h2 id={titleId} className="modal-card__title">{t.title}</h2>
          <button type="button" className="modal-card__close" aria-label={t.close} onClick={onClose}>×</button>
        </div>

        {step === 'paste' && (
          <div className="add-source__body">
            <p className="field__hint">{t.pasteHint}</p>
            <input
              className="input"
              type="url"
              autoFocus
              placeholder={t.urlPlaceholder}
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && url.trim()) void find() }}
            />
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={busy || !url.trim()} onClick={() => void find()}>
                {busy ? <span className="spinner" /> : t.findIt}
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={goManual}>
                {t.importSpreadsheet}
              </button>
            </div>
          </div>
        )}

        {step === 'confirm' && candidate && (
          <div className="add-source__body">
            <div className="add-source__found">{candidate.summary}</div>
            <label className="field__label" htmlFor={`${titleId}-name`}>{t.nameLabel}</label>
            <input
              id={`${titleId}-name`}
              className="input"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
            {previewing && <p className="field__hint">{preview ? t.checkingExisting : t.fetching}</p>}
            {preview && <TestResultLine result={preview.test} />}
            {preview?.overlap && (
              <SourceOverlapNotice
                overlap={preview.overlap}
                fetchedCount={preview.test.fetchedCount}
                onOpenExisting={onOpenExisting}
              />
            )}
            <div className="add-source__actions">
              <button
                type="button"
                className={`btn ${duplicate ? 'btn--secondary' : 'btn--primary'}`}
                disabled={busy || !displayName.trim()}
                onClick={() => void add(candidate.kind)}
              >
                {busy && !previewing ? <span className="spinner" /> : duplicate ? t.addAnyway : t.addSource}
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>{common.back}</button>
            </div>
          </div>
        )}

        {step === 'notfound' && (
          <div className="add-source__body">
            <p className="field__hint">{t.notFoundHint}</p>
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" onClick={goManual}>{t.setUpManual}</button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>{t.tryAnother}</button>
            </div>
          </div>
        )}

        {step === 'manual' && (
          <div className="add-source__body">
            <label className="field__label" htmlFor={`${titleId}-manual`}>{t.manualNameLabel}</label>
            <input
              id={`${titleId}-manual`}
              className="input"
              autoFocus
              placeholder={t.manualNamePlaceholder}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
            <p className="field__hint">{t.manualHint}</p>
            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={busy || !displayName.trim()} onClick={() => void add('manual')}>
                {busy ? <span className="spinner" /> : t.addSource}
              </button>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStep('paste')}>{common.back}</button>
            </div>
          </div>
        )}

        {error && <p className="error-text add-source__error">{error}</p>}
      </div>
    </div>
  )
}
