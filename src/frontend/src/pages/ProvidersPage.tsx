import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProviders, setProviderEnabled, testProvider } from '../api/client'
import type { ProviderSummary, ProviderTestResult } from '../api/types'
import { Toast } from '../components/Toast'
import { AddSourceModal } from '../components/AddSourceModal'
import { formatRelative } from '../utils/time'
import { useT } from '../i18n'
import type { Messages } from '../i18n'
import { friendlySecretLabel } from '../components/provider/SecretsCard'

type Health = 'working' | 'failing' | 'stale' | 'untested' | 'blocked'

type SessionTest = { kind: 'pending' } | { kind: 'done'; result: ProviderTestResult }

const STALE_DAYS = 14

// A source needs a key it doesn't have. Search skips it (see ProviderStateMerger), so it's "On but
// won't run" — flag it here instead of letting it read as OK/stale.
function isBlocked(p: ProviderSummary): boolean {
  return p.enabled && !!p.requiresSecret && !p.hasSecret
}

function classifyHealth(p: ProviderSummary, sessionTest?: SessionTest): Health {
  if (sessionTest?.kind === 'done') {
    return sessionTest.result.ok ? 'working' : 'failing'
  }
  if (isBlocked(p)) return 'blocked'
  if (!p.lastFetchedAt) return 'untested'
  const ageMs = Date.now() - new Date(p.lastFetchedAt).getTime()
  const stale = ageMs > STALE_DAYS * 24 * 60 * 60 * 1000
  if (stale) return 'stale'
  return (p.lastFetchCount ?? 0) > 0 ? 'working' : 'failing'
}

const FILTER_KEYS = ['all', 'on', 'off', 'failing'] as const
type FilterKey = (typeof FILTER_KEYS)[number]

// The filter chips double as the source summary, so their counts carry the same tone the old
// stats card used: enabled = good, failing = bad, off = muted. Zero failing stays neutral so an
// empty "Failing 0" doesn't read as an alert.
function countTone(key: FilterKey, counts: Record<FilterKey, number>): 'good' | 'bad' | 'muted' | undefined {
  if (key === 'on') return counts.on > 0 ? 'good' : undefined
  if (key === 'failing') return counts.failing > 0 ? 'bad' : undefined
  if (key === 'off') return 'muted'
  return undefined
}

