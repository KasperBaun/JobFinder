import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getSkillset } from '../api/client'
import type { ListingMatch } from '../api/types'
import { dec, useT } from '../i18n'
import { list } from '../i18n/serverText'
import { formatRelative } from '../utils/time'

// Runs recorded before LlmScore/LlmReason were structured carry the judge's verdict inside the
// English prose string — recover it so old runs get the AI strip too.
const LEGACY_AI_REVIEW = /\bAI review: (\d+[.,]\d+) — (.+)$/

type Tone = 'muted' | 'warn' | 'good'
type Fact = { id: string; label: string; value: React.ReactNode; tone?: Tone; title?: string }

interface Props {
  match: ListingMatch
}

// The card's rationale as a scannable label + value list in a fixed order, so the eye can
// column-scan a whole shortlist. Axes the posting doesn't state are folded into one muted row
// when there are several — named, but not paying a full row each.
export function ReasoningFacts({ match }: Props) {
  const t = useT('listing')
  const s = useT('server')
  const skillset = useQuery({ queryKey: ['skillset'], queryFn: getSkillset, staleTime: 5 * 60_000 })
  const notes = match.reasoningNotes ?? []
  const byKey = new Map(notes.map(note => [note.key, note]))

  const disqualified = byKey.get('disqualified')
  if (disqualified) {
    return (
      <dl className="listing-card__facts">
        <Row fact={{ id: 'disq', label: t.facts.disqualified, value: list(disqualified.args ?? {}, 'hits').join(', '), tone: 'warn' }} />
      </dl>
    )
  }

  const facts: Fact[] = []
  const notStated: Fact[] = []
  const add = (fact: Fact) => (fact.tone === 'muted' ? notStated : facts).push(fact)

  const primary = byKey.get('primaryHits')
  const primaryHits = primary ? list(primary.args ?? {}, 'skills') : []
  const ghosts = (skillset.data?.primaryStack ?? [])
    .filter(skill => !primaryHits.some(hit => hit.localeCompare(skill, undefined, { sensitivity: 'base' }) === 0))
  facts.push({
    id: 'mustHave',
    label: t.facts.mustHave,
    value: primary
      ? <Pills hits={primaryHits} ghosts={ghosts} kind="primary" ghostTitle={t.facts.ghostTooltip} />
      : <span>{t.facts.noPrimary}{ghosts.length > 0 && <> <Pills hits={[]} ghosts={ghosts} kind="primary" ghostTitle={t.facts.ghostTooltip} /></>}</span>,
    tone: primary ? undefined : 'warn',
  })

  const secondary = byKey.get('secondaryHits')
  if (secondary) {
    facts.push({ id: 'niceToHave', label: t.facts.niceToHave, value: <Pills hits={list(secondary.args ?? {}, 'skills')} ghosts={[]} kind="secondary" ghostTitle="" /> })
  }
  const domains = byKey.get('domainHits')
  if (domains) {
    facts.push({ id: 'industry', label: t.facts.industry, value: list(domains.args ?? {}, 'domains').join(', ') })
  }

  // The level is inferred from the ad's title/description and compared to the profile —
  // "close" means one step away (junior↔mid↔senior↔lead). The tooltip spells that out.
  add(byKey.has('seniorityMatches')
    ? { id: 'seniority', label: t.facts.seniority, value: `✓ ${t.facts.seniorityFits}`, tone: 'good', title: t.facts.seniorityFitsTitle }
    : byKey.has('seniorityClose')
      ? { id: 'seniority', label: t.facts.seniority, value: `≈ ${t.facts.seniorityClose}`, tone: 'good', title: t.facts.seniorityCloseTitle }
      : byKey.has('seniorityMismatch')
        ? { id: 'seniority', label: t.facts.seniority, value: t.facts.seniorityMismatch, tone: 'warn', title: t.facts.seniorityMismatchTitle }
        : { id: 'seniority', label: t.facts.seniority, value: t.facts.notStated, tone: 'muted', title: t.facts.seniorityUnknownTitle })

  const locationMismatch = byKey.has('locationMismatchRemoteUnknown') || byKey.has('neitherLocationNorRemote')
  add(match.location
    ? locationMismatch
      ? { id: 'location', label: t.facts.location, value: `${match.location} — ${t.facts.outsideArea}`, tone: 'warn' }
      : { id: 'location', label: t.facts.location, value: byKey.has('location') ? `✓ ${match.location}` : match.location, tone: byKey.has('location') ? 'good' : undefined }
    : { id: 'location', label: t.facts.location, value: t.facts.notStated, tone: 'muted' })

  add(match.remoteMode && match.remoteMode !== 'unknown'
    ? {
        id: 'remote',
        label: t.facts.remote,
        value: byKey.has('remoteOk') ? `✓ ${s.remoteMode[match.remoteMode] ?? match.remoteMode}` : s.remoteMode[match.remoteMode] ?? match.remoteMode,
        tone: byKey.has('remoteOk') ? 'good' : undefined,
      }
    : { id: 'remote', label: t.facts.remote, value: t.facts.notStated, tone: 'muted' })

  const agePenalty = byKey.get('agePenalty')
  add(match.postedAt
    ? agePenalty
      ? { id: 'posted', label: t.facts.posted, value: `${formatRelative(match.postedAt)} — ${t.facts.ratingReduced}`, tone: 'warn' }
      : { id: 'posted', label: t.facts.posted, value: formatRelative(match.postedAt) }
    : { id: 'posted', label: t.facts.posted, value: t.facts.notStated, tone: 'muted' })

  if (byKey.has('titleNotDeveloper')) {
    facts.push({ id: 'titleGate', label: t.facts.titleGate, value: t.facts.titleNotDeveloper, tone: 'warn' })
  }

  return (
    <dl className="listing-card__facts">
      {facts.map(fact => <Row key={fact.id} fact={fact} />)}
      {notStated.length === 1 && <Row fact={notStated[0]} />}
      {notStated.length > 1 && (
        <Row fact={{ id: 'fold', label: t.facts.notStated, value: notStated.map(f => f.label).join(' · '), tone: 'muted' }} />
      )}
    </dl>
  )
}

