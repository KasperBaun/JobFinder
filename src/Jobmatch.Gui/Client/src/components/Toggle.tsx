interface Props {
  checked: boolean
  onChange: (next: boolean) => void
  label?: string
  ariaLabel?: string
}

export function Toggle({ checked, onChange, label, ariaLabel }: Props) {
  return (
    <label className="toggle">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        aria-label={ariaLabel ?? label}
      />
      <span className="toggle__track">
        <span className="toggle__thumb" />
      </span>
      {label && <span className="toggle__label">{label}</span>}
    </label>
  )
}
