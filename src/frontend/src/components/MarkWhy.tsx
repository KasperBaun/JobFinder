import { useEffect, useRef, useState } from 'react'
import { useT } from '../i18n'

interface Props {
  reason?: string
  saving: boolean
  onSave: (reason: string | null) => void
}

// The "why" annotation next to a set mark: an add-link when empty, a quoted chip
// when present, and an inline input while editing. Saving is owned by MarkButton.
export function MarkWhy({ reason, saving, onSave }: Props) {
  const t = useT('listing')
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(reason ?? '')
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!editing) setDraft(reason ?? '')
  }, [reason, editing])

  useEffect(() => {
    if (editing) inputRef.current?.focus()
  }, [editing])

  function commit() {
    setEditing(false)
    const next = draft.trim()
    if (next === (reason ?? '')) return
    onSave(next.length > 0 ? next : null)
  }

  if (editing) {
    return (
      <input
        ref={inputRef}
        className="mark-why__input"
        type="text"
        value={draft}
        maxLength={500}
        placeholder={t.whyPlaceholder}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commit()
          if (e.key === 'Escape') { setDraft(reason ?? ''); setEditing(false) }
        }}
      />
    )
  }

  if (reason) {
    return (
      <button
        type="button"
        className="mark-why mark-why--set"
        onClick={() => setEditing(true)}
        disabled={saving}
        title={t.whyEdit(reason)}
      >
        “{reason}”
      </button>
    )
  }

  return (
    <button
      type="button"
      className="mark-why"
      onClick={() => setEditing(true)}
      disabled={saving}
      aria-label={t.whyTooltip}
      data-tooltip={t.whyTooltip}
    >
      {t.whyAdd}
    </button>
  )
}
