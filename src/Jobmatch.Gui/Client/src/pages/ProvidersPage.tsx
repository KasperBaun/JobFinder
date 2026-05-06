import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProviders, updateProviders } from '../api/client'
import type { ProviderSummary, ProviderType, ProviderUpsert } from '../api/types'
import { Toggle } from '../components/Toggle'
import { SaveBar } from '../components/SaveBar'
import { Toast } from '../components/Toast'
import { formatRelative } from '../utils/time'

type Row = ProviderUpsert & { lastFetchedAt?: string; lastFetchCount?: number }

const TYPE_OPTIONS: ProviderType[] = ['api', 'rss', 'html', 'manual']

function fromSummary(p: ProviderSummary): Row {
  return {
    name: p.name,
    type: p.type,
    enabled: p.enabled,
    endpoint: p.endpoint ?? '',
    rateLimitRps: p.rateLimitRps,
    notes: p.notes ?? '',
    lastFetchedAt: p.lastFetchedAt,
    lastFetchCount: p.lastFetchCount,
  }
}

function toUpsert(r: Row): ProviderUpsert {
  return {
    name: r.name.trim(),
    type: r.type,
    enabled: r.enabled,
    endpoint: r.endpoint?.trim() || undefined,
    rateLimitRps: r.rateLimitRps,
    notes: r.notes?.trim() || undefined,
  }
}

export function ProvidersPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({ queryKey: ['providers'], queryFn: getProviders })

  const [rows, setRows] = useState<Row[] | null>(null)
  const [original, setOriginal] = useState<Row[] | null>(null)
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)

  useEffect(() => {
    if (data && original === null) {
      const r = data.providers.map(fromSummary)
      setRows(r)
      setOriginal(r)
    }
  }, [data, original])

  const dirty = useMemo(() => {
    if (!rows || !original) return false
    return JSON.stringify(rows.map(toUpsert)) !== JSON.stringify(original.map(toUpsert))
  }, [rows, original])

  const save = useMutation({
    mutationFn: async () => {
      if (!rows) throw new Error('no row state')
      const res = await updateProviders({ providers: rows.map(toUpsert) })
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      return rows
    },
    onSuccess: (saved) => {
      setOriginal(saved)
      void queryClient.invalidateQueries({ queryKey: ['providers'] })
      setToast({ kind: 'ok', message: 'Providers saved' })
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function patchRow(idx: number, p: Partial<Row>) {
    setRows((rs) => rs ? rs.map((r, i) => i === idx ? { ...r, ...p } : r) : rs)
  }
  function removeRow(idx: number) {
    setRows((rs) => rs ? rs.filter((_, i) => i !== idx) : rs)
  }
  function addRow() {
    const blank: Row = { name: '', type: 'rss', enabled: true, endpoint: '', rateLimitRps: 1.0, notes: '' }
    setRows((rs) => rs ? [...rs, blank] : [blank])
  }
  function revert() {
    if (original) setRows(original)
  }

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header page__header--with-action">
        <div>
          <div className="page__eyebrow">01 / sources</div>
          <h1 className="page__heading">Job <em>portals</em></h1>
          <p className="page__lede">
            The system fetches listings from these sources on every search. Edit, toggle, or add new ones below.
          </p>
        </div>
        <div className="cta-row" style={{ marginTop: 0 }}>
          <button type="button" className="btn btn--secondary" onClick={addRow}>+ Add provider</button>
        </div>
      </header>

      {isLoading && <div className="muted">Loading providers…</div>}
      {error && <div className="error-text">Failed to load providers.</div>}

      {rows && (
        <>
          {rows.length === 0 && (
            <div className="hint-card">
              No providers configured yet. Click <strong>Add provider</strong> to add one.
            </div>
          )}

          <div className="providers-list">
            {rows.map((row, idx) => (
              <article key={idx} className={row.enabled ? 'provider-card' : 'provider-card provider-card--disabled'}>
                <div className="provider-card__head">
                  <div className="provider-card__title-block">
                    <input
                      className="input input--narrow provider-card__name-input"
                      value={row.name}
                      placeholder="provider name"
                      onChange={(e) => patchRow(idx, { name: e.target.value })}
                      style={{ fontFamily: 'var(--font-display)', fontSize: '1.1rem', fontWeight: 500, padding: '0.4rem 0.6rem' }}
                    />
                    <span className="badge">{row.type}</span>
                    {row.lastFetchedAt && (
                      <span className="provider-card__meta">
                        last fetched {formatRelative(row.lastFetchedAt)}
                        {typeof row.lastFetchCount === 'number' && ` · ${row.lastFetchCount}`}
                      </span>
                    )}
                  </div>
                  <div className="provider-card__actions">
                    <Toggle
                      checked={row.enabled}
                      onChange={(v) => patchRow(idx, { enabled: v })}
                      label={row.enabled ? 'Enabled' : 'Disabled'}
                      ariaLabel={`${row.name} enabled`}
                    />
                    <button type="button" className="btn btn--danger" onClick={() => removeRow(idx)}>
                      Remove
                    </button>
                  </div>
                </div>

                <div className="provider-card__body">
                  <div className="field">
                    <label className="field__label">Type</label>
                    <select
                      className="select"
                      value={row.type}
                      onChange={(e) => patchRow(idx, { type: e.target.value as ProviderType })}
                    >
                      {TYPE_OPTIONS.map(t => <option key={t} value={t}>{t}</option>)}
                    </select>
                  </div>
                  <div className="field">
                    <label className="field__label">Rate limit (rps)</label>
                    <input
                      type="number"
                      step={0.1}
                      min={0}
                      className="input input--mono tabular"
                      value={row.rateLimitRps}
                      onChange={(e) => patchRow(idx, { rateLimitRps: Number(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="field" style={{ gridColumn: '1 / -1' }}>
                    <label className="field__label">Endpoint</label>
                    <input
                      className="input input--mono"
                      value={row.endpoint ?? ''}
                      placeholder="https://…"
                      onChange={(e) => patchRow(idx, { endpoint: e.target.value })}
                    />
                  </div>
                  <div className="field provider-card__notes-row">
                    <label className="field__label">Notes</label>
                    <input
                      className="input"
                      value={row.notes ?? ''}
                      placeholder="optional"
                      onChange={(e) => patchRow(idx, { notes: e.target.value })}
                    />
                  </div>
                </div>
              </article>
            ))}
          </div>

          <p className="field__hint" style={{ marginTop: 'var(--space-5)' }}>
            Advanced fields (HTML selectors, query params, headers, response mapping) are preserved on save.
          </p>

          <SaveBar
            visible={!!dirty}
            message={dirty ? 'Unsaved changes to portals.yml' : ''}
            saving={save.isPending}
            onSave={() => save.mutate()}
            onRevert={revert}
          />
        </>
      )}
    </div>
  )
}
