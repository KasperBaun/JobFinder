import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getSkillset, updateSkillset } from '../api/client'
import type { SkillsetResponse, SkillsetUpdateRequest } from '../api/types'
import { TagInput } from '../components/TagInput'
import { SaveBar } from '../components/SaveBar'
import { Toast } from '../components/Toast'

type Form = SkillsetUpdateRequest

const REMOTE_OPTIONS = ['onsite', 'hybrid', 'remote', 'any'] as const
const SENIORITY_OPTIONS = ['junior', 'mid', 'senior', 'lead', 'any'] as const

function toForm(s: SkillsetResponse): Form {
  return {
    name: s.name,
    location: s.location,
    experienceYears: s.experienceYears,
    targetRoles: [...s.targetRoles],
    remotePreference: s.remotePreference,
    seniority: s.seniority,
    primaryStack: [...s.primaryStack],
    secondaryStack: [...s.secondaryStack],
    domains: [...s.domains],
    disqualifiers: [...s.disqualifiers],
    languages: [...s.languages],
    employmentTypes: [...s.employmentTypes],
    country: s.country ?? '',
    region: s.region ?? '',
    metro: [...s.metro],
  }
}

function isDirty(a: Form, b: Form): boolean {
  return JSON.stringify(a) !== JSON.stringify(b)
}

export function SkillsetPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({ queryKey: ['skillset'], queryFn: getSkillset })

  const [form, setForm] = useState<Form | null>(null)
  const [original, setOriginal] = useState<Form | null>(null)
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)

  useEffect(() => {
    if (data && original === null) {
      const f = toForm(data)
      setForm(f)
      setOriginal(f)
    }
  }, [data, original])

  const dirty = useMemo(
    () => form && original ? isDirty(form, original) : false,
    [form, original],
  )

  const save = useMutation({
    mutationFn: async () => {
      if (!form) throw new Error('no form state')
      const res = await updateSkillset(form)
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      return form
    },
    onSuccess: (saved) => {
      setOriginal(saved)
      void queryClient.invalidateQueries({ queryKey: ['skillset'] })
      setToast({ kind: 'ok', message: 'Profile saved' })
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function patch(p: Partial<Form>) {
    setForm((f) => f ? { ...f, ...p } : f)
  }

  function revert() {
    if (original) setForm(original)
  }

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">02 / profile</div>
        <h1 className="page__heading">Your <em>profile</em></h1>
        <p className="page__lede">
          Edit what jobfinder uses to rate every listing. Saved automatically.
        </p>
      </header>

      {isLoading && <div className="muted">Loading profile…</div>}
      {error && <div className="error-text">Failed to load profile.</div>}

      {form && (
        <>
          <div className="card-stack">
            <section className="card">
              <h2 className="card__title">About you</h2>
              <div className="field-grid">
                <div className="field">
                  <label className="field__label" htmlFor="sk-name">Name</label>
                  <input id="sk-name" className="input" value={form.name}
                    onChange={(e) => patch({ name: e.target.value })} />
                </div>
                <div className="field">
                  <label className="field__label" htmlFor="sk-location">Location</label>
                  <input id="sk-location" className="input" value={form.location}
                    onChange={(e) => patch({ location: e.target.value })} />
                </div>
                <div className="field">
                  <label className="field__label" htmlFor="sk-country">Country</label>
                  <input id="sk-country" className="input" value={form.country ?? ''} placeholder="optional"
                    onChange={(e) => patch({ country: e.target.value })} />
                </div>
                <div className="field">
                  <label className="field__label" htmlFor="sk-region">Region</label>
                  <input id="sk-region" className="input" value={form.region ?? ''} placeholder="optional"
                    onChange={(e) => patch({ region: e.target.value })} />
                </div>
                <div className="field">
                  <label className="field__label" htmlFor="sk-exp">Years of experience</label>
                  <input id="sk-exp" type="number" min={0} className="input input--narrow input--mono tabular"
                    value={form.experienceYears}
                    onChange={(e) => patch({ experienceYears: Number(e.target.value) || 0 })} />
                </div>
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Languages</label>
                  <TagInput values={form.languages}
                    onChange={(v) => patch({ languages: v })}
                    placeholder="e.g. en, da" ariaLabel="Languages" />
                </div>
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Cities / areas</label>
                  <TagInput values={form.metro}
                    onChange={(v) => patch({ metro: v })}
                    placeholder="optional — e.g. Copenhagen, Aarhus" ariaLabel="Cities or areas" />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">Roles &amp; preferences</h2>
              <div className="field-grid">
                <div className="field">
                  <label className="field__label" htmlFor="sk-seniority">Experience level</label>
                  <select id="sk-seniority" className="select"
                    value={form.seniority}
                    onChange={(e) => patch({ seniority: e.target.value })}>
                    {SENIORITY_OPTIONS.map(o => <option key={o} value={o}>{o}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="field__label" htmlFor="sk-remote">Where you want to work</label>
                  <select id="sk-remote" className="select"
                    value={form.remotePreference}
                    onChange={(e) => patch({ remotePreference: e.target.value })}>
                    {REMOTE_OPTIONS.map(o => <option key={o} value={o}>{o}</option>)}
                  </select>
                </div>
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Roles you want</label>
                  <TagInput values={form.targetRoles}
                    onChange={(v) => patch({ targetRoles: v })}
                    placeholder="e.g. Senior Backend Engineer" ariaLabel="Roles you want" />
                </div>
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Employment types</label>
                  <TagInput values={form.employmentTypes}
                    onChange={(v) => patch({ employmentTypes: v })}
                    placeholder="e.g. full-time, contract" ariaLabel="Employment types" />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">Skills</h2>
              <div className="field-grid">
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Must-have skills — <span className="subtle">the job has to mention these. More matches = higher rating</span></label>
                  <TagInput variant="primary"
                    values={form.primaryStack}
                    onChange={(v) => patch({ primaryStack: v })}
                    placeholder="e.g. C#, .NET, Postgres" ariaLabel="Must-have skills" />
                </div>
                <div className="field" style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">Nice-to-have skills — <span className="subtle">small bonus when mentioned</span></label>
                  <TagInput
                    values={form.secondaryStack}
                    onChange={(v) => patch({ secondaryStack: v })}
                    placeholder="e.g. Docker, Kubernetes" ariaLabel="Nice-to-have skills" />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">Industries</h2>
              <p className="field__hint" style={{ marginBottom: 'var(--space-3)' }}>Areas you'd like to work in.</p>
              <TagInput values={form.domains}
                onChange={(v) => patch({ domains: v })}
                placeholder="e.g. fintech, b2b saas" ariaLabel="Industries" />
            </section>

            <section className="card">
              <h2 className="card__title">Deal-breakers</h2>
              <p className="field__hint" style={{ marginBottom: 'var(--space-3)' }}>A listing with any of these is removed.</p>
              <TagInput variant="warning"
                values={form.disqualifiers}
                onChange={(v) => patch({ disqualifiers: v })}
                placeholder="e.g. on-site only, agency" ariaLabel="Deal-breakers" />
            </section>
          </div>

          <SaveBar
            visible={!!dirty}
            message={dirty ? 'Unsaved changes' : ''}
            saving={save.isPending}
            onSave={() => save.mutate()}
            onRevert={revert}
          />
        </>
      )}
    </div>
  )
}
