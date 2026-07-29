import { NavLink, useNavigate } from 'react-router-dom'
import { shutdown } from '../api/client'
import { useT } from '../i18n'
import type { Messages } from '../i18n'

const links: { to: string; label: keyof Messages['nav']; end?: boolean }[] = [
  { to: '/', label: 'overview', end: true },
  { to: '/providers', label: 'sources' },
  { to: '/skillset', label: 'profile' },
  { to: '/search', label: 'search' },
  { to: '/history', label: 'history' },
  { to: '/applications', label: 'applications' },
  { to: '/settings', label: 'settings' },
]

export function TopNav() {
  const navigate = useNavigate()
  const t = useT('nav')

  async function handleQuit() {
    // Desktop app: one click closes the native window and quits — the same as the titlebar X.
    // No confirm, no goodbye screen (a stray quit is cheap: searches run as durable background
    // jobs that survive restart).
    if (window.jobfinderDesktop) {
      window.jobfinderDesktop.quit()
      return
    }
    // Browser web-shell: there's no window to close, so confirm, stop the backend, then land on
    // the goodbye page telling the user they can close the tab.
    if (!confirm(t.quitConfirm)) return
    try {
      await shutdown()
    } catch {
      // server may close before responding; expected
    }
    navigate('/closed')
  }

  return (
    <nav className="top-nav">
      <div className="top-nav__inner">
        <NavLink to="/" className="top-nav__brand" end>
          <svg
            className="top-nav__brand-mark"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M10 20a1 1 0 0 0 .553.895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517-1.341L21.74 4.67A1 1 0 0 0 21 3H3a1 1 0 0 0-.742 1.67l7.225 7.989A2 2 0 0 1 10 14z" />
          </svg>
          jobfinder
          <span className="top-nav__brand-tag">v1</span>
        </NavLink>
        <ul className="top-nav__links">
          {links.map(link => (
            <li key={link.to}>
              <NavLink
                to={link.to}
                end={link.end}
                className={({ isActive }) =>
                  isActive ? 'nav-link nav-link--active' : 'nav-link'
                }
              >
                {t[link.label]}
              </NavLink>
            </li>
          ))}
        </ul>
        <button
          type="button"
          className="top-nav__quit"
          onClick={handleQuit}
          aria-label={t.quit}
          data-tooltip={t.quit}
        >
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none"
               stroke="currentColor" strokeWidth="2"
               strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M18.36 6.64a9 9 0 1 1-12.73 0" />
            <line x1="12" y1="2" x2="12" y2="12" />
          </svg>
        </button>
      </div>
    </nav>
  )
}
