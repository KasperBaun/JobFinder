import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { completeSetup, getSetupStatus } from '../api/client'

export function SetupPage() {
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })

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

  const save = useMutation({
    mutationFn: () => completeSetup({ email: email.trim(), dataDir: dataDir.trim() }),
    onSuccess: () => {
      // Re-check status (now configured) so the app replaces this screen.
      void queryClient.invalidateQueries()
    },
    onError: (e) => setError(e instanceof Error ? e.message : String(e)),
  })

  const canSubmit = acknowledged && email.trim().length > 0 && dataDir.trim().length > 0 && !save.isPending

  return (
    <div className="setup">
      <div className="setup__card">
        <div className="setup__eyebrow">first-time setup</div>
        <h1 className="setup__heading">Welcome to <em>jobfinder</em></h1>
        <p className="setup__lede">
          Before we start, choose where jobfinder should keep your data on this computer — your
          profile, job sites, marks, and search history all live in one folder that stays on your
          machine. Nothing is created until you confirm below.
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
          <span className="setup__note">Used only to label your data folder on this machine — never sent anywhere.</span>
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
          onClick={() => { setError(null); save.mutate() }}
          disabled={!canSubmit}
        >
          {save.isPending ? <span className="spinner" /> : 'Get started'}
        </button>

        {data?.bootstrapPath && (
          <p className="setup__hint">Your choice is remembered in <code>{data.bootstrapPath}</code></p>
        )}
      </div>
    </div>
  )
}
