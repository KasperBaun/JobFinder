import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { deleteProvider, getProvider, setProviderEnabled } from '../api/client'
import { Toggle } from '../components/Toggle'
import { Toast } from '../components/Toast'
import { ConfigForm } from '../components/provider/ConfigForm'
import { OriginPanel } from '../components/provider/OriginPanel'
import { SecretsCard } from '../components/provider/SecretsCard'
import { TestPanel } from '../components/provider/TestPanel'
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
  const [confirmRemove, setConfirmRemove] = useState(false)

  const invalidateProvider = () => {
    void queryClient.invalidateQueries({ queryKey: ['provider', id] })
    void queryClient.invalidateQueries({ queryKey: ['providers'] })
  }

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
      invalidateProvider()
    },
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
            <OriginPanel data={data} />

            {data.type !== 'manual' && (
              <ConfigForm
                providerId={data.id}
                config={data.config}
                onSaved={invalidateProvider}
                onError={(message) => setToast({ kind: 'err', message })}
              />
            )}

            {data.requiresSecret && (
              <SecretsCard
                providerId={data.id}
                secretName={data.requiresSecret}
                hasSecret={data.hasSecret}
                onSaved={invalidateProvider}
              />
            )}

            <TestPanel
              providerId={data.id}
              providerType={data.type}
              onError={(message) => setToast({ kind: 'err', message })}
            />

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
