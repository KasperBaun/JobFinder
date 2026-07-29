import { useT } from '../i18n'

export function ClosedPage() {
  const t = useT('common')
  return (
    <div className="closed-page">
      <div className="overlay__card">
        <h1 className="overlay__title">{t.goodbyeTitle}<span style={{ color: 'var(--c-action)' }}>.</span></h1>
        <p className="overlay__hint">{t.goodbyeBody}</p>
      </div>
    </div>
  )
}
