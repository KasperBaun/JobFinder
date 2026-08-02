import { useCallback, useEffect, useState } from 'react'
import type { ListingMatch } from '../api/types'

// The main process sanitizes for the filesystem; this only builds the human-readable suggestion.
export function suggestedPdfFileName(match: Pick<ListingMatch, 'company' | 'title'>): string {
  return `${[match.company, match.title].filter(Boolean).join(' - ')}.pdf`
}

// Without the desktop shell's source capture, a PDF is only worth offering when the ad text is
// persisted — the metadata-only header alone isn't a useful save (use "Open job posting" instead).
export function canPrintListing(match: Pick<ListingMatch, 'description'>): boolean {
  return Boolean(window.jobfinderDesktop?.printSourceToPdf) || Boolean(match.description)
}

type PrintState = 'idle' | 'printing' | 'failed'

/**
 * Drives the save-as-PDF flow for one listing. Preferred path: the desktop shell captures the
 * posting page itself (`printSourceToPdf`) — the PDF is the ad as the site renders it. Fallbacks
 * capture the SPA's print portal instead: `printToPdf` (older desktop shells) or `window.print()`
 * (the browser web-shell).
 */
export function usePrintListing(match: ListingMatch) {
  const [state, setState] = useState<PrintState>('idle')

  useEffect(() => {
    if (state !== 'printing') return
    let cancelled = false
    const finish = (failed: boolean) => {
      if (!cancelled) setState(failed ? 'failed' : 'idle')
    }
    const desktop = window.jobfinderDesktop
    if (desktop?.printSourceToPdf) {
      desktop
        .printSourceToPdf(match.url, suggestedPdfFileName(match))
        .then(saved => finish(!saved))
        .catch(() => finish(true))
      return () => {
        cancelled = true
      }
    }
    // Portal-capture paths: two frames so the print view is painted before the page is captured.
    const frame = requestAnimationFrame(() =>
      requestAnimationFrame(() => {
        if (desktop?.printToPdf) {
          desktop
            .printToPdf(suggestedPdfFileName(match))
            .then(saved => finish(!saved))
            .catch(() => finish(true))
        } else {
          window.print()
          finish(false)
        }
      }),
    )
    return () => {
      cancelled = true
      cancelAnimationFrame(frame)
    }
  }, [state, match])

  return {
    printing: state === 'printing',
    failed: state === 'failed',
    print: useCallback(() => setState('printing'), []),
    dismissError: useCallback(() => setState('idle'), []),
  }
}
