import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProviders, setProviderEnabled, testProvider } from '../api/client'
import type { ProviderSummary, ProviderTestResult } from '../api/types'
import { Toast } from '../components/Toast'
import { AddSourceModal } from '../components/AddSourceModal'
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
  working: 'OK',
  failing: 'failing',
  stale: 'stale',
  untested: 'not tested yet',
}

const FILTERS = [
  { key: 'all', label: 'All' },
  { key: 'on', label: 'On' },
  { key: 'off', label: 'Off' },
  { key: 'failing', label: 'Failing' },
] as const
type FilterKey = (typeof FILTERS)[number]['key']

export function ProvidersPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({ queryKey: ['providers'], queryFn: getProviders })
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const [tests, setTests] = useState<Record<number, SessionTest>>({})
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<FilterKey>('all')
  const [adding, setAdding] = useState(false)

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

  // Health depends on session test results as well as last-fetch metadata, so both the "failing"
  // count and the "failing" filter recompute when a Test finishes.
  const health = useMemo(() => {
    const m = new Map<number, Health>()
    for (const p of data?.providers ?? []) m.set(p.id, classifyHealth(p, tests[p.id]))
    return m
  }, [data, tests])

  const counts = useMemo(() => {
    const ps = data?.providers ?? []
    return {
      all: ps.length,
      on: ps.filter((p) => p.enabled).length,
      off: ps.filter((p) => !p.enabled).length,
      failing: ps.filter((p) => health.get(p.id) === 'failing').length,
    }
  }, [data, health])

  const filtered = useMemo(() => {
    const ps = data?.providers ?? []
    const q = query.trim().toLowerCase()
    return ps.filter((p) => {
      if (filter === 'on' && !p.enabled) return false
      if (filter === 'off' && p.enabled) return false
      if (filter === 'failing' && health.get(p.id) !== 'failing') return false
      if (q && !`${p.displayName} ${p.name} ${p.type}`.toLowerCase().includes(q)) return false
      return true
    })
  }, [data, query, filter, health])

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header page__header--with-action">
        <div>
          <div className="page__eyebrow">01 / sources</div>
          <h1 className="page__heading">Job <em>sites</em></h1>
          <p className="page__lede">
            Where listings come from. Turn each one on or off, test it, or add your own.
          </p>
        </div>
        <button type="button" className="btn btn--primary" onClick={() => setAdding(true)}>
          + Add a source
        </button>
      </header>

      {adding && (
        <AddSourceModal
          onClose={() => setAdding(false)}
          onCreated={(_id, name) => {
            setAdding(false)
            void queryClient.invalidateQueries({ queryKey: ['providers'] })
            setToast({ kind: 'ok', message: `Added ${name}.` })
          }}
        />
      )}

      {isLoading && <div className="muted">Loading sources…</div>}
      {error && <div className="error-text">Failed to load sources.</div>}

      {data && (
        <div className="provider-stats">
          <Stat label="total" value={counts.all} />
          <Stat label="on" value={counts.on} tone={counts.on > 0 ? 'good' : undefined} />
          <Stat label="off" value={counts.off} tone="muted" />
          {counts.failing > 0 && <Stat label="failing" value={counts.failing} tone="bad" />}
        </div>
      )}

      {data && data.providers.length > 0 && (
        <div className="provider-toolbar">
          <input
            type="search"
            className="input provider-toolbar__search"
            placeholder="Search sources…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="Search sources by name"
          />
          <div className="provider-toolbar__filters" role="group" aria-label="Filter sources">
            {FILTERS.map((f) => (
              <button
                key={f.key}
                type="button"
                className={filter === f.key ? 'chip chip--active' : 'chip'}
                onClick={() => setFilter(f.key)}
                aria-pressed={filter === f.key}
              >
                {f.label} <span className="provider-toolbar__count">{counts[f.key]}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {data && data.providers.length === 0 && (
        <div className="hint-card">
          <p>No job sites set up yet.</p>
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setAdding(true)}>
            + Add your first source
          </button>
        </div>
      )}

      {data && data.providers.length > 0 && filtered.length === 0 && (
        <div className="hint-card">
          No sources match{query.trim() ? ` “${query.trim()}”` : ''}
          {filter !== 'all' ? ` in “${filter}”` : ''}.
        </div>
      )}

      {filtered.length > 0 && (
        <div className="provider-grid">
          {filtered.map((p) => {
            const session = tests[p.id]
            const health = classifyHealth(p, session)
            const testing = session?.kind === 'pending'
            return (
              <article
                key={p.id}
                className={`provider-tile${p.enabled ? '' : ' provider-tile--disabled'}`}
              >
                <div className="provider-tile__eyebrow">
                  <span className="provider-tile__type">{friendlyType(p.type)}</span>
                  <span className="provider-tile__id">#{p.id}</span>
                </div>

                <Link to={`/providers/${p.id}`} className="provider-tile__title">
                  {p.displayName}
                </Link>

                <div className={`provider-tile__health provider-tile__health--${health}`}>
                  <span className="provider-tile__dot" aria-hidden />
                  <span className="provider-tile__health-label">{HEALTH_LABEL[health]}</span>
                  <span className="provider-tile__health-meta">
                    {session?.kind === 'done' ? (
                      session.result.ok
                        ? `tested · ${session.result.fetchedCount} jobs · ${session.result.durationMs}ms`
                        : `tested · ${truncate(session.result.error ?? 'failed', 32)}`
                    ) : p.lastFetchedAt ? (
                      `${formatRelative(p.lastFetchedAt)}${typeof p.lastFetchCount === 'number' ? ` · ${p.lastFetchCount} jobs` : ''}`
                    ) : (
                      'never used'
                    )}
                  </span>
                </div>

                {p.requiresSecret && !p.hasSecret && (
                  <Link to={`/providers/${p.id}`} className="provider-tile__needs-key" aria-label={`${p.displayName} needs ${friendlySecretLabel(p.requiresSecret)}`}>
                    needs {friendlySecretLabel(p.requiresSecret).toLowerCase()} →
                  </Link>
                )}

                <div className="provider-tile__actions">
                  <button
                    type="button"
                    className="btn btn--primary btn--sm"
                    onClick={() => test.mutate(p.id)}
                    disabled={testing || p.type === 'manual'}
                    title={p.type === 'manual' ? "Manual sources can't be tested automatically" : undefined}
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
                    aria-label={`Enable ${p.displayName}`}
                  />
                  <span className="provider-tile__switch" aria-hidden="true" />
                  <span className="provider-tile__toggle-label">{p.enabled ? 'On' : 'Off'}</span>
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
  return list?.find((p) => p.id === id)?.displayName ?? `#${id}`
}

function truncate(s: string, max: number): string {
  if (s.length <= max) return s
  return s.slice(0, max - 1) + '…'
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
