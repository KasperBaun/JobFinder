import { useT } from '../i18n'

type Props = {
  address: string
  radiusKm: number
  /** Last saved server state — carries the geocode outcome for the status line. */
  saved?: { address?: string | null; latitude?: number | null; resolvedAddress?: string | null }
  onPatch: (patch: { address?: string; radiusKm?: number }) => void
}

/**
 * Home address + max-distance inputs for the radius filter (R-105), with a status line
 * showing whether the saved address geocoded. Coordinates are server-computed on save;
 * this component never sees or sends them.
 */
export function AddressFields({ address, radiusKm, saved, onPatch }: Props) {
  const t = useT('skillset')

  const savedAddress = saved?.address ?? ''
  let status: { className: string; text: string } | null = null
  if (address.trim().length > 0) {
    if (address.trim() !== savedAddress) {
      status = { className: 'address-status', text: t.addressPending }
    } else if (saved?.latitude != null) {
      status = { className: 'address-status address-status--ok', text: t.addressResolved(saved.resolvedAddress ?? savedAddress) }
    } else {
      status = { className: 'address-status address-status--warn', text: t.addressNotResolved }
    }
  }

  return (
    <>
      <div className="field" style={{ gridColumn: '1 / -1' }}>
        <label className="field__label" htmlFor="sk-address">{t.homeAddress}</label>
        <input id="sk-address" className="input" value={address}
          placeholder={t.homeAddressPlaceholder}
          onChange={(e) => onPatch({ address: e.target.value })} />
        {status
          ? <p className={status.className}>{status.text}</p>
          : <p className="field__hint">{t.homeAddressHint}</p>}
      </div>
      <div className="field">
        <label className="field__label" htmlFor="sk-radius">{t.radiusKm}</label>
        <input id="sk-radius" type="number" min={0} className="input input--narrow input--mono tabular"
          value={radiusKm}
          onChange={(e) => onPatch({ radiusKm: Math.max(0, Number(e.target.value) || 0) })} />
        <p className="field__hint">{t.radiusKmHint}</p>
      </div>
    </>
  )
}
