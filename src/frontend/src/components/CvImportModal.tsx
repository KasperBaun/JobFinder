import { useEffect, useId, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getLlmStatus } from '../api/client'
import type { ExtractedProfile, SkillsetUpdateRequest } from '../api/types'
import { useCvExtraction } from '../hooks/useCvExtraction'
import { useElapsed } from '../hooks/useElapsed'
import { LlmModelBanner } from './LlmModelBanner'

type Form = SkillsetUpdateRequest
type FieldKey = keyof Form
type Mode = 'paste' | 'file' | 'url'

type FieldSpec = { key: FieldKey; label: string; kind: 'text' | 'number' | 'list' }

const FIELDS: FieldSpec[] = [
  { key: 'name', label: 'Name', kind: 'text' },
  { key: 'location', label: 'Location', kind: 'text' },
  { key: 'country', label: 'Country', kind: 'text' },
  { key: 'region', label: 'Region', kind: 'text' },
  { key: 'metro', label: 'Cities / areas', kind: 'list' },
  { key: 'experienceYears', label: 'Years of experience', kind: 'number' },
  { key: 'seniority', label: 'Experience level', kind: 'text' },
  { key: 'remotePreference', label: 'Where you want to work', kind: 'text' },
  { key: 'targetRoles', label: 'Roles you want', kind: 'list' },
  { key: 'primaryStack', label: 'Must-have skills', kind: 'list' },
  { key: 'secondaryStack', label: 'Nice-to-have skills', kind: 'list' },
  { key: 'domains', label: 'Industries', kind: 'list' },
  { key: 'languages', label: 'Languages', kind: 'list' },
  { key: 'employmentTypes', label: 'Employment types', kind: 'list' },
]

type DiffRow = { spec: FieldSpec; current: string; suggested: string; value: Form[FieldKey] }

function display(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—'
  if (Array.isArray(value)) return value.length > 0 ? value.join(', ') : '—'
  return String(value)
}

// One row per field the CV stated something for, where it differs from the form.
function buildDiff(profile: ExtractedProfile, current: Form, keys?: FieldKey[]): DiffRow[] {
  const rows: DiffRow[] = []
  for (const spec of FIELDS) {
    if (keys && !keys.includes(spec.key)) continue
    const suggested = profile[spec.key as keyof ExtractedProfile]
    const empty = suggested === null || suggested === undefined
      || suggested === '' || (Array.isArray(suggested) && suggested.length === 0)
    if (empty) continue
    const cur = current[spec.key]
    if (JSON.stringify(suggested) === JSON.stringify(cur)) continue
    rows.push({
      spec,
      current: display(cur),
      suggested: display(suggested),
      value: suggested as Form[FieldKey],
    })
  }
  return rows
}