export function ProvidersPage() {
  const t = useT('providers')
  const navigate = useNavigate()
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
      if (!res.success) throw new Error(res.error ?? t.saveFailed)
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
          ? t.testResultOk(nameById(data?.providers, id), result.fetchedCount, result.durationMs)
          : t.testResultFail(nameById(data?.providers, id), result.error ?? t.failedShort),
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
          <div className="page__eyebrow">{t.eyebrow}</div>
          <h1 className="page__heading">{t.heading()}</h1>
          <p className="page__lede">{t.lede}</p>
        </div>
        <button type="button" className="btn btn--primary" onClick={() => setAdding(true)}>
          {t.addSource}
        </button>
      </header>

      {adding && (
        <AddSourceModal
          onClose={() => setAdding(false)}
          onCreated={(_id, name) => {
            setAdding(false)
            void queryClient.invalidateQueries({ queryKey: ['providers'] })
            setToast({ kind: 'ok', message: t.added(name) })
          }}
        />
      )}

      {isLoading && <div className="muted">{t.loading}</div>}
      {error && <div className="error-text">{t.loadFailed}</div>}

      {data && data.providers.length > 0 && (
        <div className="provider-toolbar">
          <input
            type="search"
            className="input provider-toolbar__search"
            placeholder={t.searchPlaceholder}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label={t.searchAria}
          />
          <div className="provider-toolbar__filters" role="group" aria-label={t.filterAria}>
            {FILTER_KEYS.map((key) => {
              const tone = countTone(key, counts)
              return (
                <button
                  key={key}
                  type="button"
                  className={filter === key ? 'chip chip--active' : 'chip'}
                  onClick={() => setFilter(key)}
                  aria-pressed={filter === key}
                >
                  {t.filter[key]}{' '}
                  <span
                    className={`provider-toolbar__count${tone ? ` provider-toolbar__count--${tone}` : ''}`}
                  >
                    {counts[key]}
                  </span>
                </button>
              )
            })}
          </div>
        </div>
      )}

      {data && data.providers.length === 0 && (
        <div className="hint-card">
          <p>{t.noneYet}</p>
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setAdding(true)}>
            {t.addFirstSource}
          </button>
        </div>
      )}

      {data && data.providers.length > 0 && filtered.length === 0 && (
        <div className="hint-card">
          {t.noMatches(query.trim(), filter === 'all' ? '' : t.filter[filter])}
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
                className={`provider-tile provider-tile--clickable${p.enabled ? '' : ' provider-tile--disabled'}`}
                data-tooltip={t.tileTooltip}
                onClick={(e) => {
                  // The whole card is a shortcut to the detail page — but not when the click lands on
                  // an interactive control (Test button, the on/off toggle, or a link that navigates itself).
                  if ((e.target as HTMLElement).closest('button, label, a')) return
                  navigate(`/providers/${p.id}`)
                }}
              >
                <div className="provider-tile__eyebrow">
                  <span className="provider-tile__type">{friendlyType(p.type, t)}</span>
                  <span className="provider-tile__id">#{p.id}</span>
                </div>

                <Link to={`/providers/${p.id}`} className="provider-tile__title">
                  {p.displayName}
                </Link>

                <div className={`provider-tile__health provider-tile__health--${health}`}>
                  <span className="provider-tile__dot" aria-hidden />
                  <span className="provider-tile__health-label">{t.health[health]}</span>
                  <span className="provider-tile__health-meta">
                    {session?.kind === 'done' ? (
                      session.result.ok
                        ? t.testedOk(session.result.fetchedCount, session.result.durationMs)
                        : t.testedFail(truncate(session.result.error ?? t.failedShort, 32))
                    ) : health === 'blocked' ? (
                      t.blockedMeta
                    ) : p.lastFetchedAt ? (
                      t.fetchedMeta(formatRelative(p.lastFetchedAt), p.lastFetchCount)
                    ) : (
                      t.neverUsed
                    )}
                  </span>
                </div>

                {p.requiresSecret && !p.hasSecret && (
                  <Link to={`/providers/${p.id}`} className="provider-tile__needs-key" aria-label={t.addKeyAria(friendlySecretLabel(p.requiresSecret, t), p.displayName)}>
                    {t.addKey(friendlySecretLabel(p.requiresSecret, t))}
                  </Link>
                )}

                <div className="provider-tile__actions">
                  <button
                    type="button"
                    className="btn btn--primary btn--sm"
                    onClick={() => test.mutate(p.id)}
                    disabled={testing || p.type === 'manual'}
                    title={p.type === 'manual' ? t.manualCantTest : undefined}
                  >
                    {testing ? <span className="spinner" /> : t.test}
                  </button>
                </div>

                <label className="provider-tile__toggle">
                  <input
                    type="checkbox"
                    checked={p.enabled}
                    onChange={(e) => toggle.mutate({ p, enabled: e.target.checked })}
                    disabled={toggle.isPending}
                    aria-label={t.enableAria(p.displayName)}
                  />
                  <span className="provider-tile__switch" aria-hidden="true" />
                  <span className="provider-tile__toggle-label">{p.enabled ? t.on : t.off}</span>
                </label>
              </article>
            )
          })}
        </div>
      )}
    </div>
  )
}

function nameById(list: ProviderSummary[] | undefined, id: number): string {
  return list?.find((p) => p.id === id)?.displayName ?? `#${id}`
}

function truncate(s: string, max: number): string {
  if (s.length <= max) return s
  return s.slice(0, max - 1) + '…'
}

function friendlyType(type: string, t: Messages['providers']): string {
  return t.type[type as keyof Messages['providers']['type']] ?? type
}
