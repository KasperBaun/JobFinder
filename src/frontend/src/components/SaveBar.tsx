import { useT } from '../i18n'

interface Props {
  visible: boolean
  message?: string
  saving?: boolean
  onSave: () => void
  onRevert: () => void
}

export function SaveBar({ visible, message, saving, onSave, onRevert }: Props) {
  const t = useT('common')
  if (!visible) return null
  return (
    <div className="save-bar" role="region" aria-label={t.unsavedChanges}>
      <div className="save-bar__msg">{message ?? t.unsavedChanges}</div>
      <div className="save-bar__actions">
        <button type="button" className="btn btn--ghost" onClick={onRevert} disabled={saving}>
          {t.revert}
        </button>
        <button type="button" className="btn btn--primary" onClick={onSave} disabled={saving}>
          {saving ? t.saving : t.saveChanges}
        </button>
      </div>
    </div>
  )
}
