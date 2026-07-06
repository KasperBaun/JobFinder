import { useId, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { completeSetup, getSetupStatus } from '../api/client'

interface Props {
  notify: (kind: 'ok' | 'err', message: string) => void
}

// Lets the user point jobfinder at a different email / data folder after first-run setup —
// e.g. to keep separate work and personal profiles. Reuses the same endpoint the setup wizard
// uses; the server live-swaps the active context, so invalidating every query is enough to make
// all pages refetch from the new folder.
export function ActiveProfileSection({ notify }: Props) {
  const domId = useId()
  const queryClient = useQueryClient()
  const setup = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })

  const [editing, setEditing] = useState(false)
  const [email, setEmail] = useState('')
  const [dataDir, setDataDir] = useState('')

  const switching = useMutation({
    mutationFn: () => completeSetup({ email: email.trim(), dataDir: dataDir.trim() }),
    onSuccess: async () => {
      await queryClient.invalidateQueries()
      setEditing(false)
      notify('ok', 'Switched profile — every page now reads from the new folder.')
    },
    onError: (err) => notify('err', err instanceof Error ? err.message : String(err)),
  })

  function beginEdit() {
    setEmail(setup.data?.email ?? '')
    setDataDir(setup.data?.dataDir ?? '')
    setEditing(true)
  }

  function handleSwitch() {
    const e = email.trim()
    const d = dataDir.trim()
    if (!e || !d) {
      notify('err', 'Both an email and a data folder are required.')
      return
    }
    if (
      !confirm(
        `Point jobfinder at:\n\n${d}\n\nIt will read your profile, job sites, marks and history from `
        + 'there. Your current data stays where it is — switch back any time by entering the old '
        + 'folder again.',
      )
    ) {
      return
    }
    switching.mutate()
  }

  return (
    <section className="settings-section" data-testid={domId}>
      <h2 className="settings-section__title">Active profile</h2>
      <p className="settings-section__body">
        Everything jobfinder knows lives in one folder on this computer. Switch to a different email
        or folder to keep separate setups.
      </p>

      <dl className="settings-facts">
        <div className="settings-facts__row">
          <dt>Email</dt>
          <dd>{setup.isLoading ? <span className="muted">…</span> : setup.data?.email ?? '—'}</dd>
        </div>
        <div className="settings-facts__row">
          <dt>Data folder</dt>
          <dd className="mono">{setup.isLoading ? <span className="muted">…</span> : setup.data?.dataDir ?? '—'}</dd>
        </div>
      </dl>

      {!editing ? (
        <button type="button" className="btn" onClick={beginEdit} disabled={setup.isLoading}>
          Switch profile…
        </button>
      ) : (
        <>
          <div className="field-grid">
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label" htmlFor={`${domId}-email`}>Email</label>
              <input
                id={`${domId}-email`}
                type="email"
                className="input"
                value={email}
                placeholder="you@example.com"
                onChange={e => setEmail(e.target.value)}
              />
            </div>
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label" htmlFor={`${domId}-dir`}>Data folder</label>
              <input
                id={`${domId}-dir`}
                type="text"
                className="input input--mono"
                value={dataDir}
                spellCheck={false}
                onChange={e => setDataDir(e.target.value)}
              />
            </div>
          </div>
          <div className="settings-facts__actions">
            <button type="button" className="btn btn--primary" onClick={handleSwitch} disabled={switching.isPending}>
              {switching.isPending ? <span className="spinner" /> : 'Switch profile'}
            </button>
            <button type="button" className="btn" onClick={() => setEditing(false)} disabled={switching.isPending}>
              Cancel
            </button>
          </div>
        </>
      )}
    </section>
  )
}
