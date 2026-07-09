import type { ProviderDetail } from '../../api/types'
import { platformHost, platformLabel } from '../../utils/platform'

function friendlyType(type: string): string {
  switch (type) {
    case 'api':        return 'Auto-fetched (API)'
    case 'rss':        return 'News feed (RSS)'
    case 'html':       return 'Read from website'
    case 'teamtailor': return 'Auto-fetched (Teamtailor)'
    case 'hrmanager':  return 'Auto-fetched (HR Manager)'
    case 'manual':     return 'Manual import'
    default:           return type
  }
}

// Read-only "where does this come from & how is it fetched" panel. The editable knobs live in
// ConfigForm; this panel is the at-a-glance truth about origin, query, and fetch ceiling.
export function OriginPanel({ data }: { data: ProviderDetail }) {
  const cfg = data.config
  const platform = platformLabel(data.endpoint)
  const host = platformHost(data.endpoint)

  const paginationSummary = !cfg.paginates
    ? 'Single fetch — returns everything the endpoint gives'
    : cfg.hardCeiling != null
      ? `Up to ${cfg.maxPages} pages × ${cfg.pageSize} = ${cfg.hardCeiling} listings max`
      : `Up to ${cfg.maxPages ?? '?'} pages`

  return (
    <section className="card">
      <h2 className="card__title">Information</h2>
      <dl className="provider-config-grid">
        {platform && (
          <Row label="Platform">
            <span className="provider-config-grid__platform">{platform}</span>
            {host && platform !== host && <span className="muted small"> · {host}</span>}
          </Row>
        )}
        <Row label="Access method">{friendlyType(data.type)}</Row>
        {data.endpoint && (
          <Row label="Endpoint" wide>
            <span className="mono small break-anywhere">{data.endpoint}</span>
          </Row>
        )}
        {cfg.searchQuery && (
          <Row label="Search query"><span className="mono">{cfg.searchQuery}</span></Row>
        )}
        {data.type !== 'manual' && <Row label="Fetch strategy">{paginationSummary}</Row>}
        {data.type !== 'manual' && (
          <Row label="Rate limit">{cfg.rateLimitRps}/s</Row>
        )}
        {data.type !== 'manual' && (
          <Row label="Full descriptions">{cfg.enrichBody ? 'On — fetches each listing’s page' : 'Off — list data only'}</Row>
        )}
        {data.notes && <Row label="Notes" wide>{data.notes}</Row>}
      </dl>
    </section>
  )
}

function Row({ label, children, wide = false }: { label: string; children: React.ReactNode; wide?: boolean }) {
  return (
    <div className={wide ? 'provider-config-grid__row provider-config-grid__row--wide' : 'provider-config-grid__row'}>
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}
