import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nProvider } from './I18nProvider'
import { activeLocale } from './active'
import { useLocale, useT } from './useT'

function Probe() {
  const t = useT('nav')
  const { locale, setLocale } = useLocale()
  return (
    <div>
      <span data-testid="settings">{t.settings}</span>
      <span data-testid="locale">{locale}</span>
      <button type="button" onClick={() => setLocale(locale === 'en' ? 'da' : 'en')}>toggle</button>
    </div>
  )
}

beforeEach(() => localStorage.clear())

describe('I18nProvider', () => {
  it('renders English strings without a provider, so unwrapped tests keep working', () => {
    render(<Probe />)
    expect(screen.getByTestId('settings')).toHaveTextContent('Settings')
  })

  it('renders the pinned locale', () => {
    render(<I18nProvider locale="da"><Probe /></I18nProvider>)
    expect(screen.getByTestId('settings')).toHaveTextContent('Indstillinger')
  })

  it('re-renders the tree and persists the choice when the locale changes', async () => {
    render(<I18nProvider locale="en"><Probe /></I18nProvider>)
    await userEvent.click(screen.getByRole('button', { name: 'toggle' }))

    expect(screen.getByTestId('locale')).toHaveTextContent('da')
    expect(screen.getByTestId('settings')).toHaveTextContent('Indstillinger')
    expect(localStorage.getItem('jobfinder.lang')).toBe('da')
  })

  it('keeps document.documentElement.lang in step', async () => {
    render(<I18nProvider locale="en"><Probe /></I18nProvider>)
    expect(document.documentElement.lang).toBe('en')

    await userEvent.click(screen.getByRole('button', { name: 'toggle' }))
    expect(document.documentElement.lang).toBe('da')
  })

  it('mirrors the locale into the module singleton the formatters read', () => {
    render(<I18nProvider locale="da"><Probe /></I18nProvider>)
    expect(activeLocale()).toBe('da')
  })
})
