import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  deleteProvider,
  getProvider,
  setProviderEnabled,
  setProviderSecrets,
  testProvider,
} from '../api/client'
import type { ProviderTestResult } from '../api/types'
import { Toggle } from '../components/Toggle'
import { Toast } from '../components/Toast'
import { formatRelative } from '../utils/time'

export function ProviderDetailPage() {
  const { id: idParam } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const id = Number(idParam)
  const validId = Number.isFinite(id)

  const { data, isLoading, error } = useQuery({
    queryKey: ['provider', id],
    queryFn: () => getProvider(id),
    enabled: validId,
  })

  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const [testResult, setTestResult] = useState<ProviderTestResult | null>(null)
  const [confirmRemove, setConfirmRemove] = useState(false)

  const remove = useMutation({
    mutationFn: () => deleteProvider(id),
    onSuccess: (res) => {
      if (!res.success) {
        setToast({ kind: 'err', message: res.error ?? 'Remove failed' })
        return
      }
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
      navigate('/providers')
    },
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  const toggle = useMutation({
    mutationFn: (enabled: boolean) => setProviderEnabled(id, enabled),
    onSuccess: (res) => {
      if (!res.success) {
        setToast({ kind: 'err', message: res.error ?? 'Save failed' })
        return
      }
      void queryClient.invalidateQueries({ queryKey: ['provider', id] })
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
    },
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  const test = useMutation({
    mutationFn: () => testProvider(id),
    onMutate: () => setTestResult(null),
    onSuccess: (result) => setTestResult(result),
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  return (
    <div className="page">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <Link to="/providers" className="back-link">← all sources</Link>

      {isLoading && <div className="muted">Loading…</div>}
      {error && <div className="error-text">Failed to load source.</div>}

      {data && (
        <>
          <header className="page__header">
            <div className="provider-detail__head">
              <div className="provider-detail__head-left">
                <div className="page__eyebrow">source</div>
                <h1 className="page__heading provider-detail__heading">{data.displayName}</h1>
              </div>
              <Toggle
                checked={data.enabled}
                onChange={(v) => toggle.mutate(v)}
                label={data.enabled ? 'On' : 'Off'}
                ariaLabel="on"
              />
            </div>
          </header>

          <div className="provider-detail__sections">
            <section className="card">
              <h2 className="card__title">About this source</h2>
              <dl className="provider-detail__readonly">
                <ReadonlyField label="Type" value={friendlyType(data.type)} />
                {data.notes && <ReadonlyField label="Notes" value={data.notes} />}
              </dl>
            </section>

            {data.requiresSecret && (
              <SecretsCard
                providerId={data.id}
                secretName={data.requiresSecret}
                hasSecret={data.hasSecret}
                onSaved={() => {
                  void queryClient.invalidateQueries({ queryKey: ['provider', data.id] })
                  void queryClient.invalidateQueries({ queryKey: ['providers'] })
                }}
              />
            )}

            <section className="card">
              <div className="row-spread">
                <h2 className="card__title" style={{ marginBottom: 0 }}>Test the source</h2>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => test.mutate()}
                  disabled={test.isPending || data.type === 'manual'}
                >
                  {test.isPending ? <span className="spinner" /> : 'Test now'}
                </button>
              </div>
              <p className="field__hint" style={{ marginTop: 'var(--space-3)' }}>
                {data.type === 'manual'
                  ? "This source doesn't fetch automatically — there's nothing to test."
                  : 'Pulls listings once and shows how many came back, how long it took, and one example title.'}
              </p>
              {testResult && (
                <div className={`provider-test-result provider-test-result--${testResult.ok ? 'ok' : 'fail'}`}>
                  <div className="provider-test-result__head">
                    <span className="provider-test-result__dot" aria-hidden />
                    <span>{testResult.ok ? 'Working' : 'Connection failed'}</span>
                    <span className="provider-test-result__meta">
                      {testResult.durationMs}ms · {formatRelative(testResult.testedAt)}
                    </span>
                  </div>
                  <dl className="provider-test-result__grid">
                    <div>
                      <dt>jobs found</dt>
                      <dd className="tabular">{testResult.fetchedCount}</dd>
                    </div>
                    {testResult.sampleTitle && (
                      <div>
                        <dt>example</dt>
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

            {data.removable && (
              <section className="card">
                <div className="row-spread">
                  <div>
                    <h2 className="card__title" style={{ marginBottom: 0 }}>Remove this source</h2>
                    <p className="field__hint" style={{ marginTop: 'var(--space-3)' }}>
                      You added this source, so you can remove it. This only affects your setup.
                    </p>
                  </div>
                  {confirmRemove ? (
                    <div className="add-source__actions">
                      <button type="button" className="btn btn--danger btn--sm" disabled={remove.isPending} onClick={() => remove.mutate()}>
                        {remove.isPending ? <span className="spinner" /> : 'Yes, remove'}
                      </button>
                      <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirmRemove(false)}>Cancel</button>
                    </div>
                  ) : (
                    <button type="button" className="btn btn--danger btn--sm" onClick={() => setConfirmRemove(true)}>Remove</button>
                  )}
                </div>
              </section>
            )}

            {data.recentRuns.length > 0 && (
              <section className="card">
                <h2 className="card__title">Recent searches</h2>
                <ul className="provider-recent-runs">
                  {data.recentRuns.map((r) => (
                    <li key={r.runId} className={`provider-recent-runs__row provider-recent-runs__row--${r.status}`}>
                      <span className="provider-recent-runs__date tabular">{formatRelative(r.startedAt)}</span>
                      <span className="provider-recent-runs__status">{r.status}</span>
                      <span className="provider-recent-runs__count tabular">
                        {typeof r.fetchedCount === 'number' ? r.fetchedCount : '—'}
                      </span>
                      <Link to={`/history/${r.runId}`} className="provider-recent-runs__link">view search →</Link>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function friendlyType(type: string): string {
  switch (type) {
    case 'api':        return 'Auto-fetched'
    case 'rss':        return 'News feed'
    case 'html':       return 'Read from website'
    case 'teamtailor': return 'Auto-fetched'
    case 'hrmanager':  return 'Auto-fetched'
    case 'manual':     return 'Manual import'
    default:           return type
  }
}

function friendlySecretLabel(name: string): string {
  switch (name) {
    case 'api_key': return 'API key'
    case 'affid':   return 'Affiliate ID'
    default:        return 'Access key'
  }
}

function ReadonlyField({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="provider-detail__readonly-row">
      <dt>{label}</dt>
      <dd className={mono ? 'mono' : undefined}>{value}</dd>
    </div>
  )
}

function SecretsCard({
  providerId,
  secretName,
  hasSecret,
  onSaved,
}: {
  providerId: number
  secretName: string
  hasSecret: boolean
  onSaved: () => void
}) {
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'err'; text: string } | null>(null)

  async function save() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: value })
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      setValue('')
      setMsg({ kind: 'ok', text: 'Saved.' })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'err', text: e instanceof Error ? e.message : String(e) })
    } finally {
      setSaving(false)
    }
  }

  async function clear() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: '' })
      if (!res.success) throw new Error(res.error ?? 'Clear failed')
      setMsg({ kind: 'ok', text: 'Cleared.' })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'err', text: e instanceof Error ? e.message : String(e) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card">
      <h2 className="card__title">{friendlySecretLabel(secretName)}</h2>
      <p className="field__hint">
        Saved on this computer only. Until you save a value here, this source is skipped when you search.
      </p>
      <div className="secrets-form">
        <input
          className="input input--mono"
          type="password"
          autoComplete="off"
          placeholder={hasSecret ? '••••••••  (overwrite to update)' : `Paste your ${friendlySecretLabel(secretName)}`}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          disabled={saving}
        />
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={saving || value.length === 0}
          onClick={save}
        >
          {saving ? <span className="spinner" /> : 'Save'}
        </button>
        {hasSecret && (
          <button type="button" className="btn btn--ghost btn--sm" disabled={saving} onClick={clear}>
            Clear
          </button>
        )}
        {msg && (
          <span className={msg.kind === 'ok' ? 'muted small' : 'error-text small'}>
            {msg.text}
          </span>
        )}
      </div>
    </section>
  )
}
