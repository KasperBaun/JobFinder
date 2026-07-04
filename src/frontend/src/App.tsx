import { Routes, Route, Link, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getSetupStatus } from './api/client'
import { useServerConnection } from './hooks/useServerConnection'
import { ServerDisconnectedOverlay } from './components/ServerDisconnectedOverlay'
import { TopNav } from './components/TopNav'
import { HomePage } from './pages/HomePage'
import { ProvidersPage } from './pages/ProvidersPage'
import { ProviderDetailPage } from './pages/ProviderDetailPage'
import { SkillsetPage } from './pages/SkillsetPage'
import { SearchPage } from './pages/SearchPage'
import { HistoryPage } from './pages/HistoryPage'
import { SettingsPage } from './pages/SettingsPage'
import { SetupPage } from './pages/SetupPage'
import { ClosedPage } from './pages/ClosedPage'

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
  const location = useLocation()
  const isClosed = location.pathname === '/closed'

  // First-run gate: until the user has confirmed where their data lives, show only the setup screen.
  const setup = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })
  if (!isClosed && setup.isLoading) {
    return <div className="app-boot" />
  }
  if (!isClosed && setup.data && !setup.data.configured) {
    return <SetupPage />
  }

  return (
    <>
      {!isClosed && connectionStatus === 'disconnected' && <ServerDisconnectedOverlay />}
      {!isClosed && <TopNav />}
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/providers" element={<ProvidersPage />} />
        <Route path="/providers/:id" element={<ProviderDetailPage />} />
        <Route path="/skillset" element={<SkillsetPage />} />
        <Route path="/search" element={<SearchPage />} />
        <Route path="/history" element={<HistoryPage />} />
        <Route path="/history/:runId" element={<HistoryPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/closed" element={<ClosedPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </>
  )
}
