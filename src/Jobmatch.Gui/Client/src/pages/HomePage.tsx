import { useQuery } from '@tanstack/react-query'
import { getWhoami } from '../api/client'

export function HomePage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['whoami'],
    queryFn: getWhoami,
  })

  if (isLoading) {
    return (
      <div className="page-loading">
        <span className="spinner" aria-hidden="true" />
        <p>Loading…</p>
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="page-error">
        <p>Could not load user info.</p>
        {error && <p className="page-error__detail">{String(error)}</p>}
      </div>
    )
  }

  return (
    <div className="page">
      <div className="card">
        <h1 className="card__heading">{data.email}</h1>
        <dl className="card__meta">
          <dt>Data directory</dt>
          <dd className="mono">{data.dataDir}</dd>
          <dt>Tool version</dt>
          <dd>{data.toolVersion}</dd>
        </dl>
      </div>
    </div>
  )
}
