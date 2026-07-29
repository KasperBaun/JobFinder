import { useId, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { completeSetup, getSetupStatus } from '../api/client'
import { useT } from '../i18n'

interface Props {
  notify: (kind: 'ok' | 'err', message: string) => void
}

// Lets the user point jobfinder at a different email / data folder after first-run setup —
// e.g. to keep separate work and personal profiles. Reuses the same endpoint the setup wizard
// uses; the server live-swaps the active context, so invalidating every query is enough to make
// all pages refetch from the new folder.
export function ActiveProfileSection({ notify }: Props) {
  const t = useT('settings')
  const common = useT('common')
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
      notify('ok', t.switched)
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
      notify('err', t.bothRequired)
      return
    }
    if (!confirm(t.switchConfirm(d))) return
    switching.mutate()
  }

  return (
    <section className="settings-section" data-testid={domId}>
      <h2 className="settings-section__title">{t.activeProfileTitle}</h2>
      <p className="settings-section__body">{t.activeProfileBody}</p>

      <dl className="settings-facts">
        <div className="settings-facts__row">
          <dt>{t.email}</dt>
          <dd>{setup.isLoading ? <span className="muted">…</span> : setup.data?.email ?? '—'}</dd>
        </div>
        <div className="settings-facts__row">
          <dt>{t.dataFolder}</dt>
          <dd className="mono">{setup.isLoading ? <span className="muted">…</span> : setup.data?.dataDir ?? '—'}</dd>
        </div>
      </dl>

      {!editing ? (
        <button type="button" className="btn" onClick={beginEdit} disabled={setup.isLoading}>
          {t.switchProfileCta}
        </button>
      ) : (
        <>
          <div className="field-grid">
            <div className="field" style={{ gridColumn: '1 / -1' }}>
              <label className="field__label" htmlFor={`${domId}-email`}>{t.email}</label>
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
              <label className="field__label" htmlFor={`${domId}-dir`}>{t.dataFolder}</label>
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
              {switching.isPending ? <span className="spinner" /> : t.switchProfile}
            </button>
            <button type="button" className="btn" onClick={() => setEditing(false)} disabled={switching.isPending}>
              {common.cancel}
            </button>
          </div>
        </>
      )}
    </section>
  )
}
