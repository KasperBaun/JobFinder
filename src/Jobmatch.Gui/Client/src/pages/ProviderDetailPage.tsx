import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createProvider,
  deleteProvider,
  getProvider,
  testProvider,
  updateProvider,
} from '../api/client'
import type { ProviderDetail, ProviderTestResult, ProviderType, ProviderUpsert } from '../api/types'
import { Toggle } from '../components/Toggle'
import { SaveBar } from '../components/SaveBar'
import { Toast } from '../components/Toast'
import { formatRelative } from '../utils/time'

const TYPES: ProviderType[] = ['api', 'rss', 'html', 'manual']

const BLANK: ProviderUpsert = {
  name: '',
  type: 'rss',
  enabled: true,
  endpoint: '',
  rateLimitRps: 1.0,
  notes: '',
}

function fromDetail(d: ProviderDetail): ProviderUpsert {
  return {
    name: d.name,
    type: d.type,
    enabled: d.enabled,
    endpoint: d.endpoint ?? '',
    rateLimitRps: d.rateLimitRps,
    notes: d.notes ?? '',
  }
}

export function ProviderDetailPage() {
  const { id: idParam } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const isNew = idParam === 'new'
  const id = isNew ? null : Number(idParam)
  const validId = !isNew && id !== null && Number.isFinite(id)

  const { data, isLoading, error } = useQuery({
    queryKey: ['provider', id],
    queryFn: () => getProvider(id as number),
    enabled: validId,
  })

  const [form, setForm] = useState<ProviderUpsert>(BLANK)
  const [original, setOriginal] = useState<ProviderUpsert | null>(null)
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)

  useEffect(() => {
    if (isNew) {
      setForm(BLANK)
      setOriginal(BLANK)
      return
    }
    if (data) {
      const u = fromDetail(data)
      setForm(u)
      setOriginal(u)
    }
  }, [data, isNew])

  const dirty = useMemo(() => {
    if (!original) return false
    return JSON.stringify(form) !== JSON.stringify(original)
  }, [form, original])

  const save = useMutation({
    mutationFn: async () => {
      if (isNew) {
        const res = await createProvider(form)
        if (!res.success) throw new Error(res.error ?? 'Create failed')
        return res.id
      }
      const res = await updateProvider(id as number, form)
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      return id as number
    },
    onSuccess: (savedId) => {
      setToast({ kind: 'ok', message: isNew ? 'Provider created' : 'Provider saved' })
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
      void queryClient.invalidateQueries({ queryKey: ['provider', savedId] })
      if (isNew) navigate(`/providers/${savedId}`, { replace: true })
      else setOriginal(form)
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  const remove = useMutation({
    mutationFn: async () => {
      const res = await deleteProvider(id as number)
      if (!res.success) throw new Error(res.error ?? 'Delete failed')
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
      navigate('/providers')
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
      setConfirmDelete(false)
    },
  })

  const test = useMutation({
    mutationFn: () => testProvider(id as number),
    onMutate: () => setTestResult(null),
    onSuccess: (result) => setTestResult(result),
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function patch(p: Partial<ProviderUpsert>) {
    setForm((f) => ({ ...f, ...p }))
  }

  return (
    <div className="page">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <Link to="/providers" className="back-link">← all providers</Link>

      {!isNew && isLoading && <div className="muted">Loading…</div>}
      {!isNew && error && <div className="error-text">Failed to load provider.</div>}

      {(isNew || data) && (
        <>
          <header className="page__header">
            <div className="provider-detail__head">
              <div className="provider-detail__head-left">
                <div className="page__eyebrow">
                  {isNew ? 'new provider' : `provider · #${data?.id}`}
                </div>
                <h1 className="page__heading provider-detail__heading">
                  {isNew ? <em>New source</em> : <>{data?.name}</>}
                </h1>
              </div>
              {!isNew && (
                <Toggle
                  checked={form.enabled}
                  onChange={(v) => patch({ enabled: v })}
                  label={form.enabled ? 'Enabled' : 'Disabled'}
                  ariaLabel="enabled"
                />
              )}
            </div>
          </header>

          <div className="provider-detail__sections">
          <section className="card">
            <h2 className="card__title">Configuration</h2>
            <div className="provider-detail__form">
              <div className="field">
                <label className="field__label">Name</label>
                <input
                  className="input"
                  value={form.name}
                  onChange={(e) => patch({ name: e.target.value })}
                />
              </div>
              <div className="field">
                <label className="field__label">Type</label>
                <select
                  className="select"
                  value={form.type}
                  onChange={(e) => patch({ type: e.target.value as ProviderType })}
                >
                  {TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div className="field">
                <label className="field__label">Rate limit (rps)</label>
                <input
                  type="number"
                  step={0.1}
                  min={0}
                  className="input input--mono tabular"
                  value={form.rateLimitRps}
                  onChange={(e) => patch({ rateLimitRps: Number(e.target.value) || 0 })}
                />
              </div>
              <div className="field provider-detail__field--wide">
                <label className="field__label">Endpoint</label>
                <input
                  className="input input--mono"
                  value={form.endpoint ?? ''}
                  placeholder={form.type === 'manual' ? '(none — manual provider)' : 'https://…'}
                  onChange={(e) => patch({ endpoint: e.target.value })}
                  disabled={form.type === 'manual'}
                />
              </div>
              <div className="field provider-detail__field--wide">
                <label className="field__label">Notes</label>
                <input
                  className="input"
                  value={form.notes ?? ''}
                  placeholder="optional"
                  onChange={(e) => patch({ notes: e.target.value })}
                />
              </div>
              {isNew && (
                <p className="field__hint provider-detail__field--wide">
                  Advanced fields (selectors, query params, headers, response mapping)
                  can be added by editing <code>portals.yml</code> directly. They round-trip through the GUI safely.
                </p>
              )}
            </div>
          </section>

          {!isNew && (
            <section className="card">
              <div className="row-spread">
                <h2 className="card__title" style={{ marginBottom: 0 }}>Test connection</h2>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => test.mutate()}
                  disabled={test.isPending || form.type === 'manual'}
                >
                  {test.isPending ? <span className="spinner" /> : 'Run test'}
                </button>
              </div>
              <p className="field__hint" style={{ marginTop: 'var(--space-3)' }}>
                {form.type === 'manual'
                  ? 'Manual providers have no live endpoint — nothing to test.'
                  : 'Runs the actual adapter once and reports listings fetched, latency, and a sample title.'}
              </p>
              {testResult && (
                <div className={`provider-test-result provider-test-result--${testResult.ok ? 'ok' : 'fail'}`}>
                  <div className="provider-test-result__head">
                    <span className="provider-test-result__dot" aria-hidden />
                    <span>{testResult.ok ? 'Connection healthy' : 'Connection failed'}</span>
                    <span className="provider-test-result__meta">
                      {testResult.durationMs}ms · {formatRelative(testResult.testedAt)}
                    </span>
                  </div>
                  <dl className="provider-test-result__grid">
                    <div>
                      <dt>fetched</dt>
                      <dd className="tabular">{testResult.fetchedCount}</dd>
                    </div>
                    {testResult.sampleTitle && (
                      <div>
                        <dt>sample</dt>
                        <dd>{testResult.sampleTitle}</dd>
                      </div>
                    )}
                    {testResult.error && (
                      <div className="provider-test-result__error">
                        <dt>error</dt>
                        <dd>{testResult.error}</dd>
                      </div>
                    )}
                  </dl>
                </div>
              )}
            </section>
          )}

          {!isNew && data && data.recentRuns.length > 0 && (
            <section className="card">
              <h2 className="card__title">Recent runs</h2>
              <ul className="provider-recent-runs">
                {data.recentRuns.map((r) => (
                  <li key={r.runId} className={`provider-recent-runs__row provider-recent-runs__row--${r.status}`}>
                    <span className="provider-recent-runs__date tabular">{formatRelative(r.startedAt)}</span>
                    <span className="provider-recent-runs__status">{r.status}</span>
                    <span className="provider-recent-runs__count tabular">
                      {typeof r.fetchedCount === 'number' ? r.fetchedCount : '—'}
                    </span>
                    <Link to={`/history/${r.runId}`} className="provider-recent-runs__link">view run →</Link>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {!isNew && (
            <section className="card provider-danger">
              <h2 className="card__title">Danger zone</h2>
              <p className="field__hint">Removes the provider from <code>portals.yml</code>. Past run history attribution is preserved.</p>
              {!confirmDelete ? (
                <button type="button" className="btn btn--danger" onClick={() => setConfirmDelete(true)}>
                  Delete provider
                </button>
              ) : (
                <div className="provider-danger__confirm">
                  <span>Delete <strong>{data?.name}</strong>?</span>
                  <button type="button" className="btn btn--danger" onClick={() => remove.mutate()} disabled={remove.isPending}>
                    {remove.isPending ? <span className="spinner" /> : 'Yes, delete'}
                  </button>
                  <button type="button" className="btn btn--ghost" onClick={() => setConfirmDelete(false)}>
                    Cancel
                  </button>
                </div>
              )}
            </section>
          )}

          </div>

          <SaveBar
            visible={dirty}
            message={isNew ? 'New provider — fill in fields to create' : 'Unsaved changes'}
            saving={save.isPending}
            onSave={() => save.mutate()}
            onRevert={() => original && setForm(original)}
          />
        </>
      )}
    </div>
  )
}
