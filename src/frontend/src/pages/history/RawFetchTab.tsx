import { useEffect, useState } from 'react'
import type { RunDetail } from '../../api/types'
import { formatRelative, formatStepDuration } from '../../utils/time'
import { useT } from '../../i18n'

export function RawFetchTab({ data, focusProvider }: { data: RunDetail; focusProvider?: string }) {
  const t = useT('history')
  const [open, setOpen] = useState<Set<string>>(() =>
    new Set(focusProvider ? [focusProvider] : data.raw?.map(p => p.provider) ?? [])
  )

  useEffect(() => {
    if (!focusProvider) return
    const el = document.getElementById(`raw-${focusProvider}`)
    if (!el) return
    const t = window.setTimeout(
      () => el.scrollIntoView({ behavior: 'smooth', block: 'start' }),
      50,
    )
    return () => window.clearTimeout(t)
  }, [focusProvider])

  if (!data.raw) {
    return <div className="muted">{t.noRawRecorded}</div>
  }
  const durationByProvider = new Map(data.providers.map(p => [p.name, p.durationMs]))
  return (
    <section className="raw-fetch">
      {data.raw.map(group => {
        const durationMs = durationByProvider.get(group.provider)
        const isOpen = open.has(group.provider)
        return (
          <div
            key={group.provider}
            id={`raw-${group.provider}`}
            className={[
              'raw-group',
              isOpen ? 'raw-group--open' : '',
              focusProvider === group.provider ? 'raw-group--focus' : '',
            ].filter(Boolean).join(' ')}
          >
            <button
              type="button"
              className="raw-group__header"
              onClick={() => {
                const next = new Set(open)
                if (isOpen) next.delete(group.provider)
                else next.add(group.provider)
                setOpen(next)
              }}
            >
              <span className="raw-group__caret" aria-hidden="true">{isOpen ? '▾' : '▸'}</span>
              <span className="raw-group__name">{group.provider}</span>
              {durationMs != null && (
                <span className="raw-group__dur mono">{formatStepDuration(durationMs)}</span>
              )}
              <span className="raw-group__count">{group.listings.length}</span>
            </button>
            {isOpen && group.listings.length > 0 && (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>{t.colTitle}</th>
                      <th>{t.colCompany}</th>
                      <th>{t.colLocation}</th>
                      <th>{t.colPosted}</th>
                      <th>{t.colUrl}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {/* Position joins the id because this dump is deliberately un-deduped: a source
                        can return the same listing twice (html-jyskebank returns one four times),
                        and the id alone then collides. Nothing here reorders or holds per-row state. */}
                    {group.listings.map((l, i) => (
                      <tr key={`${l.id}-${i}`}>
                        <td>{l.title}</td>
                        <td>{l.company ?? <span className="muted">—</span>}</td>
                        <td>{l.location ?? <span className="muted">—</span>}</td>
                        <td className="tabular mono">
                          {l.postedAt ? formatRelative(l.postedAt) : <span className="muted">—</span>}
                        </td>
                        <td><a href={l.url} target="_blank" rel="noreferrer">{t.openListing}</a></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {isOpen && group.listings.length === 0 && (
              <div className="muted" style={{ padding: '0.5rem 1rem' }}>{t.zeroListings}</div>
            )}
          </div>
        )
      })}
    </section>
  )
}