// CV → profile prefill (R-011). The extraction runs server-side and this dialog only
// observes it; applying a suggestion patches the form in memory — nothing is saved
// until the user hits Save on the page itself (R-012).
export function CvImportModal({
  current,
  fields,
  onApply,
  onClose,
}: {
  current: Form
  fields?: FieldKey[]
  onApply: (patch: Partial<Form>, keys: FieldKey[]) => void
  onClose: () => void
}) {
  const titleId = useId()
  const llm = useQuery({ queryKey: ['llm-status'], queryFn: getLlmStatus, refetchOnWindowFocus: false })
  const ready = llm.data?.enabled === true && llm.data.modelPresent
  const { status, start } = useCvExtraction(ready === true)

  const [mode, setMode] = useState<Mode>('paste')
  const [text, setText] = useState('')
  const [url, setUrl] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [startError, setStartError] = useState<string | null>(null)
  const [dismissedResult, setDismissedResult] = useState(false)
  const [unchecked, setUnchecked] = useState<Set<FieldKey>>(new Set())

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const extracting = status?.state === 'extracting'
  const reviewing = status?.state === 'completed' && !!status.profile && !dismissedResult
  const elapsed = useElapsed(status?.startedAt ?? undefined, undefined, extracting)

  const canStart = !busy && (
    (mode === 'paste' && text.trim().length > 0)
    || (mode === 'file' && file !== null)
    || (mode === 'url' && url.trim().length > 0))

  async function onStart() {
    setStartError(null)
    setBusy(true)
    try {
      setDismissedResult(false)
      setUnchecked(new Set())
      await start(
        mode === 'paste' ? { text: text.trim() }
        : mode === 'file' ? { file: file! }
        : { url: url.trim() })
    } catch (e) {
      setStartError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  function toggle(key: FieldKey) {
    setUnchecked((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function apply(rows: DiffRow[]) {
    const accepted = rows.filter((r) => !unchecked.has(r.spec.key))
    const patch: Partial<Form> = {}
    for (const row of accepted) {
      // Safe: row.value came from the same key's slot in ExtractedProfile.
      ;(patch as Record<string, unknown>)[row.spec.key] = row.value
    }
    onApply(patch, accepted.map((r) => r.spec.key))
  }

  const diff = reviewing ? buildDiff(status!.profile!, current, fields) : []

  return (
    <div className="overlay" onClick={onClose}>
      <div
        className="modal-card modal-card--wide"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-card__head">
          <h2 id={titleId} className="modal-card__title">Fill from CV</h2>
          <button type="button" className="modal-card__close" aria-label="Close" onClick={onClose}>×</button>
        </div>

        {llm.data && !llm.data.enabled && (
          <p className="field__hint">
            AI review is turned off (<code>llm.enabled</code> in <code>ranking.yml</code>), and reading a CV
            needs the local AI model. Enable it and come back.
          </p>
        )}

        {llm.data?.enabled && !llm.data.modelPresent && (
          <div className="cv-import__body">
            <p className="field__hint">
              Reading a CV uses the local AI model, which hasn't been downloaded yet. Start the download
              below — you can keep using the app and come back when it's done.
            </p>
            <LlmModelBanner />
          </div>
        )}

        {ready && extracting && (
          <div className="cv-import__body">
            <p><span className="spinner" /> Reading your CV… <strong>{elapsed}</strong></p>
            <p className="field__hint">
              This runs the local AI model — typically a minute or two on CPU. You can close this dialog
              or navigate away; it keeps running and the result will be here when you return.
            </p>
          </div>
        )}

        {ready && reviewing && (
          <div className="cv-import__body">
            <p className="field__hint">
              Here's what the CV states, next to what your profile has now. Applying only fills the form —
              review the result and hit Save to keep it.
            </p>
            {diff.length === 0 ? (
              <p className="field__hint">Nothing new — your profile already covers everything the CV states.</p>
            ) : (
              <table className="cv-import__table">
                <thead>
                  <tr><th /><th>Field</th><th>Current</th><th>From CV</th></tr>
                </thead>
                <tbody>
                  {diff.map((row) => (
                    <tr key={row.spec.key} className={unchecked.has(row.spec.key) ? 'cv-import__row--off' : ''}>
                      <td>
                        <input
                          type="checkbox"
                          aria-label={`Apply ${row.spec.label}`}
                          checked={!unchecked.has(row.spec.key)}
                          onChange={() => toggle(row.spec.key)}
                        />
                      </td>
                      <td>{row.spec.label}</td>
                      <td className="cv-import__current">{row.current}</td>
                      <td>{row.suggested}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <div className="add-source__actions">
              {diff.length > 0 && (
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={diff.every((r) => unchecked.has(r.spec.key))}
                  onClick={() => apply(diff)}
                >
                  Apply {diff.filter((r) => !unchecked.has(r.spec.key)).length} field(s)
                </button>
              )}
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDismissedResult(true)}>
                Start over
              </button>
            </div>
          </div>
        )}

        {ready && !extracting && !reviewing && (
          <div className="cv-import__body">
            <div className="cv-import__modes" role="tablist">
              {(['paste', 'file', 'url'] as const).map((m) => (
                <button
                  key={m}
                  type="button"
                  role="tab"
                  aria-selected={mode === m}
                  className={`btn btn--sm ${mode === m ? 'btn--secondary' : 'btn--ghost'}`}
                  onClick={() => setMode(m)}
                >
                  {m === 'paste' ? 'Paste text' : m === 'file' ? 'Upload file' : 'From a link'}
                </button>
              ))}
            </div>

            {mode === 'paste' && (
              <textarea
                className="input cv-import__textarea"
                rows={8}
                autoFocus
                placeholder="Paste the full text of your CV here…"
                value={text}
                onChange={(e) => setText(e.target.value)}
              />
            )}
            {mode === 'file' && (
              <>
                <input
                  className="input"
                  type="file"
                  accept=".pdf,.txt,.md"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
                <p className="field__hint">.pdf, .txt or .md — for Word documents, paste the text instead.</p>
              </>
            )}
            {mode === 'url' && (
              <input
                className="input"
                type="url"
                autoFocus
                placeholder="https://example.com/my-cv.pdf"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
              />
            )}

            {status?.state === 'failed' && !startError && (
              <p className="error-text">Extraction failed: {status.error ?? 'unknown error'}</p>
            )}
            {startError && <p className="error-text">{startError}</p>}

            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={!canStart} onClick={() => void onStart()}>
                {busy ? <span className="spinner" /> : 'Read my CV'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