// The judge's verdict as its own strip under the header — the one sentence-shaped judgement on
// the card. English by design (local model). Cards the judge never reached say so explicitly,
// so a missing strip reads as "unjudged", not "bad". Click toggles the 2-line clamp.
export function AiVerdict({ match }: Props) {
  const t = useT('listing')
  const [expanded, setExpanded] = useState(false)
  const legacy = LEGACY_AI_REVIEW.exec(match.reasoning)
  const reason = match.llmReason ?? legacy?.[2]
  const score = match.llmScore ?? (legacy ? Number(legacy[1].replace(',', '.')) : undefined)
  if (!reason) {
    return <p className="listing-card__ai listing-card__ai--none">{t.facts.aiNotReviewed}</p>
  }
  return (
    <button
      type="button"
      className={`listing-card__ai${expanded ? ' listing-card__ai--expanded' : ''}`}
      onClick={() => setExpanded(v => !v)}
      title={t.facts.aiExpand}
    >
      <span className="listing-card__ai-tag">✦ {t.facts.ai}{typeof score === 'number' && !Number.isNaN(score) ? ` ${dec(score, 2)}` : ''}</span>
      {' '}{reason}
    </button>
  )
}

function Row({ fact }: { fact: Fact }) {
  return (
    <>
      <dt className="listing-card__fact-label">{fact.label}</dt>
      <dd
        className={`listing-card__fact-value${fact.tone ? ` listing-card__fact-value--${fact.tone}` : ''}${fact.title ? ' listing-card__fact-value--hinted' : ''}`}
        title={fact.title}
      >
        {fact.value}
      </dd>
    </>
  )
}

function Pills({ hits, ghosts, kind, ghostTitle }: { hits: string[]; ghosts: string[]; kind: 'primary' | 'secondary'; ghostTitle: string }) {
  return (
    <span className="listing-card__fact-pills">
      {hits.map(name => (
        <span key={name} className={`pill pill--${kind}`}>{name}</span>
      ))}
      {ghosts.map(name => (
        <span key={`g-${name}`} className="pill pill--ghost" title={ghostTitle}>{name}</span>
      ))}
    </span>
  )
}
