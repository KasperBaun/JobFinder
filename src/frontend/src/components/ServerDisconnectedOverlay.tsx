import { useT } from '../i18n'

export function ServerDisconnectedOverlay() {
  const t = useT('common')
  return (
    <div className="overlay">
      <div className="overlay__card">
        <h2 className="overlay__title">{t.serverDisconnectedTitle}</h2>
        <p className="overlay__hint">{t.serverDisconnectedBody}</p>
      </div>
    </div>
  )
}
