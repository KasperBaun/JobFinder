export function ServerDisconnectedOverlay() {
  return (
    <div className="overlay">
      <div className="overlay__card">
        <h2 className="overlay__title">Server disconnected</h2>
        <p className="overlay__hint">
          The jobfinder app is no longer running. You can close this tab.
        </p>
      </div>
    </div>
  )
}
