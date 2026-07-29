import { LOCALES, LOCALE_LABEL, useLocale } from '../i18n'
import type { Locale } from '../i18n'

type Props = {
  className?: string
  ariaLabel: string
  /** Called after the locale is applied, so callers can persist it server-side. */
  onPick?: (locale: Locale) => void
}

export function LanguageSelect({ className, ariaLabel, onPick }: Props) {
  const { locale, setLocale } = useLocale()

  return (
    <select
      className={className}
      value={locale}
      aria-label={ariaLabel}
      onChange={(e) => {
        const next = e.target.value as Locale
        setLocale(next)
        onPick?.(next)
      }}
    >
      {LOCALES.map(l => (
        <option key={l} value={l}>{LOCALE_LABEL[l]}</option>
      ))}
    </select>
  )
}
