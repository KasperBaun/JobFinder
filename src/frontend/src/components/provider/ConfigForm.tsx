import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { setProviderConfig } from '../../api/client'
import type { ProviderConfigUpdate, ProviderConfigView } from '../../api/types'
import { Toggle } from '../Toggle'
import { useT } from '../../i18n'

// Editable per-user override of a source's fetch knobs. Inputs start from the effective values; Save
// persists any that differ from the catalog default, Reset clears every override. Pagination knobs are
// hidden for sources that fetch in a single request (they have nothing to page through).
export function ConfigForm({
  providerId,
  config,
  onSaved,
  onError,
}: {
  providerId: number
  config: ProviderConfigView
  onSaved: () => void
  onError: (message: string) => void
}) {
  const t = useT('providers')
  const common = useT('common')
  const [maxPages, setMaxPages] = useState(config.maxPages != null ? String(config.maxPages) : '')
  const [pageSize, setPageSize] = useState(config.pageSize != null ? String(config.pageSize) : '')
  const [rate, setRate] = useState(String(config.rateLimitRps))
  const [enrich, setEnrich] = useState(config.enrichBody)

  const save = useMutation({
    mutationFn: (update: ProviderConfigUpdate) => setProviderConfig(providerId, update),
    onSuccess: (res) => {
      if (!res.success) onError(res.error ?? t.saveFailed)
      else onSaved()
    },
    onError: (e) => onError(e instanceof Error ? e.message : String(e)),
  })

  // A value equal to the catalog default is sent as null so it isn't recorded as an override.
  function numOrNull(s: string, def: number | undefined): number | null {
    const n = Number(s)
    if (s.trim() === '' || !Number.isFinite(n)) return null
    return def != null && n === def ? null : n
  }

  function onSave() {
    save.mutate({
      maxPages: config.paginates ? numOrNull(maxPages, config.defaults.maxPages) : null,
      pageSize: config.paginates ? numOrNull(pageSize, config.defaults.pageSize) : null,
      rateLimitRps: numOrNull(rate, config.defaults.rateLimitRps),
      enrichBody: enrich === config.defaults.enrichBody ? null : enrich,
    })
  }

  function onReset() {
    setMaxPages(config.defaults.maxPages != null ? String(config.defaults.maxPages) : '')
    setPageSize(config.defaults.pageSize != null ? String(config.defaults.pageSize) : '')
    setRate(String(config.defaults.rateLimitRps))
    setEnrich(config.defaults.enrichBody)
    save.mutate({ maxPages: null, pageSize: null, rateLimitRps: null, enrichBody: null })
  }

  const anyOverridden =
    config.maxPagesOverridden || config.pageSizeOverridden || config.rateLimitOverridden || config.enrichBodyOverridden

  return (
    <section className="card">
      <h2 className="card__title">{t.configurationTitle}</h2>
      <p className="field__hint">{t.configurationHint}</p>

      <div className="provider-detail__form">
        {config.paginates && (
          <>
            <NumberField
              label={t.maxPages}
              value={maxPages}
              onChange={setMaxPages}
              defaultValue={config.defaults.maxPages}
              overridden={config.maxPagesOverridden}
              min={1}
            />
            <NumberField
              label={t.pageSize}
              value={pageSize}
              onChange={setPageSize}
              defaultValue={config.defaults.pageSize}
              overridden={config.pageSizeOverridden}
              min={1}
            />
          </>
        )}
        <NumberField
          label={t.rateLimitField}
          value={rate}
          onChange={setRate}
          defaultValue={config.defaults.rateLimitRps}
          overridden={config.rateLimitOverridden}
          min={0}
          step={0.5}
        />
        <div className="field">
          <label className="field__label">
            {t.fullDescriptions}
            {config.enrichBodyOverridden && <span className="provider-config-overridden">{t.custom}</span>}
          </label>
          <Toggle
            checked={enrich}
            onChange={setEnrich}
            label={enrich ? t.on : t.off}
            ariaLabel={t.bodyEnrichmentAria}
          />
          <span className="field__hint">{t.enrichHint(config.defaults.enrichBody ? t.on : t.off)}</span>
        </div>
      </div>

      <div className="add-source__actions" style={{ marginTop: 'var(--space-4)' }}>
        <button type="button" className="btn btn--primary btn--sm" disabled={save.isPending} onClick={onSave}>
          {save.isPending ? <span className="spinner" /> : common.save}
        </button>
        <button
          type="button"
          className="btn btn--ghost btn--sm"
          disabled={save.isPending || !anyOverridden}
          onClick={onReset}
        >
          {t.resetToDefaults}
        </button>
      </div>
    </section>
  )
}

function NumberField({
  label,
  value,
  onChange,
  defaultValue,
  overridden,
  min,
  step,
}: {
  label: string
  value: string
  onChange: (v: string) => void
  defaultValue?: number
  overridden: boolean
  min: number
  step?: number
}) {
  const { custom, defaultIs } = useT('providers')
  return (
    <div className="field">
      <label className="field__label">
        {label}
        {overridden && <span className="provider-config-overridden">{custom}</span>}
      </label>
      <input
        className="input input--narrow"
        type="number"
        min={min}
        step={step ?? 1}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {defaultValue != null && <span className="field__hint">{defaultIs(defaultValue)}</span>}
    </div>
  )
}
