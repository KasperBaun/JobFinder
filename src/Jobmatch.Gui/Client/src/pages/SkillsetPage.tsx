import { useQuery } from '@tanstack/react-query'
import { getSkillset } from '../api/client'
import type { ReactNode } from 'react'

interface PillRowProps {
  items: string[]
  variant?: 'primary' | 'secondary' | 'warning'
  empty?: string
}

function PillRow({ items, variant, empty = '—' }: PillRowProps) {
  if (!items.length) return <span className="muted">{empty}</span>
  const cls = variant ? `pill pill--${variant}` : 'pill'
  return (
    <div className="pill-row">
      {items.map(item => <span key={item} className={cls}>{item}</span>)}
    </div>
  )
}

interface SectionProps {
  title: string
  children: ReactNode
}

function Section({ title, children }: SectionProps) {
  return (
    <section className="card">
      <h2 className="card__heading">{title}</h2>
      {children}
    </section>
  )
}

export function SkillsetPage() {
  const { data, isLoading, error } = useQuery({
    queryKey: ['skillset'],
    queryFn: getSkillset,
  })

  return (
    <div className="page page--wide">
      <header className="page__header">
        <h1 className="page__heading">Search criteria</h1>
        <p className="page__lede">The active skillset used to score listings.</p>
      </header>

      {isLoading && <div className="muted">Loading skillset…</div>}
      {error && <div className="error-text">Failed to load skillset.</div>}

      {data && (
        <div className="card-stack">
          <Section title="Identity">
            <dl className="definition-list">
              <dt>Name</dt><dd>{data.name || '—'}</dd>
              <dt>Location</dt><dd>{data.location || '—'}</dd>
              <dt>Experience</dt><dd>{data.experienceYears} years</dd>
              <dt>Languages</dt><dd><PillRow items={data.languages} /></dd>
            </dl>
          </Section>

          <Section title="Roles">
            <dl className="definition-list">
              <dt>Target roles</dt><dd><PillRow items={data.targetRoles} /></dd>
              <dt>Seniority</dt><dd>{data.seniority || '—'}</dd>
              <dt>Remote</dt><dd>{data.remotePreference || '—'}</dd>
              <dt>Employment</dt><dd><PillRow items={data.employmentTypes} /></dd>
            </dl>
          </Section>

          <Section title="Tech">
            <dl className="definition-list">
              <dt>Primary stack</dt><dd><PillRow items={data.primaryStack} variant="primary" /></dd>
              <dt>Secondary stack</dt><dd><PillRow items={data.secondaryStack} variant="secondary" /></dd>
            </dl>
          </Section>

          <Section title="Domains">
            <PillRow items={data.domains} />
          </Section>

          <Section title="Disqualifiers">
            <PillRow items={data.disqualifiers} variant="warning" empty="None set" />
          </Section>

          <p className="footnote">
            To edit, modify your <code>skillset.md</code>. The system reads it on every search.
          </p>
        </div>
      )}
    </div>
  )
}
