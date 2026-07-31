import { useCallback, useEffect, useState } from 'react'
import type { ListingMatch } from '../api/types'

// The main process sanitizes for the filesystem; this only builds the human-readable suggestion.
export function suggestedPdfFileName(match: Pick<ListingMatch, 'company' | 'title'>): string {
  return `${[match.company, match.title].filter(Boolean).join(' - ')}.pdf`
}

type PrintState = 'idle' | 'printing' | 'failed'

/**
 * Drives the save-as-PDF flow for one listing: while `printing` the caller mounts the print
 * portal, then the desktop shell's native save dialog takes over when present
 * (`window.jobfinderDesktop.printToPdf`), else the system print dialog (`window.print`).
 */
export function usePrintListing(match: ListingMatch) {
  const [state, setState] = useState<PrintState>('idle')

  useEffect(() => {
    if (state !== 'printing') return
    let cancelled = false
    const finish = (failed: boolean) => {
      if (!cancelled) setState(failed ? 'failed' : 'idle')
    }
    // Two frames so the portal is painted before the page is captured.
    const frame = requestAnimationFrame(() =>
      requestAnimationFrame(() => {
        const desktopPrint = window.jobfinderDesktop?.printToPdf
        if (desktopPrint) {
          desktopPrint(suggestedPdfFileName(match))
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
