import { useState } from 'react'
import { setProviderSecrets } from '../../api/client'
import { useT } from '../../i18n'
import type { Messages } from '../../i18n'

export function friendlySecretLabel(name: string, t: Messages['providers']): string {
  switch (name) {
    case 'api_key': return t.secretLabel.api_key
    case 'affid':   return t.secretLabel.affid
    default:        return t.secretLabel.other
  }
}

export function SecretsCard({
  providerId,
  secretName,
  hasSecret,
  onSaved,
}: {
  providerId: number
  secretName: string
  hasSecret: boolean
  onSaved: () => void
}) {
  const t = useT('providers')
  const common = useT('common')
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'err'; text: string } | null>(null)

  async function save() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: value })
      if (!res.success) throw new Error(res.error ?? t.saveFailed)
      setValue('')
      setMsg({ kind: 'ok', text: t.savedShort })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'err', text: e instanceof Error ? e.message : String(e) })
    } finally {
      setSaving(false)
    }
  }

  async function clear() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: '' })
      if (!res.success) throw new Error(res.error ?? t.clearFailed)
      setMsg({ kind: 'ok', text: t.clearedShort })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'err', text: e instanceof Error ? e.message : String(e) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card">
      <h2 className="card__title">{friendlySecretLabel(secretName, t)}</h2>
      <p className="field__hint">{t.secretHint}</p>
      <div className="secrets-form">
        <input
          className="input input--mono"
          type="password"
          autoComplete="off"
          placeholder={hasSecret ? t.secretPlaceholderSet : t.secretPlaceholder(friendlySecretLabel(secretName, t))}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          disabled={saving}
        />
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={saving || value.length === 0}
          onClick={save}
        >
          {saving ? <span className="spinner" /> : common.save}
        </button>
        {hasSecret && (
          <button type="button" className="btn btn--ghost btn--sm" disabled={saving} onClick={clear}>
            {t.clear}
          </button>
        )}
        {msg && (
          <span className={msg.kind === 'ok' ? 'muted small' : 'error-text small'}>
            {msg.text}
          </span>
        )}
      </div>
    </section>
  )
}
