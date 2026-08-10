import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getApplications } from '../api/client'
import { StatusSelect } from '../components/StatusSelect'
import { SortableHeader } from '../components/longlist/SortableHeader'
import { formatAbsolute, formatRelative } from '../utils/time'
import type { ApplicationEntry, ApplicationStatus } from '../api/types'
import { dec, n, useT } from '../i18n'

// Render-time threshold only — no scheduler or notification behind it (R-107).
export const AWAITING_RESPONSE_DAYS = 14
const DAY_MS = 86_400_000

const STATUS_ORDER: ApplicationStatus[] = ['offer', 'interview', 'applied', 'no-response', 'rejected']

type StatusSort = 'none' | 'desc' | 'asc'

// Cross-run tracker: every listing that carries an application status, with the
// newest run's status when the same job was statused in several runs (R-097).
export function ApplicationsPage() {
  const t = useT('applications')
  const { data, isLoading, error } = useQuery({
    queryKey: ['applications'],
    queryFn: getApplications,
  })
  const [statusFilter, setStatusFilter] = useState<ApplicationStatus | null>(null)
  const [sort, setSort] = useState<StatusSort>('none')

  const apps = useMemo(() => data?.applications ?? [], [data])
  const counts = useMemo(() => countByStatus(apps), [apps])
  const visible = useMemo(() => {
    const rows = statusFilter ? apps.filter((a) => a.status === statusFilter) : apps
    return sort === 'none' ? rows : sortByStatusSet(rows, sort)
  }, [apps, statusFilter, sort])

  const cycleSort = () => setSort(sort === 'none' ? 'desc' : sort === 'desc' ? 'asc' : 'none')

  return (
    <div className="page page--wide">
      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{t.heading()}</h1>
        <p className="page__lede">{t.lede}</p>
      </header>

      {isLoading && <div className="muted">{t.loading}</div>}
      {error && <div className="error-text">{t.loadFailed}</div>}

      {data && apps.length === 0 && (
        <div className="hint-card">
          {t.emptyPrefix} <em>{t.emptyStatus}</em> {t.emptyMiddle}{' '}
          <Link to="/history">{t.emptyLink}</Link> {t.emptySuffix}
        </div>
      )}

      {apps.length > 0 && (
        <StatusTiles
          counts={counts}
          total={apps.length}
          active={statusFilter}
          onSelect={setStatusFilter}
        />
      )}

      {apps.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t.colTitle}</th>
                <th>{t.colCompany}</th>
                <th>{t.colSource}</th>
                <th>{t.colRating}</th>
                <th>{t.colStatus}</th>
                <SortableHeader dir={sort === 'none' ? undefined : sort} onActivate={cycleSort}>
                  {t.colStatusSet}
                </SortableHeader>
                <th>{t.colYourRating}</th>
                <th>{t.colFromSearch}</th>
              </tr>
            </thead>
            <tbody>
              {visible.map(a => <ApplicationRow key={a.listingId} entry={a} />)}
            </tbody>
          </table>
          {visible.length === 0 && (
            <div className="muted apps__no-match">
              {t.filterNoMatch}{' '}
              <button type="button" className="link-button" onClick={() => setStatusFilter(null)}>
                {t.filterReset}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// Per-status totals doubling as the status filter — click a tile to narrow, click again to clear.
function StatusTiles({ counts, total, active, onSelect }: {
  counts: Map<ApplicationStatus, number>
  total: number
  active: ApplicationStatus | null
  onSelect: (status: ApplicationStatus | null) => void
}) {
  const t = useT('applications')
  const tl = useT('listing')
  return (
    <div className="app-stat-tiles" role="group" aria-label={t.tilesLabel}>
      <button
        type="button"
        className={`app-stat-tile ${active === null ? 'app-stat-tile--active' : ''}`}
        aria-pressed={active === null}
        onClick={() => onSelect(null)}
      >
        <span className="app-stat-tile__count">{n(total)}</span> {t.filterAll}
      </button>
      {STATUS_ORDER.filter((s) => (counts.get(s) ?? 0) > 0).map((s) => (
        <button
          key={s}
          type="button"
          className={`app-stat-tile ${active === s ? 'app-stat-tile--active' : ''}`}
          aria-pressed={active === s}
          onClick={() => onSelect(active === s ? null : s)}
        >
          <span className="app-stat-tile__count">{n(counts.get(s) ?? 0)}</span> {tl.status[s]}
        </button>
      ))}
    </div>
  )
}

function ApplicationRow({ entry }: { entry: ApplicationEntry }) {
  const t = useT('listing')
  const ta = useT('applications')
  const waiting = daysAwaitingResponse(entry)
  return (
    <tr>
      <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
      <td>{entry.company ?? <span className="muted">—</span>}</td>
      <td><span className="badge badge--muted">{entry.portalDisplayName ?? entry.portal}</span></td>
      <td className="tabular mono">{dec(entry.score, 2)}</td>
      <td>
        <StatusSelect runId={entry.runId} listingId={entry.listingId} current={entry.status} compact />
      </td>
      <td className="apps__status-set">
        {entry.statusChangedAt
          ? (
            <span title={formatAbsolute(entry.statusChangedAt)}>
              {formatRelative(entry.statusChangedAt)}
            </span>
          )
          : <span className="muted">—</span>}
        {waiting !== null && (
          <span className="badge badge--waiting" title={ta.waitingTitle}>
            {ta.waiting(waiting)}
          </span>
        )}
      </td>
      <td>
        {entry.mark
          ? (
            <span
              className={`badge ${entry.mark === 'good' ? 'badge--score' : 'badge--muted'}`}
              title={entry.reason ? `“${entry.reason}”` : undefined}
            >
              {entry.mark === 'good' ? t.markGood : t.markBad}
            </span>
          )
          : <span className="muted">—</span>}
      </td>
      <td title={formatAbsolute(entry.runStartedAt)}>
        <Link to={`/history/${entry.runId}`}>{formatRelative(entry.runStartedAt)}</Link>
      </td>
    </tr>
  )
}

function countByStatus(apps: readonly ApplicationEntry[]): Map<ApplicationStatus, number> {
  const m = new Map<ApplicationStatus, number>()
  for (const a of apps) m.set(a.status, (m.get(a.status) ?? 0) + 1)
  return m
}

// Entries without a timestamp (statused before R-107) sort last in either direction.
function sortByStatusSet(rows: readonly ApplicationEntry[], dir: 'asc' | 'desc'): ApplicationEntry[] {
  const sign = dir === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    const ta = a.statusChangedAt ? Date.parse(a.statusChangedAt) : null
    const tb = b.statusChangedAt ? Date.parse(b.statusChangedAt) : null
    if (ta === null && tb === null) return 0
    if (ta === null) return 1
    if (tb === null) return -1
    return sign * (ta - tb)
  })
}

function daysAwaitingResponse(entry: ApplicationEntry): number | null {
  if (entry.status !== 'applied' || !entry.statusChangedAt) return null
  const elapsed = Date.now() - Date.parse(entry.statusChangedAt)
  if (!Number.isFinite(elapsed) || elapsed <= AWAITING_RESPONSE_DAYS * DAY_MS) return null
  return Math.floor(elapsed / DAY_MS)
}
