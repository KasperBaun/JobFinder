import { useRef, useState, type KeyboardEvent } from 'react'

interface Props {
  values: string[]
  onChange: (next: string[]) => void
  placeholder?: string
  variant?: 'default' | 'primary' | 'warning'
  ariaLabel?: string
}

export function TagInput({ values, onChange, placeholder, variant = 'default', ariaLabel }: Props) {
  const [draft, setDraft] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  function commit() {
    const t = draft.trim()
    if (!t) return
    if (values.includes(t)) {
      setDraft('')
      return
    }
    onChange([...values, t])
    setDraft('')
  }

  function remove(index: number) {
    onChange(values.filter((_, i) => i !== index))
  }

  function handleKey(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault()
      commit()
    } else if (e.key === 'Backspace' && draft.length === 0 && values.length > 0) {
      e.preventDefault()
      remove(values.length - 1)
    }
  }

  const cls = variant === 'primary' ? 'tag-input tag-input--primary'
            : variant === 'warning' ? 'tag-input tag-input--warning'
            : 'tag-input'

  return (
    <div className={cls} onClick={() => inputRef.current?.focus()} aria-label={ariaLabel}>
      {values.map((v, i) => (
        <span key={`${v}-${i}`} className="tag">
          {v}
          <button
            type="button"
            className="tag__close"
            onClick={(e) => { e.stopPropagation(); remove(i) }}
            aria-label={`Remove ${v}`}
          >
            ×
          </button>
        </span>
      ))}
      <input
        ref={inputRef}
        className="tag-input__entry"
        value={draft}
        placeholder={values.length === 0 ? (placeholder ?? 'Type and press Enter') : ''}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKey}
        onBlur={commit}
      />
    </div>
  )
}
