import { useEffect, useId, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getLlmStatus } from '../api/client'
import type { ExtractedProfile, SkillsetUpdateRequest } from '../api/types'
import { useCvExtraction } from '../hooks/useCvExtraction'
import { useElapsed } from '../hooks/useElapsed'
import { LlmModelBanner } from './LlmModelBanner'
import { useT } from '../i18n'
import type { Messages } from '../i18n'

type Form = SkillsetUpdateRequest
type FieldKey = keyof Form
type Mode = 'paste' | 'file' | 'url'

type FieldSpec = { key: FieldKey; labelKey: keyof Messages['cv']['fields']; kind: 'text' | 'number' | 'list' }

const FIELDS: FieldSpec[] = [
  { key: 'name', labelKey: 'name', kind: 'text' },
  { key: 'location', labelKey: 'location', kind: 'text' },
  { key: 'country', labelKey: 'country', kind: 'text' },
  { key: 'region', labelKey: 'region', kind: 'text' },
  { key: 'metro', labelKey: 'metro', kind: 'list' },
  { key: 'experienceYears', labelKey: 'experienceYears', kind: 'number' },
  { key: 'seniority', labelKey: 'seniority', kind: 'text' },
  { key: 'remotePreference', labelKey: 'remotePreference', kind: 'text' },
  { key: 'targetRoles', labelKey: 'targetRoles', kind: 'list' },
  { key: 'primaryStack', labelKey: 'primaryStack', kind: 'list' },
  { key: 'secondaryStack', labelKey: 'secondaryStack', kind: 'list' },
  { key: 'domains', labelKey: 'domains', kind: 'list' },
  { key: 'languages', labelKey: 'languages', kind: 'list' },
  { key: 'employmentTypes', labelKey: 'employmentTypes', kind: 'list' },
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
  const t = useT('cv')
  const { close } = useT('common')
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
          <h2 id={titleId} className="modal-card__title">{t.title}</h2>
          <button type="button" className="modal-card__close" aria-label={close} onClick={onClose}>×</button>
        </div>

        {llm.data && !llm.data.enabled && (
          <p className="field__hint">{t.aiDisabled()}</p>
        )}

        {llm.data?.enabled && !llm.data.modelPresent && (
          <div className="cv-import__body">
            <p className="field__hint">{t.modelMissing}</p>
            <LlmModelBanner />
          </div>
        )}

        {ready && extracting && (
          <div className="cv-import__body">
            <p><span className="spinner" /> {t.reading} <strong>{elapsed}</strong></p>
            <p className="field__hint">{t.readingHint}</p>
          </div>
        )}

        {ready && reviewing && (
          <div className="cv-import__body">
            <p className="field__hint">{t.reviewHint}</p>
            {diff.length === 0 ? (
              <p className="field__hint">{t.nothingNew}</p>
            ) : (
              <table className="cv-import__table">
                <thead>
                  <tr><th /><th>{t.colField}</th><th>{t.colCurrent}</th><th>{t.colFromCv}</th></tr>
                </thead>
                <tbody>
                  {diff.map((row) => (
                    <tr key={row.spec.key} className={unchecked.has(row.spec.key) ? 'cv-import__row--off' : ''}>
                      <td>
                        <input
                          type="checkbox"
                          aria-label={t.applyAria(t.fields[row.spec.labelKey])}
                          checked={!unchecked.has(row.spec.key)}
                          onChange={() => toggle(row.spec.key)}
                        />
                      </td>
                      <td>{t.fields[row.spec.labelKey]}</td>
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
                  {t.applyFields(diff.filter((r) => !unchecked.has(r.spec.key)).length)}
                </button>
              )}
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDismissedResult(true)}>
                {t.startOver}
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
                  {m === 'paste' ? t.modePaste : m === 'file' ? t.modeFile : t.modeUrl}
                </button>
              ))}
            </div>

            {mode === 'paste' && (
              <textarea
                className="input cv-import__textarea"
                rows={8}
                autoFocus
                placeholder={t.pastePlaceholder}
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
                <p className="field__hint">{t.fileHint}</p>
              </>
            )}
            {mode === 'url' && (
              <input
                className="input"
                type="url"
                autoFocus
                placeholder={t.urlPlaceholder}
                value={url}
                onChange={(e) => setUrl(e.target.value)}
              />
            )}

            {status?.state === 'failed' && !startError && (
              <p className="error-text">{t.extractionFailed(status.error ?? t.unknownError)}</p>
            )}
            {startError && <p className="error-text">{startError}</p>}

            <div className="add-source__actions">
              <button type="button" className="btn btn--primary" disabled={!canStart} onClick={() => void onStart()}>
                {busy ? <span className="spinner" /> : t.readMyCv}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
