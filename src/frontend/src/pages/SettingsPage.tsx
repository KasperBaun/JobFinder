import { useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { exportConfig, importConfig, setLanguage } from '../api/client'
import { Toast } from '../components/Toast'
import { ActiveProfileSection } from '../components/ActiveProfileSection'
import { LanguageSelect } from '../components/LanguageSelect'
import { useT } from '../i18n'

export function SettingsPage() {
  const t = useT('settings')
  const queryClient = useQueryClient()
  const [toast, setToast] = useState<{ kind: 'ok' | 'err'; message: string } | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)

  const exporting = useMutation({
    mutationFn: exportConfig,
    onSuccess: () => setToast({ kind: 'ok', message: t.backupDownloaded }),
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  const importing = useMutation({
    mutationFn: importConfig,
    onSuccess: (res) => {
      // Everything on disk changed — drop all cached data so pages refetch.
      void queryClient.invalidateQueries()
      setToast({ kind: 'ok', message: t.restored(res.restored, res.warnings.length) })
    },
    onError: (err) => setToast({ kind: 'err', message: err instanceof Error ? err.message : String(err) }),
  })

  const savingLanguage = useMutation({
    mutationFn: setLanguage,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['setup'] })
      setToast({ kind: 'ok', message: t.languageSaved })
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
    if (!confirm(t.importConfirm)) return
    importing.mutate(file)
  }

  const busy = exporting.isPending || importing.isPending

  return (
    <div className="page page--settings">
      {toast && <Toast kind={toast.kind} message={toast.message} onDismiss={() => setToast(null)} />}

      <header className="page__header">
        <div className="page__eyebrow">{t.eyebrow}</div>
        <h1 className="page__heading">{t.title}</h1>
        <p className="page__lede">{t.lede}</p>
      </header>

      <section className="settings-section">
        <h2 className="settings-section__title">{t.languageTitle}</h2>
        <p className="settings-section__body">{t.languageBody}</p>
        <LanguageSelect
          className="input input--narrow"
          ariaLabel={t.languageLabel}
          onPick={(next) => savingLanguage.mutate(next)}
        />
      </section>

      <ActiveProfileSection notify={(kind, message) => setToast({ kind, message })} />

      <section className="settings-section">
        <h2 className="settings-section__title">{t.exportTitle}</h2>
        <p className="settings-section__body">{t.exportBody()}</p>
        <p className="settings-section__warning">{t.exportWarning}</p>
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => exporting.mutate()}
          disabled={busy}
        >
          {exporting.isPending ? <span className="spinner" /> : t.downloadBackup}
        </button>
      </section>

      <section className="settings-section">
        <h2 className="settings-section__title">{t.importTitle}</h2>
        <p className="settings-section__body">{t.importBody()}</p>
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
          {importing.isPending ? <span className="spinner" /> : t.chooseBackupFile}
        </button>
      </section>
    </div>
  )
}
