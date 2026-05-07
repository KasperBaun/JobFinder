interface Props {
  visible: boolean
  message?: string
  saving?: boolean
  onSave: () => void
  onRevert: () => void
}

export function SaveBar({ visible, message, saving, onSave, onRevert }: Props) {
  if (!visible) return null
  return (
    <div className="save-bar" role="region" aria-label="Unsaved changes">
      <div className="save-bar__msg">{message ?? 'Unsaved changes'}</div>
      <div className="save-bar__actions">
        <button type="button" className="btn btn--ghost" onClick={onRevert} disabled={saving}>
          Revert
        </button>
        <button type="button" className="btn btn--primary" onClick={onSave} disabled={saving}>
          {saving ? 'Saving…' : 'Save changes'}
        </button>
      </div>
    </div>
  )
}
