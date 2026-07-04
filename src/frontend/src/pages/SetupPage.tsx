import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { completeSetup, getSetupStatus, updateSkillset } from '../api/client'
import type { SkillsetUpdateRequest } from '../api/types'
import { TagInput } from '../components/TagInput'

const SENIORITY_OPTIONS = ['junior', 'mid', 'senior', 'lead', 'any'] as const
const REMOTE_OPTIONS = ['onsite', 'hybrid', 'remote', 'any'] as const

export function SetupPage() {
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
    mutationFn: () => completeSetup({ email: email.trim(), dataDir: dataDir.trim() }),
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

  function finishToApp() {
    void queryClient.invalidateQueries()
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
      }
      return updateSkillset(payload)
    },
    onSuccess: (res) => {
      if (!res.success) {
        setError(res.error ?? 'Could not save your profile.')
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
            <div className="setup__eyebrow">first-time setup · step 1 of 2</div>
            <h1 className="setup__heading">Welcome to <em>jobfinder</em></h1>
            <p className="setup__lede">
              First, choose where jobfinder should keep your data on this computer — your profile, job
              sites, marks, and search history all live in one folder that stays on your machine.
              Nothing is created until you confirm.
            </p>

            <label className="setup__field">
              <span className="setup__label">Your email</span>
              <input
                className="setup__input"
                type="email"
                value={email}
                placeholder="you@example.com"
                onChange={(e) => setEmail(e.target.value)}
              />
              <span className="setup__note">Used only to label your data folder — never sent anywhere.</span>
            </label>

            <label className="setup__field">
              <span className="setup__label">Data folder</span>
              <input
                className="setup__input"
                type="text"
                value={dataDir}
                spellCheck={false}
                onChange={(e) => setDataDir(e.target.value)}
              />
              <span className="setup__note">The suggestion is just a starting point — change it to any folder you like.</span>
            </label>

            <label className="setup__ack">
              <input
                type="checkbox"
                checked={acknowledged}
                onChange={(e) => setAcknowledged(e.target.checked)}
              />
              <span>I understand my data will be stored in this folder on my computer.</span>
            </label>

            {error && <div className="setup__error">{error}</div>}

            <button
              type="button"
              className="btn btn--primary btn--lg"
              onClick={() => { setError(null); location.mutate() }}
              disabled={!canContinue}
            >
              {location.isPending ? <span className="spinner" /> : 'Continue'}
            </button>

            {data?.bootstrapPath && (
              <p className="setup__hint">Your choice is remembered in <code>{data.bootstrapPath}</code></p>
            )}
          </>
        ) : (
          <>
            <div className="setup__eyebrow">first-time setup · step 2 of 2</div>
            <h1 className="setup__heading">Set up your <em>profile</em></h1>
            <p className="setup__lede">
              This is what jobfinder rates every listing against. Just the essentials for now — you can
              fine-tune everything later on the Profile page.
            </p>

            <label className="setup__field">
              <span className="setup__label">Your name</span>
              <input className="setup__input" type="text" value={name}
                placeholder="Jane Doe" onChange={(e) => setName(e.target.value)} />
            </label>

            <label className="setup__field">
              <span className="setup__label">Where you're based</span>
              <input className="setup__input" type="text" value={profileLocation}
                placeholder="e.g. Copenhagen, Denmark" onChange={(e) => setProfileLocation(e.target.value)} />
            </label>

            <div className="setup__row">
              <label className="setup__field">
                <span className="setup__label">Years of experience</span>
                <input className="setup__input" type="number" min={0} value={years}
                  onChange={(e) => setYears(Number(e.target.value) || 0)} />
              </label>
              <label className="setup__field">
                <span className="setup__label">Experience level</span>
                <select className="setup__input" value={seniority} onChange={(e) => setSeniority(e.target.value)}>
                  {SENIORITY_OPTIONS.map(o => <option key={o} value={o}>{o}</option>)}
                </select>
              </label>
              <label className="setup__field">
                <span className="setup__label">Where you want to work</span>
                <select className="setup__input" value={remote} onChange={(e) => setRemote(e.target.value)}>
                  {REMOTE_OPTIONS.map(o => <option key={o} value={o}>{o}</option>)}
                </select>
              </label>
            </div>

            <div className="setup__field">
              <span className="setup__label">Roles you want</span>
              <TagInput values={targetRoles} onChange={setTargetRoles}
                placeholder="e.g. Senior Backend Engineer" ariaLabel="Roles you want" />
            </div>

            <div className="setup__field">
              <span className="setup__label">Must-have skills <span className="subtle">— the ones a job should mention</span></span>
              <TagInput variant="primary" values={primaryStack} onChange={setPrimaryStack}
                placeholder="e.g. C#, .NET, Postgres" ariaLabel="Must-have skills" />
            </div>

            {error && <div className="setup__error">{error}</div>}

            <button
              type="button"
              className="btn btn--primary btn--lg"
              onClick={() => { setError(null); saveProfile.mutate() }}
              disabled={!canFinish}
            >
              {saveProfile.isPending ? <span className="spinner" /> : 'Finish setup'}
            </button>

            <button type="button" className="setup__skip" onClick={finishToApp} disabled={saveProfile.isPending}>
              Skip for now — I'll fill this in later
            </button>
          </>
        )}
      </div>
    </div>
  )
}
