import { useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { exportConfig, importConfig } from '../api/client'
import { Toast } from '../components/Toast'
import { ActiveProfileSection } from '../components/ActiveProfileSection'

export function SettingsPage() {
  const queryClient = useQueryClient()
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)

  const exporting = useMutation({
    mutationFn: exportConfig,
    onSuccess: () => setToast({ kind: 'ok', message: 'Backup downloaded.' }),
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  const importing = useMutation({
    mutationFn: importConfig,
    onSuccess: (res) => {
      // Everything on disk changed — drop all cached data so pages refetch.
      void queryClient.invalidateQueries()
      const warned = res.warnings.length > 0 ? ` (${res.warnings.length} item(s) skipped)` : ''
      setToast({ kind: 'ok', message: `Restored ${res.restored} file(s).${warned}` })
    },
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  function handlePickFile() {
    fileInput.current?.click()
  }

  function handleFileChosen(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-picking the same file later
    if (!file) return
    if (
      !confirm(
        'Importing replaces everything currently in this profile — your job sites, profile, marks, '
        + 'and search history. A backup of the current data is saved automatically first. Continue?',
      )
    ) {
      return
    }
    importing.mutate(file)
  }

  const busy = exporting.isPending || importing.isPending

  return (
    <div className="page page--settings">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">settings</div>
        <h1 className="page__heading">Settings</h1>
        <p className="page__lede">
          Your profile&apos;s data location, and backup or restore everything to a file.
        </p>
      </header>

      <ActiveProfileSection notify={(kind, message) => setToast({ kind, message })} />

      <section className="settings-section">
        <h2 className="settings-section__title">Export a backup</h2>
        <p className="settings-section__body">
          Downloads a single <code>.zip</code> with everything in this profile: your job sites and their
          settings, your profile, your saved marks, and your full search history. The large AI model
          isn&apos;t included — it re-downloads automatically when needed.
        </p>
        <p className="settings-section__warning">
          ⚠ The file includes any saved site passwords / API keys. Keep it somewhere private.
        </p>
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => exporting.mutate()}
          disabled={busy}
        >
          {exporting.isPending ? <span className="spinner" /> : 'Download backup'}
        </button>
      </section>

      <section className="settings-section">
        <h2 className="settings-section__title">Import a backup</h2>
        <p className="settings-section__body">
          Restores a backup file you exported earlier. This <strong>replaces</strong> the data in this
          profile. Don&apos;t worry — the current data is backed up automatically before anything is
          overwritten.
        </p>
        <input
          ref={fileInput}
          type="file"
          accept=".zip,application/zip"
          onChange={handleFileChosen}
          hidden
        />
        <button
          type="button"
          className="btn"
          onClick={handlePickFile}
          disabled={busy}
        >
          {importing.isPending ? <span className="spinner" /> : 'Choose backup file…'}
        </button>
      </section>
    </div>
  )
}
