import { createPortal } from 'react-dom'
import type { ListingMatch } from '../api/types'
import { useT } from '../i18n'
import { canPrintListing, usePrintListing } from '../hooks/usePrintListing'
import { formatAbsolute } from '../utils/time'
import { Toast } from './Toast'

interface Props {
  match: ListingMatch
}

export function PrintListingButton({ match }: Props) {
  const t = useT('listing')
  const { printing, failed, print, dismissError } = usePrintListing(match)
  if (!canPrintListing(match)) return null
  return (
    <>
      <button type="button" className="btn" onClick={print} disabled={printing} title={t.savePdfTooltip}>
        {t.savePdf}
      </button>
      {printing && createPortal(<PrintListingView match={match} />, document.body)}
      {failed && <Toast kind="err" message={t.savePdfFailed} onDismiss={dismissError} />}
    </>
  )
}

// Fallback print view for the portal-capture paths (browser shell / older desktop shells) —
// hidden on screen; @media print (css/print.css) swaps the app chrome out for it. The current
// desktop shell captures the posting page itself instead and never prints this.
function PrintListingView({ match }: Props) {
  const t = useT('listing')
  const subline = [match.company, match.location].filter(Boolean).join(' · ')
  return (
    <section className="print-listing">
      <h1 className="print-listing__title">{match.title}</h1>
      {subline && <p className="print-listing__subline">{subline}</p>}
      <dl className="print-listing__meta">
        <dt>{t.printPortal}</dt>
        <dd>{match.portalDisplayName ?? match.portal}</dd>
        {match.postedAt && (
          <>
            <dt>{t.printPosted}</dt>
            <dd>{formatAbsolute(match.postedAt)}</dd>
          </>
        )}
        <dt>{t.printSource}</dt>
        <dd>{match.url}</dd>
      </dl>
      {match.description && <div className="print-listing__body">{match.description}</div>}
    </section>
  )
}
