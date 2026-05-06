import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProviders, setProviderEnabled, testProvider } from '../api/client'
import type { ProviderSummary, ProviderTestResult } from '../api/types'
import { Toast } from '../components/Toast'
import { formatRelative } from '../utils/time'

type Health = 'working' | 'failing' | 'stale' | 'untested'

type SessionTest = { kind: 'pending' } | { kind: 'done'; result: ProviderTestResult }

const STALE_DAYS = 14

function classifyHealth(p: ProviderSummary, sessionTest?: SessionTest): Health {
  if (sessionTest?.kind === 'done') {
    return sessionTest.result.ok ? 'working' : 'failing'
  }
  if (!p.lastFetchedAt) return 'untested'
  const ageMs = Date.now() - new Date(p.lastFetchedAt).getTime()
  const stale = ageMs > STALE_DAYS * 24 * 60 * 60 * 1000
  if (stale) return 'stale'
  return (p.lastFetchCount ?? 0) > 0 ? 'working' : 'failing'
}

const HEALTH_LABEL: Record<Health, string> = {
  working: 'working',
  failing: 'failing',
  stale: 'stale',
  untested: 'untested',
}

export function ProvidersPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const [tests, setTests] = useState<Record<number, SessionTest>>({})

  const toggle = useMutation({
    mutationFn: async ({ p, enabled }: { p: ProviderSummary; enabled: boolean }) => {
      const res = await setProviderEnabled(p.id, enabled)
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      return enabled
    },
    onMutate: async ({ p, enabled }) => {
      await queryClient.cancelQueries({ queryKey: ['providers'] })
      const prev = queryClient.getQueryData(['providers'])
      queryClient.setQueryData(['providers'], (old: { providers: ProviderSummary[] } | undefined) =>
        old
          ? { providers: old.providers.map((x) => (x.id === p.id ? { ...x, enabled } : x)) }
          : old,
      )
      return { prev }
    },
    onError: (err, _vars, ctx) => {
      if (ctx?.prev) queryClient.setQueryData(['providers'], ctx.prev)
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
    },
  })

  const test = useMutation({
    mutationFn: async (id: number) => {
      const result = await testProvider(id)
      return { id, result }
    },
    onMutate: (id: number) => {
      setTests((t) => ({ ...t, [id]: { kind: 'pending' } }))
    },
    onSuccess: ({ id, result }) => {
      setTests((t) => ({ ...t, [id]: { kind: 'done', result } }))
      setToast({
        kind: result.ok ? 'ok' : 'err',
        message: result.ok
          ? `${nameById(data?.providers, id)}: ${result.fetchedCount} listings · ${result.durationMs}ms`
          : `${nameById(data?.providers, id)}: ${result.error ?? 'failed'}`,
      })
    },
    onError: (err, vars) => {
      setTests((t) => {
        const copy = { ...t }
        delete copy[vars]
        return copy
      })
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  const stats = useMemo(() => {
    if (!data) return null
    const total = data.providers.length
    let enabled = 0
    let working = 0
    let failing = 0
    let stale = 0
    let untested = 0
    for (const p of data.providers) {
      if (p.enabled) enabled++
      const h = classifyHealth(p, tests[p.id])
      if (h === 'working') working++
      else if (h === 'failing') failing++
      else if (h === 'stale') stale++
      else untested++
    }
    return { total, enabled, disabled: total - enabled, working, failing, stale, untested }
  }, [data, tests])

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header page__header--with-action">
        <div>
          <div className="page__eyebrow">01 / sources</div>
          <h1 className="page__heading">Job <em>portals</em></h1>
          <p className="page__lede">
            Where listings come from. Toggle, test, or edit any provider.
          </p>
        </div>
      </header>

      {isLoading && <div className="muted">Loading providers…</div>}
      {error && <div className="error-text">Failed to load providers.</div>}

      {stats && (
        <div className="provider-stats">
          <Stat label="total" value={stats.total} />
          <Stat label="enabled" value={stats.enabled} tone={stats.enabled > 0 ? 'good' : undefined} />
          <Stat label="disabled" value={stats.disabled} tone="muted" />
          <span className="provider-stats__sep" aria-hidden />
          <Stat label="working" value={stats.working} tone={stats.working > 0 ? 'good' : undefined} />
          <Stat label="failing" value={stats.failing} tone={stats.failing > 0 ? 'bad' : undefined} />
          <Stat label="stale" value={stats.stale} tone={stats.stale > 0 ? 'warn' : undefined} />
          <Stat label="untested" value={stats.untested} tone="muted" />
        </div>
      )}

      {data && data.providers.length === 0 && (
        <div className="hint-card">
          No providers configured yet.
        </div>
      )}

      {data && data.providers.length > 0 && (
        <div className="provider-grid">
          {data.providers.map((p) => {
            const session = tests[p.id]
            const health = classifyHealth(p, session)
            const testing = session?.kind === 'pending'
            return (
              <article
                key={p.id}
                className={`provider-tile${p.enabled ? '' : ' provider-tile--disabled'}`}
              >
                <div className="provider-tile__eyebrow">
                  <span className="provider-tile__type">{p.type}</span>
                  <span className="provider-tile__id">#{p.id}</span>
                </div>

                <Link to={`/providers/${p.id}`} className="provider-tile__title">
                  {p.name}
                </Link>

                <div className={`provider-tile__health provider-tile__health--${health}`}>
                  <span className="provider-tile__dot" aria-hidden />
                  <span className="provider-tile__health-label">{HEALTH_LABEL[health]}</span>
                  <span className="provider-tile__health-meta">
                    {session?.kind === 'done' ? (
                      session.result.ok
                        ? `tested · ${session.result.fetchedCount} · ${session.result.durationMs}ms`
                        : `tested · ${truncate(session.result.error ?? 'failed', 32)}`
                    ) : p.lastFetchedAt ? (
                      `${formatRelative(p.lastFetchedAt)}${typeof p.lastFetchCount === 'number' ? ` · ${p.lastFetchCount}` : ''}`
                    ) : (
                      'never run'
                    )}
                  </span>
                </div>

                {p.requiresSecret && !p.hasSecret && (
                  <Link to={`/providers/${p.id}`} className="provider-tile__needs-key" aria-label={`${p.name} needs ${p.requiresSecret}`}>
                    needs {p.requiresSecret} →
                  </Link>
                )}

                <div className="provider-tile__actions">
                  <button
                    type="button"
                    className="btn btn--primary btn--sm"
                    onClick={() => test.mutate(p.id)}
                    disabled={testing || p.type === 'manual'}
                    title={p.type === 'manual' ? 'Manual providers have no live endpoint to test' : undefined}
                  >
                    {testing ? <span className="spinner" /> : 'Test'}
                  </button>
                </div>

                <label className="provider-tile__toggle">
                  <input
                    type="checkbox"
                    checked={p.enabled}
                    onChange={(e) => toggle.mutate({ p, enabled: e.target.checked })}
                    disabled={toggle.isPending}
                    aria-label={`${p.name} enabled`}
                  />
                  <span>{p.enabled ? 'enabled' : 'disabled'}</span>
                </label>
              </article>
            )
          })}
        </div>
      )}
    </div>
  )
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: 'good' | 'bad' | 'warn' | 'muted' }) {
  return (
    <span className={`provider-stats__item${tone ? ` provider-stats__item--${tone}` : ''}`}>
      <span className="provider-stats__value">{value}</span>
      <span className="provider-stats__label">{label}</span>
    </span>
  )
}

function nameById(list: ProviderSummary[] | undefined, id: number): string {
  return list?.find((p) => p.id === id)?.name ?? `#${id}`
}

function truncate(s: string, max: number): string {
  if (s.length <= max) return s
  return s.slice(0, max - 1) + '…'
}
