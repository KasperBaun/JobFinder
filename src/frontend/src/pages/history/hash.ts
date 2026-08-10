export type TabKey = 'shortlist' | 'longlist' | 'raw' | 'dedupe'

const VALID: TabKey[] = ['shortlist', 'longlist', 'raw', 'dedupe']

export function parseHash(hash: string): { tab: TabKey; provider?: string } {
  const cleaned = hash.startsWith('#') ? hash.slice(1) : hash
  const params = new URLSearchParams(cleaned)
  const raw = params.get('tab')
  const provider = params.get('provider') ?? undefined
  // Back-compat: legacy ?tab=ranking → longlist
  const normalised = raw === 'ranking' ? 'longlist' : raw
  if (normalised && (VALID as string[]).includes(normalised)) {
    return { tab: normalised as TabKey, provider }
  }
  return { tab: 'shortlist', provider }
}

// Amends the current hash rather than rebuilding it, so the longlist's filters and sort survive a
// trip to another result tab and back. `provider` is scoped to the raw tab and cleared on the way out.
export function buildHash(current: string, tab: TabKey, provider?: string): string {
  const cleaned = current.startsWith('#') ? current.slice(1) : current
  const params = new URLSearchParams(cleaned)
  params.set('tab', tab)
  if (provider) params.set('provider', provider)
  else params.delete('provider')
  return `#${params.toString()}`
}
