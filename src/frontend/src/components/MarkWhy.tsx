import { useEffect, useRef, useState } from 'react'

interface Props {
  reason?: string
  saving: boolean
  onSave: (reason: string | null) => void
}

const ADD_TOOLTIP = 'Add a short why — it teaches the AI what to look for next time.'

// The "why" annotation next to a set mark: an add-link when empty, a quoted chip
// when present, and an inline input while editing. Saving is owned by MarkButton.
export function MarkWhy({ reason, saving, onSave }: Props) {
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
        placeholder="Why? e.g. “I'm not a student”"
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
        title={`${reason} — click to edit`}
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
      aria-label={ADD_TOOLTIP}
      data-tooltip={ADD_TOOLTIP}
    >
      + why?
    </button>
  )
}
