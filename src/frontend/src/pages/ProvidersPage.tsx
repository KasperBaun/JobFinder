import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProviders, setProviderEnabled, testProvider } from '../api/client'
import type { ProviderSummary } from '../api/types'
import { Toast } from '../components/Toast'
import { AddSourceModal } from '../components/AddSourceModal'
import { useT } from '../i18n'
import { classifyHealth, nameById, type Health, type SessionTest } from './providers/health'
import type { FilterKey } from './providers/filters'
import { ProviderTile } from './providers/ProviderTile'
import { ProviderToolbar } from './providers/ProviderToolbar'

export function ProvidersPage() {
  const t = useT('providers')
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
        <ProviderToolbar
          query={query}
          onQueryChange={setQuery}
          filter={filter}
          onFilterChange={setFilter}
          counts={counts}
        />
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
          {filtered.map((p) => (
            <ProviderTile
              key={p.id}
              provider={p}
              session={tests[p.id]}
              onTest={(id) => test.mutate(id)}
              onToggle={(provider, enabled) => toggle.mutate({ p: provider, enabled })}
              togglePending={toggle.isPending}
            />
          ))}
        </div>
      )}
    </div>
  )
}
