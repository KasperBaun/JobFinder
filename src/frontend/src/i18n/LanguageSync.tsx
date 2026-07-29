import { useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getSetupStatus } from '../api/client'
import { isLocale } from './locale'
import { readStoredLocale } from './storage'
import { useLocale } from './useT'

/**
 * Adopts the language persisted in bootstrap.json once /api/setup/status resolves. A locale already
 * stored in this browser wins, because it is the more recent explicit choice — and honouring it is
 * what lets the provider resolve the language synchronously at boot instead of flashing English.
 *
 * Shares the ['setup'] query key with App, so this costs no extra request.
 */
export function LanguageSync() {
  const { setLocale } = useLocale()
  const setup = useQuery({ queryKey: ['setup'], queryFn: getSetupStatus })
  const server = setup.data?.language

  useEffect(() => {
    if (readStoredLocale()) return
    if (isLocale(server)) setLocale(server)
  }, [server, setLocale])

  return null
}
