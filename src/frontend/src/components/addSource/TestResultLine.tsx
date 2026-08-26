import type { ProviderTestResult } from '../../api/types'
import { useT } from '../../i18n'

export function TestResultLine({ result }: { result: ProviderTestResult }) {
  const t = useT('sources')
  return (
    <div className={`provider-test-result provider-test-result--${result.ok ? 'ok' : 'fail'}`}>
      <div className="provider-test-result__head">
        <span className="provider-test-result__dot" aria-hidden />
        <span>{result.ok ? t.foundJobs(result.fetchedCount) : t.nothingCameBack}</span>
        <span className="provider-test-result__meta">{result.durationMs}ms</span>
      </div>
      {result.sampleTitle && <div className="add-source__sample">{t.sample(result.sampleTitle)}</div>}
      {result.error && !result.ok && <div className="add-source__sample">{result.error}</div>}
    </div>
  )
}
