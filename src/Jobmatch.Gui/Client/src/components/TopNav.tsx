import { NavLink, useNavigate } from 'react-router-dom'
import { shutdown } from '../api/client'

const links: { to: string; label: string; end?: boolean }[] = [
  { to: '/', label: 'Home', end: true },
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
      // server may close before responding; that's expected
    }
    navigate('/closed')
  }

  return (
    <nav className="top-nav">
      <div className="top-nav__inner">
        <div className="top-nav__brand">jobfinder</div>
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
        <button type="button" className="btn btn--secondary top-nav__quit" onClick={handleQuit}>
          Quit
        </button>
      </div>
    </nav>
  )
}
