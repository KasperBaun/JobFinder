import { useQuery } from '@tanstack/react-query'
import { getProviders } from '../api/client'
import { formatRelative } from '../utils/time'

export function ProvidersPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['providers'],
    queryFn: getProviders,
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <h1 className="page__heading">Configured providers</h1>
        <p className="page__lede">Sources the system fetches listings from on each search.</p>
      </header>

      {isLoading && <div className="muted">Loading providers…</div>}
      {error && <div className="error-text">Failed to load providers.</div>}

      {data && (
        <>
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Type</th>
                  <th>Enabled</th>
                  <th>Last fetched</th>
                  <th>Count</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                {data.providers.length === 0 && (
                  <tr><td colSpan={6} className="muted center">No providers configured.</td></tr>
                )}
                {data.providers.map(p => (
                  <tr key={p.name}>
                    <td><strong>{p.name}</strong></td>
                    <td><span className="badge badge--muted">{p.type}</span></td>
                    <td>
                      {p.enabled
                        ? <span className="badge badge--enabled">enabled</span>
                        : <span className="badge badge--disabled">disabled</span>}
                    </td>
                    <td title={p.lastFetchedAt ?? ''}>{formatRelative(p.lastFetchedAt)}</td>
                    <td>{p.lastFetchCount ?? '—'}</td>
                    <td className="truncate" title={p.notes ?? ''}>{p.notes ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="footnote">
            To add or remove providers, edit your <code>portals.yml</code>. The system reads it on every search.
          </p>
        </>
      )}
    </div>
  )
}
