import { activeLocale } from './active'
import type { Locale } from './locale'

// Intl constructors are expensive enough that building one per render (which String.localeCompare
// and Number.toLocaleString do implicitly) shows up on a longlist of hundreds of rows.
const numberFormats = new Map<string, Intl.NumberFormat>()
const collators = new Map<Locale, Intl.Collator>()
const relativeFormats = new Map<Locale, Intl.RelativeTimeFormat>()
const dateTimeFormats = new Map<Locale, Intl.DateTimeFormat>()

function numberFormat(locale: Locale, digits?: number): Intl.NumberFormat {
  const key = `${locale}:${digits ?? ''}`
  let format = numberFormats.get(key)
  if (!format) {
    format = new Intl.NumberFormat(
      locale,
      digits == null ? undefined : { minimumFractionDigits: digits, maximumFractionDigits: digits },
    )
    numberFormats.set(key, format)
  }
  return format
}

/** Grouped integer in the active locale: 1,234 (en) · 1.234 (da). */
export function n(value: number): string {
  return numberFormat(activeLocale()).format(value)
}

/** Fixed-precision decimal in the active locale: 0.82 (en) · 0,82 (da). */
export function dec(value: number, digits: number): string {
  return numberFormat(activeLocale(), digits).format(value)
}

/** Danish sorts æ, ø and å after z — the whole reason this exists instead of localeCompare. */
export function collator(locale: Locale): Intl.Collator {
  let value = collators.get(locale)
  if (!value) {
    value = new Intl.Collator(locale, { numeric: true, sensitivity: 'base' })
    collators.set(locale, value)
  }
  return value
}

// numeric: 'always' — 'auto' would render "yesterday" / "i går" instead of "1 day ago", which
// breaks the fixed-width tabular timeline column and changes today's English output.
export function relativeTimeFormat(locale: Locale): Intl.RelativeTimeFormat {
  let value = relativeFormats.get(locale)
  if (!value) {
    value = new Intl.RelativeTimeFormat(locale, { numeric: 'always' })
    relativeFormats.set(locale, value)
  }
  return value
}

// An explicit style rather than the toLocaleString default, so output doesn't silently follow the
// host OS locale — which it did in the Electron shell.
export function dateTimeFormat(locale: Locale): Intl.DateTimeFormat {
  let value = dateTimeFormats.get(locale)
  if (!value) {
    value = new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' })
    dateTimeFormats.set(locale, value)
  }
  return value
}
