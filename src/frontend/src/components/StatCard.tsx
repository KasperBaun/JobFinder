import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'

interface Props {
  label: string
  value: ReactNode
  subtitle?: ReactNode
  link?: string
}

export function StatCard({ label, value, subtitle, link }: Props) {
  const content = (
    <>
      <div className="stat-card__label">{label}</div>
      <div className="stat-card__value">{value}</div>
      {subtitle !== undefined && <div className="stat-card__subtitle">{subtitle}</div>}
    </>
  )

  if (link) {
    return (
      <Link to={link} className="stat-card stat-card--clickable">
        {content}
      </Link>
    )
  }
  return <div className="stat-card">{content}</div>
}
