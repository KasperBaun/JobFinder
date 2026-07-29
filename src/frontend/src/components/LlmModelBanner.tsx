import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getLlmStatus, startLlmDownload } from '../api/client'
import { useT } from '../i18n'

function formatBytes(n: number | null): string {
  if (n === null || n === undefined) return '?'
  const gb = n / (1024 * 1024 * 1024)
  if (gb >= 1) return gb.toFixed(2) + ' GB'
  const mb = n / (1024 * 1024)
  return mb.toFixed(1) + ' MB'
}

// Surfaced on the Search page when the LLM is enabled but the model file isn't on disk yet.
// Clicking Download starts a background download server-side; progress is read by polling
// /api/llm/status. Because the state lives on the server (not in this component), the download —
// and its progress bar — survive navigating away and reloading: the query just refetches on
// remount and picks the transfer back up.
export function LlmModelBanner() {
  const t = useT('cv')
  const queryClient = useQueryClient()
  const { data: status, isLoading } = useQuery({
    queryKey: ['llm-status'],
    queryFn: getLlmStatus,
    refetchOnWindowFocus: false,
    // Poll only while a download is in flight; stops once it completes/fails or the model is present.
    refetchInterval: (query) =>
      query.state.data?.download.state === 'downloading' ? 1000 : false,
  })

  const [starting, setStarting] = useState(false)
  const [startError, setStartError] = useState<string | null>(null)

  if (isLoading || !status) return null
  if (!status.enabled) return null
  if (status.modelPresent) return null

  const dl = status.download
  const downloading = dl.state === 'downloading' || starting
  const error = startError ?? (dl.state === 'failed' ? dl.error : null)

  async function onDownload() {
    setStartError(null)
    setStarting(true)
    try {
      await startLlmDownload()
      await queryClient.invalidateQueries({ queryKey: ['llm-status'] })
    } catch (err) {
      setStartError(err instanceof Error ? err.message : String(err))
    } finally {
      setStarting(false)
    }
  }

  const pct = dl.totalBytes
    ? Math.min(100, Math.round((dl.downloadedBytes / dl.totalBytes) * 100))
    : null

  return (
    <aside
      style={{
        border: '1px solid #1d3557',
        borderRadius: 6,
        padding: '14px 18px',
        margin: '12px 0 24px 0',
        background: '#fafafa',
        color: '#1d3557',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 16 }}>
        <div>
          <strong>{t.modelBannerTitle}</strong>
          <div style={{ fontSize: 13, marginTop: 4 }}>
            {t.modelBannerExpects(status.provider, status.modelPath)}
          </div>
        </div>
        {!downloading && (
          <button
            onClick={onDownload}
            style={{
              padding: '8px 16px',
              background: '#1d3557',
              color: 'white',
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              whiteSpace: 'nowrap',
            }}
          >
            {dl.state === 'failed' ? t.retryDownload : t.downloadModel}
          </button>
        )}
      </div>

      {downloading && (
        <div style={{ marginTop: 12 }}>
          <div style={{ fontSize: 13 }}>
            {t.downloading(formatBytes(dl.downloadedBytes), dl.totalBytes ? formatBytes(dl.totalBytes) : null, pct)}
          </div>
          <div style={{ height: 4, background: '#e0e0e0', borderRadius: 2, marginTop: 6, overflow: 'hidden' }}>
            <div
              style={{
                height: '100%',
                width: pct !== null ? `${pct}%` : '8%',
                background: '#1d3557',
                transition: 'width 0.3s ease',
              }}
            />
          </div>
        </div>
      )}

      {error && (
        <div style={{ marginTop: 12, fontSize: 13, color: '#a00' }}>
          {t.downloadFailed(error)}
        </div>
      )}
    </aside>
  )
}
