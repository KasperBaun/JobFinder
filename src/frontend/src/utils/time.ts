import { activeLocale } from '../i18n/active'
import { activeMessages } from '../i18n/catalogs'
import { dateTimeFormat, dec, relativeTimeFormat } from '../i18n/format'

export function formatRelative(iso: string | undefined): string {
  const t = activeMessages().common
  if (!iso) return t.emDash
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  const diffMs = Date.now() - date.getTime()
  const sec = Math.round(diffMs / 1000)
  if (sec < 60) return t.justNow

  const rtf = relativeTimeFormat(activeLocale())
  const min = Math.round(sec / 60)
  if (min < 60) return rtf.format(-min, 'minute')
  const hr = Math.round(min / 60)
  if (hr < 24) return rtf.format(-hr, 'hour')
  const day = Math.round(hr / 24)
  if (day < 30) return rtf.format(-day, 'day')
  const month = Math.round(day / 30)
  if (month < 12) return rtf.format(-month, 'month')
  return rtf.format(-Math.round(month / 12), 'year')
}

// Elapsed duration as m:ss (or h:mm:ss past an hour). Used for the live search timer. A clock, so
// locale-invariant.
export function formatDuration(ms: number): string {
  const totalSec = Number.isFinite(ms) && ms > 0 ? Math.floor(ms / 1000) : 0
  const h = Math.floor(totalSec / 3600)
  const m = Math.floor((totalSec % 3600) / 60)
  const s = totalSec % 60
  const ss = String(s).padStart(2, '0')
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${ss}`
  return `${m}:${ss}`
}

// Discrete per-step duration: 340ms · 0.4s · 12.3s · 1m 04s. Empty string when not measurable.
// Danish differs only in the decimal separator (12,3s).
export function formatStepDuration(ms: number): string {
  const u = activeMessages().common.units
  if (!Number.isFinite(ms) || ms < 0) return ''
  if (ms < 1000) return `${Math.round(ms)}${u.ms}`
  const sec = ms / 1000
  if (sec < 60) return `${dec(sec, 1)}${u.s}`
  const total = Math.round(sec)
  return `${Math.floor(total / 60)}${u.m} ${String(total % 60).padStart(2, '0')}${u.s}`
}

export function formatAbsolute(iso: string | undefined): string {
  if (!iso) return activeMessages().common.emDash
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return dateTimeFormat(activeLocale()).format(date)
}
