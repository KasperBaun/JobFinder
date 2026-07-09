import { useState } from 'react'
import { setProviderSecrets } from '../../api/client'

export function friendlySecretLabel(name: string): string {
  switch (name) {
    case 'api_key': return 'API key'
    case 'affid':   return 'Affiliate ID'
    default:        return 'Access key'
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
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState<{ kind: 'ok' | 'err'; text: string } | null>(null)

  async function save() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: value })
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      setValue('')
      setMsg({ kind: 'ok', text: 'Saved.' })
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
      if (!res.success) throw new Error(res.error ?? 'Clear failed')
      setMsg({ kind: 'ok', text: 'Cleared.' })
      onSaved()
    } catch (e) {
      setMsg({ kind: 'err', text: e instanceof Error ? e.message : String(e) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card">
      <h2 className="card__title">{friendlySecretLabel(secretName)}</h2>
      <p className="field__hint">
        Saved on this computer only. Until you save a value here, this source is skipped when you search.
      </p>
      <div className="secrets-form">
        <input
          className="input input--mono"
          type="password"
          autoComplete="off"
          placeholder={hasSecret ? '••••••••  (overwrite to update)' : `Paste your ${friendlySecretLabel(secretName)}`}
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
          {saving ? <span className="spinner" /> : 'Save'}
        </button>
        {hasSecret && (
          <button type="button" className="btn btn--ghost btn--sm" disabled={saving} onClick={clear}>
            Clear
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
