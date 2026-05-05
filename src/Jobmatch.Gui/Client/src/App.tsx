import { Routes, Route, Link } from 'react-router-dom'
import { useServerConnection } from './hooks/useServerConnection'
import { ServerDisconnectedOverlay } from './components/ServerDisconnectedOverlay'
import { HomePage } from './pages/HomePage'

function NotFoundPage() {
  return (
    <div className="page-error">
      <h1 className="page-error__heading">404</h1>
      <p>Page not found.</p>
      <Link to="/" className="btn btn--primary page-error__home-link">
        Go home
      </Link>
    </div>
  )
}

export function App() {
  const connectionStatus = useServerConnection()

  return (
    <>
      {connectionStatus === 'disconnected' && <ServerDisconnectedOverlay />}
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </>
  )
}
