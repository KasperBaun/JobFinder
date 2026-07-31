import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { completeSetup, getSetupStatus, updateSkillset } from '../api/client'
import type { SkillsetUpdateRequest } from '../api/types'
import { TagInput } from '../components/TagInput'
import { CvImportModal } from '../components/CvImportModal'
import { LanguageSelect } from '../components/LanguageSelect'
import { useLocale, useT } from '../i18n'

const SENIORITY_OPTIONS = ['junior', 'mid', 'senior', 'lead', 'any'] as const
const REMOTE_OPTIONS = ['onsite', 'hybrid', 'remote', 'any'] as const

export function SetupPage() {
  const t = useT('setup')
  const common = useT('common')
  const options = useT('skillset')
  const { locale } = useLocale()
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })

  const [step, setStep] = useState<1 | 2>(1)

  // ---- step 1: data location ----
  const [email, setEmail] = useState('')
  const [dataDir, setDataDir] = useState('')
  const [acknowledged, setAcknowledged] = useState(false)
  const [seeded, setSeeded] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (data && !seeded) {
      setEmail(data.suggestedEmail)
      setDataDir(data.suggestedDataDir)
      setSeeded(true)
    }
  }, [data, seeded])

  const location = useMutation({
    mutationFn: () => completeSetup({ email: email.trim(), dataDir: dataDir.trim(), language: locale }),
    // Do NOT invalidate here — that would flip `configured` true and unmount the wizard.
    // Advance to the profile step instead; we invalidate once, at the very end.
    onSuccess: () => setStep(2),
    onError: (e) => setError(e instanceof Error ? e.message : String(e)),
  })

  const canContinue =
    acknowledged && email.trim().length > 0 && dataDir.trim().length > 0 && !location.isPending

  // ---- step 2: profile essentials ----
  const [name, setName] = useState('')
  const [profileLocation, setProfileLocation] = useState('')
  const [years, setYears] = useState(0)
  const [seniority, setSeniority] = useState('mid')
  const [remote, setRemote] = useState('any')
  const [targetRoles, setTargetRoles] = useState<string[]>([])
  const [primaryStack, setPrimaryStack] = useState<string[]>([])
  const [address, setAddress] = useState('')
  const [radiusKm, setRadiusKm] = useState(0)
  const [cvOpen, setCvOpen] = useState(false)

  function finishToApp() {
    void queryClient.invalidateQueries()
  }

  // The wizard only owns the essentials; the CV modal is scoped to those fields.
  // The full form (incl. domains, languages, …) is available later on the Profile page.
  const cvCurrent: SkillsetUpdateRequest = {
    name, location: profileLocation, experienceYears: years, targetRoles,
    remotePreference: remote, seniority, primaryStack, secondaryStack: [],
    domains: [], disqualifiers: [], languages: [], employmentTypes: [],
    country: '', region: '', metro: [], preferredCompanies: [],
  }

  function applyCv(patch: Partial<SkillsetUpdateRequest>) {
    if (patch.name !== undefined) setName(patch.name)
    if (patch.location !== undefined) setProfileLocation(patch.location)
    if (patch.experienceYears !== undefined) setYears(patch.experienceYears)
    if (patch.seniority !== undefined) setSeniority(patch.seniority)
    if (patch.remotePreference !== undefined) setRemote(patch.remotePreference)
    if (patch.targetRoles !== undefined) setTargetRoles(patch.targetRoles)
    if (patch.primaryStack !== undefined) setPrimaryStack(patch.primaryStack)
    setCvOpen(false)
  }

  const saveProfile = useMutation({
    mutationFn: () => {
      const payload: SkillsetUpdateRequest = {
        name: name.trim(),
        location: profileLocation.trim(),
        experienceYears: years,
        targetRoles,
        remotePreference: remote,
        seniority,
        primaryStack,
        secondaryStack: [],
        domains: [],
        disqualifiers: [],
        languages: [],
        employmentTypes: [],
        country: '',
        region: '',
        metro: [],
        preferredCompanies: [],
        address: address.trim(),
        radiusKm,
      }
      return updateSkillset(payload)
    },
    onSuccess: (res) => {
      if (!res.success) {
        setError(res.error ?? t.saveFailed)
        return
      }
      finishToApp()
    },
    onError: (e) => setError(e instanceof Error ? e.message : String(e)),
  })

  const canFinish =
    name.trim().length > 0 && profileLocation.trim().length > 0 && !saveProfile.isPending

  return (
    <div className="setup">
      <div className="setup__card">
        {step === 1 ? (
          <>
            <div className="setup__eyebrow">
              <span className="setup__eyebrow-title">{t.eyebrow}</span>
              <span className="setup__step">{t.step(1, 2)}</span>
            </div>
            <h1 className="setup__heading">{t.welcomeHeading()}</h1>
            <p className="setup__lede">{t.welcomeLede}</p>

            <label className="setup__field">
              <span className="setup__label">{t.languageLabel}</span>
              <LanguageSelect className="setup__input" ariaLabel={t.languageLabel} />
              <span className="setup__note">{t.languageNote}</span>
            </label>

            <label className="setup__field">
              <span className="setup__label">{t.emailLabel}</span>
              <input
                className="setup__input"
                type="email"
                value={email}
                placeholder="you@example.com"
                onChange={(e) => setEmail(e.target.value)}
              />
              <span className="setup__note">{t.emailNote}</span>
            </label>

            <label className="setup__field">
              <span className="setup__label">{t.dataDirLabel}</span>
              <input
                className="setup__input"
                type="text"
                value={dataDir}
                spellCheck={false}
                onChange={(e) => setDataDir(e.target.value)}
              />
              <span className="setup__note">{t.dataDirNote}</span>
            </label>

            <label className="setup__ack">
              <input
                type="checkbox"
                checked={acknowledged}
                onChange={(e) => setAcknowledged(e.target.checked)}
              />
              <span>{t.acknowledge}</span>
            </label>

            {error && <div className="setup__error">{error}</div>}

            <button
              type="button"
              className="btn btn--primary btn--lg"
              onClick={() => { setError(null); location.mutate() }}
              disabled={!canContinue}
            >
              {location.isPending ? <span className="spinner" /> : common.continue}
            </button>

            {data?.bootstrapPath && (
              <p className="setup__hint">{t.rememberedIn} <code>{data.bootstrapPath}</code></p>
            )}
          </>
        ) : (
          <>
            <div className="setup__eyebrow">
              <span className="setup__eyebrow-title">{t.eyebrow}</span>
              <span className="setup__step">{t.step(2, 2)}</span>
            </div>
            <h1 className="setup__heading">{t.profileHeading()}</h1>
            <p className="setup__lede">
              {t.profileLede}{' '}
              <button type="button" className="setup__cv-link" onClick={() => setCvOpen(true)}>
                {t.cvLink}
              </button>
            </p>

            {cvOpen && (
              <CvImportModal
                current={cvCurrent}
                fields={['name', 'location', 'experienceYears', 'seniority', 'remotePreference', 'targetRoles', 'primaryStack']}
                onApply={applyCv}
                onClose={() => setCvOpen(false)}
              />
            )}

            <label className="setup__field">
              <span className="setup__label">{t.nameLabel}</span>
              <input className="setup__input" type="text" value={name}
                placeholder={t.namePlaceholder} onChange={(e) => setName(e.target.value)} />
            </label>

            <label className="setup__field">
              <span className="setup__label">{t.locationLabel}</span>
              <input className="setup__input" type="text" value={profileLocation}
                placeholder={t.locationPlaceholder} onChange={(e) => setProfileLocation(e.target.value)} />
            </label>

            <div className="setup__row">
              <label className="setup__field">
                <span className="setup__label">{t.yearsLabel}</span>
                <input className="setup__input" type="number" min={0} value={years}
                  onChange={(e) => setYears(Number(e.target.value) || 0)} />
              </label>
              <label className="setup__field">
                <span className="setup__label">{t.seniorityLabel}</span>
                <select className="setup__input" value={seniority} onChange={(e) => setSeniority(e.target.value)}>
                  {SENIORITY_OPTIONS.map(o => <option key={o} value={o}>{options.seniority[o]}</option>)}
                </select>
              </label>
              <label className="setup__field">
                <span className="setup__label">{t.remoteLabel}</span>
                <select className="setup__input" value={remote} onChange={(e) => setRemote(e.target.value)}>
                  {REMOTE_OPTIONS.map(o => <option key={o} value={o}>{options.remote[o]}</option>)}
                </select>
              </label>
            </div>

            <div className="setup__field">
              <span className="setup__label">{t.rolesLabel}</span>
              <TagInput values={targetRoles} onChange={setTargetRoles}
                placeholder={t.rolesPlaceholder} ariaLabel={t.rolesLabel} />
            </div>

            <div className="setup__field">
              <span className="setup__label">{t.primaryStackLabel} <span className="subtle">{t.primaryStackHint}</span></span>
              <TagInput variant="primary" values={primaryStack} onChange={setPrimaryStack}
                placeholder={t.primaryStackPlaceholder} ariaLabel={t.primaryStackLabel} />
            </div>

            <div className="setup__row">
              <label className="setup__field">
                <span className="setup__label">{t.addressLabel}</span>
                <input className="setup__input" type="text" value={address}
                  placeholder={t.addressPlaceholder} onChange={(e) => setAddress(e.target.value)} />
                <span className="setup__note">{t.addressNote}</span>
              </label>
              <label className="setup__field">
                <span className="setup__label">{t.radiusLabel}</span>
                <input className="setup__input" type="number" min={0} value={radiusKm}
                  onChange={(e) => setRadiusKm(Math.max(0, Number(e.target.value) || 0))} />
              </label>
            </div>

            {error && <div className="setup__error">{error}</div>}

            <button
              type="button"
              className="btn btn--primary btn--lg"
              onClick={() => { setError(null); saveProfile.mutate() }}
              disabled={!canFinish}
            >
              {saveProfile.isPending ? <span className="spinner" /> : t.finish}
            </button>

            <button type="button" className="setup__skip" onClick={finishToApp} disabled={saveProfile.isPending}>
              {t.skip}
            </button>
          </>
        )}
      </div>
    </div>
  )
}
