export function ClosedPage() {
  return (
    <div className="closed-page">
      <div className="overlay__card">
        <h1 className="overlay__title">Goodbye<span style={{ color: 'var(--c-action)' }}>.</span></h1>
        <p className="overlay__hint">
          The jobfinder app has stopped. You can close this tab.
        </p>
      </div>
    </div>
  )
}
