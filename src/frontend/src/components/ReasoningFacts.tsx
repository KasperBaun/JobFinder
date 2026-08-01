import type { ListingMatch } from '../api/types'
import { serverText, useT } from '../i18n'
import { list } from '../i18n/serverText'

// Runs recorded before LlmScore/LlmReason were structured carry the judge's verdict inside the
// English prose string — recover it so old runs get the AI row too.
const LEGACY_AI_REVIEW = /\bAI review: \d+[.,]\d+ — (.+)$/

// Note keys this component renders as labelled rows; anything else (a future backend key) falls
// back to the sentence catalog as a full-width row instead of being dropped.
const KNOWN_KEYS = new Set([
  'disqualified', 'primaryHits', 'noPrimaryHits', 'secondaryHits', 'domainHits',
  'seniorityClose', 'seniorityMatches', 'seniorityMismatch', 'seniorityUnknown',
  'titleNotDeveloper', 'location', 'remoteOk', 'neitherLocationNorRemote',
  'locationRemoteUnknown', 'locationMismatchRemoteUnknown', 'locationUnknownRemoteMismatch',
  'agePenalty',
])

interface Props {
  match: ListingMatch
}

// The card's rationale as a scannable label + value list. Every axis is always present —
// "not stated" is rendered muted rather than omitted, so the eye learns where things live.
export function ReasoningFacts({ match }: Props) {
  const t = useT('listing')
  const s = useT('server')
  const notes = match.reasoningNotes ?? []
  const byKey = new Map(notes.map(note => [note.key, note]))

  const disqualified = byKey.get('disqualified')
  if (disqualified) {
    return (
      <dl className="listing-card__facts">
        <Row label={t.facts.disqualified} value={list(disqualified.args ?? {}, 'hits').join(', ')} tone="warn" />
      </dl>
    )
  }

  const primary = byKey.get('primaryHits')
  const secondary = byKey.get('secondaryHits')
  const domains = byKey.get('domainHits')
  const age = byKey.get('agePenalty')

  const seniority = byKey.has('seniorityMatches')
    ? { value: t.facts.seniorityFits }
    : byKey.has('seniorityClose')
      ? { value: t.facts.seniorityClose }
      : byKey.has('seniorityMismatch')
        ? { value: t.facts.seniorityMismatch, tone: 'warn' as const }
        : { value: t.facts.notStated, tone: 'muted' as const }

  const locationMismatch = byKey.has('locationMismatchRemoteUnknown') || byKey.has('neitherLocationNorRemote')
  const location = match.location
    ? { value: locationMismatch ? `${match.location} — ${t.facts.outsideArea}` : match.location, tone: locationMismatch ? ('warn' as const) : undefined }
    : { value: t.facts.notStated, tone: 'muted' as const }

  const remote = match.remoteMode && match.remoteMode !== 'unknown'
    ? { value: s.remoteMode[match.remoteMode] ?? match.remoteMode }
    : { value: t.facts.notStated, tone: 'muted' as const }

  const aiReason = match.llmReason ?? LEGACY_AI_REVIEW.exec(match.reasoning)?.[1]
  const extras = notes.filter(note => !KNOWN_KEYS.has(note.key))

  return (
    <dl className="listing-card__facts">
      {primary
        ? <Row label={t.facts.mustHave} value={<Pills names={list(primary.args ?? {}, 'skills')} kind="primary" />} />
        : <Row label={t.facts.mustHave} value={t.facts.noPrimary} tone="warn" />}
      {secondary && <Row label={t.facts.niceToHave} value={<Pills names={list(secondary.args ?? {}, 'skills')} kind="secondary" />} />}
      {domains && <Row label={t.facts.industry} value={list(domains.args ?? {}, 'domains').join(', ')} />}
      <Row label={t.facts.seniority} value={seniority.value} tone={seniority.tone} />
      <Row label={t.facts.location} value={location.value} tone={location.tone} />
      <Row label={t.facts.remote} value={remote.value} tone={remote.tone} />
      {byKey.has('titleNotDeveloper') && <Row label={t.facts.titleGate} value={t.facts.titleNotDeveloper} tone="warn" />}
      {age && <Row label={t.facts.age} value={t.facts.agePenalty(typeof age.args?.days === 'number' ? age.args.days : 0)} tone="warn" />}
      {extras.map(note => {
        const text = serverText(s.reasoning, note.key, note.args, '')
        return text ? <dd key={note.key} className="listing-card__fact-extra">{text}</dd> : null
      })}
      {aiReason && <Row label={t.facts.ai} value={aiReason} tone="ai" />}
    </dl>
  )
}

function Row({ label, value, tone }: { label: string; value: React.ReactNode; tone?: 'muted' | 'warn' | 'ai' }) {
  return (
    <>
      <dt className="listing-card__fact-label">{label}</dt>
      <dd className={`listing-card__fact-value${tone ? ` listing-card__fact-value--${tone}` : ''}`}>{value}</dd>
    </>
  )
}

function Pills({ names, kind }: { names: string[]; kind: 'primary' | 'secondary' }) {
  return (
    <span className="listing-card__fact-pills">
      {names.map(name => (
        <span key={name} className={`pill pill--${kind}`}>{name}</span>
      ))}
    </span>
  )
}
