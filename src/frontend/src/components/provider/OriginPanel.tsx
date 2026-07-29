import type { ProviderDetail } from '../../api/types'
import { platformHost, platformLabel } from '../../utils/platform'
import { useLocale, useT } from '../../i18n'
import type { Messages } from '../../i18n'

function friendlyType(type: string, t: Messages['providers']): string {
  return t.typeDetailed[type as keyof Messages['providers']['typeDetailed']] ?? type
}

// Read-only "where does this come from & how is it fetched" panel. The editable knobs live in
// ConfigForm; this panel is the at-a-glance truth about origin, query, and fetch ceiling.
export function OriginPanel({ data }: { data: ProviderDetail }) {
  const t = useT('providers')
  const { locale } = useLocale()
  const cfg = data.config
  const platform = platformLabel(data.endpoint)
  const host = platformHost(data.endpoint)

  const paginationSummary = !cfg.paginates
    ? t.singleFetch
    : cfg.hardCeiling != null
      ? t.upToPagesCeiling(cfg.maxPages!, cfg.pageSize!, cfg.hardCeiling)
      : t.upToPages(String(cfg.maxPages ?? '?'))

  // The catalog ships a Danish rendering of the shipped notes; sources the user added themselves
  // only ever have the English text the detector produced.
  const notes = (locale === 'da' ? data.notesDa : undefined) ?? data.notes

  return (
    <section className="card">
      <h2 className="card__title">{t.informationTitle}</h2>
      <dl className="provider-config-grid">
        {platform && (
          <Row label={t.platform}>
            <span className="provider-config-grid__platform">{platform}</span>
            {host && platform !== host && <span className="muted small"> · {host}</span>}
          </Row>
        )}
        <Row label={t.accessMethod}>{friendlyType(data.type, t)}</Row>
        {data.endpoint && (
          <Row label={t.endpoint} wide>
            <span className="mono small break-anywhere">{data.endpoint}</span>
          </Row>
        )}
        {cfg.searchQuery && (
          <Row label={t.searchQuery}><span className="mono">{cfg.searchQuery}</span></Row>
        )}
        {data.type !== 'manual' && <Row label={t.fetchStrategy}>{paginationSummary}</Row>}
        {data.type !== 'manual' && (
          <Row label={t.rateLimit}>{t.perSecond(cfg.rateLimitRps)}</Row>
        )}
        {data.type !== 'manual' && (
          <Row label={t.fullDescriptions}>{cfg.enrichBody ? t.fullDescriptionsOn : t.fullDescriptionsOff}</Row>
        )}
        {notes && <Row label={t.notesLabel} wide>{notes}</Row>}
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
