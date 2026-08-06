import { Link, useNavigate } from 'react-router-dom'
import type { ProviderSummary } from '../../api/types'
import { friendlySecretLabel } from '../../components/provider/SecretsCard'
import { formatRelative } from '../../utils/time'
import { useT } from '../../i18n'
import { classifyHealth, friendlyType, healthMeta, truncate, type SessionTest } from './health'

interface Props {
  provider: ProviderSummary
  session?: SessionTest
  onTest: (id: number) => void
  onToggle: (p: ProviderSummary, enabled: boolean) => void
  togglePending: boolean
}

export function ProviderTile({ provider: p, session, onTest, onToggle, togglePending }: Props) {
  const t = useT('providers')
  const navigate = useNavigate()
  const health = classifyHealth(p, session)
  const testing = session?.kind === 'pending'

  return (
    <article
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
        {/* Ellipsized by design (see .provider-tile__health-meta) — the title keeps the
            full text reachable, which matters more in Danish where the labels run longer. */}
        <span className="provider-tile__health-meta" title={healthMeta(p, session, t)}>
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
          onClick={() => onTest(p.id)}
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
          onChange={(e) => onToggle(p, e.target.checked)}
          disabled={togglePending}
          aria-label={t.enableAria(p.displayName)}
        />
        <span className="provider-tile__switch" aria-hidden="true" />
        <span className="provider-tile__toggle-label">{p.enabled ? t.on : t.off}</span>
      </label>
    </article>
  )
}
