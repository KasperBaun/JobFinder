import { NavLink, useNavigate } from 'react-router-dom'
import { shutdown } from '../api/client'

const links: { to: string; label: string; end?: boolean }[] = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/providers', label: 'Providers' },
  { to: '/skillset', label: 'Skillset' },
  { to: '/search', label: 'Search' },
  { to: '/history', label: 'History' },
]

export function TopNav() {
  const navigate = useNavigate()

  async function handleQuit() {
    if (!confirm('Stop the jobfinder app?')) return
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
          <span className="top-nav__brand-mark" />
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
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>
        <button
          type="button"
          className="top-nav__quit"
          onClick={handleQuit}
          aria-label="Stop the jobfinder app"
          data-tooltip="Stop the jobfinder app"
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
