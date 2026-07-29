import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getSetupStatus, getSkillset, updateSkillset } from '../api/client'
import type { SkillsetResponse, SkillsetUpdateRequest } from '../api/types'
import { TagInput } from '../components/TagInput'
import { SaveBar } from '../components/SaveBar'
import { Toast } from '../components/Toast'
import { CvImportModal } from '../components/CvImportModal'
import { useT } from '../i18n'

type Form = SkillsetUpdateRequest

const REMOTE_OPTIONS = ['onsite', 'hybrid', 'remote', 'any'] as const
const SENIORITY_OPTIONS = ['junior', 'mid', 'senior', 'lead', 'any'] as const

const EMPTY_FORM: Form = {
  name: '', location: '', experienceYears: 0, targetRoles: [],
  remotePreference: 'any', seniority: 'mid', primaryStack: [], secondaryStack: [],
  domains: [], disqualifiers: [], languages: [], employmentTypes: [],
  country: '', region: '', metro: [], preferredCompanies: [],
}

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
    preferredCompanies: [...s.preferredCompanies],
  }
}

function isDirty(a: Form, b: Form): boolean {
  return JSON.stringify(a) !== JSON.stringify(b)
}

export function SkillsetPage() {
  const t = useT('skillset')
  const queryClient = useQueryClient()
  const setup = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })
  const profileExists = setup.data?.profileExists
  // Only load an existing profile in edit mode; in create mode we start from a blank form.
  const { data, isLoading, error } = useQuery({
    queryKey: ['skillset'],
    queryFn: getSkillset,
    enabled: profileExists === true,
  })

  const creating = profileExists === false

  const [form, setForm] = useState<Form | null>(null)
  const [original, setOriginal] = useState<Form | null>(null)
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const [cvOpen, setCvOpen] = useState(false)
  // Fields last prefilled from a CV; highlighted until edited, reverted, or saved.
  const [suggested, setSuggested] = useState<Set<string>>(new Set())

  useEffect(() => {
    if (original !== null) return
    if (profileExists === false) {
      setForm(EMPTY_FORM)
      setOriginal(EMPTY_FORM)
    } else if (data) {
      const f = toForm(data)
      setForm(f)
      setOriginal(f)
    }
  }, [data, original, profileExists])

  const dirty = useMemo(
    () => form && original ? isDirty(form, original) : false,
    [form, original],
  )

  const save = useMutation({
    mutationFn: async () => {
      if (!form) throw new Error('no form state')
      const res = await updateSkillset(form)
      if (!res.success) throw new Error(res.error ?? t.saveFailed)
      return form
    },
    onSuccess: (saved) => {
      setOriginal(saved)
      setSuggested(new Set())
      // Invalidate setup too: a first save flips profileExists true (clears create-mode + nudges).
      void queryClient.invalidateQueries({ queryKey: ['skillset'] })
      void queryClient.invalidateQueries({ queryKey: ['setup'] })
      setToast({ kind: 'ok', message: creating ? t.created : t.saved })
    },
    onError: (err) => {
      setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) })
    },
  })

  function patch(p: Partial<Form>) {
    setForm((f) => f ? { ...f, ...p } : f)
    // A manual edit clears the "from CV" marker for the touched fields.
    setSuggested((prev) => {
      if (prev.size === 0) return prev
      const next = new Set(prev)
      for (const key of Object.keys(p)) next.delete(key)
      return next
    })
  }

  function revert() {
    if (original) setForm(original)
    setSuggested(new Set())
  }

  function applyCv(p: Partial<Form>, keys: (keyof Form)[]) {
    setForm((f) => f ? { ...f, ...p } : f)
    setSuggested(new Set(keys))
    setCvOpen(false)
    setToast({ kind: 'ok', message: t.prefilled })
  }

  const sug = (key: keyof Form) => `field${suggested.has(key) ? ' field--suggested' : ''}`

  return (
    <div className="page page--wide">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{creating ? t.headingCreate() : t.headingEdit()}</h1>
        <p className="page__lede">{creating ? t.ledeCreate : t.ledeEdit}</p>
        <div style={{ marginTop: 'var(--space-3)' }}>
          <button type="button" className="btn btn--secondary" onClick={() => setCvOpen(true)}>
            {t.fillFromCv}
          </button>
        </div>
      </header>

      {cvOpen && form && (
        <CvImportModal current={form} onApply={applyCv} onClose={() => setCvOpen(false)} />
      )}

      {isLoading && <div className="muted">{t.loading}</div>}
      {error && <div className="error-text">{t.loadFailed}</div>}

      {form && (
        <>
          <div className="card-stack">
            <section className="card">
              <h2 className="card__title">{t.aboutYou}</h2>
              <div className="field-grid">
                <div className={sug('name')}>
                  <label className="field__label" htmlFor="sk-name">{t.name}</label>
                  <input id="sk-name" className="input" value={form.name}
                    onChange={(e) => patch({ name: e.target.value })} />
                </div>
                <div className={sug('location')}>
                  <label className="field__label" htmlFor="sk-location">{t.location}</label>
                  <input id="sk-location" className="input" value={form.location}
                    onChange={(e) => patch({ location: e.target.value })} />
                </div>
                <div className={sug('country')}>
                  <label className="field__label" htmlFor="sk-country">{t.country}</label>
                  <input id="sk-country" className="input" value={form.country ?? ''} placeholder={t.optional}
                    onChange={(e) => patch({ country: e.target.value })} />
                </div>
                <div className={sug('region')}>
                  <label className="field__label" htmlFor="sk-region">{t.region}</label>
                  <input id="sk-region" className="input" value={form.region ?? ''} placeholder={t.optional}
                    onChange={(e) => patch({ region: e.target.value })} />
                </div>
                <div className={sug('experienceYears')}>
                  <label className="field__label" htmlFor="sk-exp">{t.experienceYears}</label>
                  <input id="sk-exp" type="number" min={0} className="input input--narrow input--mono tabular"
                    value={form.experienceYears}
                    onChange={(e) => patch({ experienceYears: Number(e.target.value) || 0 })} />
                </div>
                <div className={sug('languages')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.languages}</label>
                  <TagInput values={form.languages}
                    onChange={(v) => patch({ languages: v })}
                    placeholder={t.languagesPlaceholder} ariaLabel={t.languages} />
                </div>
                <div className={sug('metro')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.metro}</label>
                  <TagInput values={form.metro}
                    onChange={(v) => patch({ metro: v })}
                    placeholder={t.metroPlaceholder} ariaLabel={t.metro} />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">{t.rolesAndPreferences}</h2>
              <div className="field-grid">
                <div className={sug('seniority')}>
                  <label className="field__label" htmlFor="sk-seniority">{t.seniorityLabel}</label>
                  <select id="sk-seniority" className="select"
                    value={form.seniority}
                    onChange={(e) => patch({ seniority: e.target.value })}>
                    {SENIORITY_OPTIONS.map(o => <option key={o} value={o}>{t.seniority[o]}</option>)}
                  </select>
                </div>
                <div className={sug('remotePreference')}>
                  <label className="field__label" htmlFor="sk-remote">{t.remoteLabel}</label>
                  <select id="sk-remote" className="select"
                    value={form.remotePreference}
                    onChange={(e) => patch({ remotePreference: e.target.value })}>
                    {REMOTE_OPTIONS.map(o => <option key={o} value={o}>{t.remote[o]}</option>)}
                  </select>
                </div>
                <div className={sug('targetRoles')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.targetRoles}</label>
                  <TagInput values={form.targetRoles}
                    onChange={(v) => patch({ targetRoles: v })}
                    placeholder={t.targetRolesPlaceholder} ariaLabel={t.targetRoles} />
                </div>
                <div className={sug('employmentTypes')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.employmentTypes}</label>
                  <TagInput values={form.employmentTypes}
                    onChange={(v) => patch({ employmentTypes: v })}
                    placeholder={t.employmentTypesPlaceholder} ariaLabel={t.employmentTypes} />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">{t.skills}</h2>
              <div className="field-grid">
                <div className={sug('primaryStack')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.primaryStack} — <span className="subtle">{t.primaryStackHint}</span></label>
                  <TagInput variant="primary"
                    values={form.primaryStack}
                    onChange={(v) => patch({ primaryStack: v })}
                    placeholder={t.primaryStackPlaceholder} ariaLabel={t.primaryStack} />
                </div>
                <div className={sug('secondaryStack')} style={{ gridColumn: '1 / -1' }}>
                  <label className="field__label">{t.secondaryStack} — <span className="subtle">{t.secondaryStackHint}</span></label>
                  <TagInput
                    values={form.secondaryStack}
                    onChange={(v) => patch({ secondaryStack: v })}
                    placeholder={t.secondaryStackPlaceholder} ariaLabel={t.secondaryStack} />
                </div>
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">{t.industries}</h2>
              <p className="field__hint" style={{ marginBottom: 'var(--space-3)' }}>{t.industriesHint}</p>
              <div className={sug('domains')}>
                <TagInput values={form.domains}
                  onChange={(v) => patch({ domains: v })}
                  placeholder={t.industriesPlaceholder} ariaLabel={t.industries} />
              </div>
            </section>

            <section className="card">
              <h2 className="card__title">{t.favoriteCompanies}</h2>
              <p className="field__hint" style={{ marginBottom: 'var(--space-3)' }}>{t.favoriteCompaniesHint}</p>
              <TagInput variant="primary"
                values={form.preferredCompanies}
                onChange={(v) => patch({ preferredCompanies: v })}
                placeholder={t.favoriteCompaniesPlaceholder} ariaLabel={t.favoriteCompanies} />
            </section>

            <section className="card">
              <h2 className="card__title">{t.dealBreakers}</h2>
              <p className="field__hint" style={{ marginBottom: 'var(--space-3)' }}>{t.dealBreakersHint}</p>
              <TagInput variant="warning"
                values={form.disqualifiers}
                onChange={(v) => patch({ disqualifiers: v })}
                placeholder={t.dealBreakersPlaceholder} ariaLabel={t.dealBreakers} />
            </section>
          </div>

          <SaveBar
            visible={!!dirty}
            message={dirty ? undefined : ''}
            saving={save.isPending}
            onSave={() => save.mutate()}
            onRevert={revert}
          />
        </>
      )}
    </div>
  )
}
